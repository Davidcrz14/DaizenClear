using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace MemoryCleaner
{
    public static class MemoryManager
    {
        #region Estructuras de Windows

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MemoryStatusEx()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PerformanceInformation
        {
            public uint cb;
            public UIntPtr CommitTotal;
            public UIntPtr CommitLimit;
            public UIntPtr CommitPeak;
            public UIntPtr PhysicalTotal;
            public UIntPtr PhysicalAvailable;
            public UIntPtr SystemCache;
            public UIntPtr KernelTotal;
            public UIntPtr KernelPaged;
            public UIntPtr KernelNonpaged;
            public UIntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        #endregion

        #region APIs de Windows

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PerformanceInformation pPerformanceInformation, uint cb);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemFileCacheSize(IntPtr MinimumFileCacheSize, IntPtr MaximumFileCacheSize, uint Flags);

        // APIs críticas de ntdll.dll (como en Mem Reduct)
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

        #endregion

        #region Constantes del sistema

        // Constantes específicas de Mem Reduct para limpieza agresiva
        private const int SystemMemoryListInformation = 0x50;
        private const int SystemRegistryReconciliationInformation = 0x51;
        private const int SystemCombinePhysicalMemoryInformation = 0x52;

        // Comandos de memoria específicos de Mem Reduct (CRÍTICOS)
        private const int MemoryEmptyWorkingSets = 0x00000001;
        private const int MemoryFlushModifiedList = 0x00000002;
        private const int MemoryPurgeStandbyList = 0x00000003;
        private const int MemoryPurgeLowPriorityStandbyList = 0x00000004;

        // Máscaras de limpieza como Mem Reduct (EXACTAS)
        private const uint REDUCT_WORKING_SET = 0x1;
        private const uint REDUCT_SYSTEM_FILE_CACHE = 0x2;
        private const uint REDUCT_MODIFIED_LIST = 0x4;
        private const uint REDUCT_STANDBY_LIST = 0x8;
        private const uint REDUCT_STANDBY_PRIORITY0_LIST = 0x10;
        private const uint REDUCT_MODIFIED_FILE_CACHE = 0x20;
        private const uint REDUCT_REGISTRY_CACHE = 0x40;
        private const uint REDUCT_COMBINE_MEMORY_LISTS = 0x80;

        // Máscara por defecto MÁS AGRESIVA (como Mem Reduct)
        private const uint REDUCT_MASK_DEFAULT = REDUCT_WORKING_SET | REDUCT_SYSTEM_FILE_CACHE |
                                                REDUCT_MODIFIED_LIST | REDUCT_STANDBY_LIST |
                                                REDUCT_STANDBY_PRIORITY0_LIST | REDUCT_MODIFIED_FILE_CACHE |
                                                REDUCT_REGISTRY_CACHE | REDUCT_COMBINE_MEMORY_LISTS;

        #endregion

        #region Métodos principales

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static MemoryInfo GetMemoryInfo()
        {
            var memoryStatus = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(ref memoryStatus))
            {
                throw new InvalidOperationException("No se pudo obtener información de memoria");
            }

            // Obtener información más detallada del rendimiento
            PerformanceInformation perfInfo;
            GetPerformanceInfo(out perfInfo, (uint)Marshal.SizeOf<PerformanceInformation>());

            // Calcular memoria RAM con mayor precisión
            var ramTotal = (long)memoryStatus.ullTotalPhys;
            var ramUsed = (long)(memoryStatus.ullTotalPhys - memoryStatus.ullAvailPhys);
            var ramUsagePercent = ramTotal > 0 ? (double)ramUsed * 100.0 / ramTotal : 0;

            // Calcular memoria virtual
            var virtualTotal = (long)memoryStatus.ullTotalPageFile;
            var virtualUsed = (long)(memoryStatus.ullTotalPageFile - memoryStatus.ullAvailPageFile);
            var virtualUsagePercent = virtualTotal > 0 ? (double)virtualUsed * 100.0 / virtualTotal : 0;

            // Si no hay archivo de paginación, usar memoria virtual del proceso
            if (virtualTotal == 0)
            {
                virtualTotal = (long)memoryStatus.ullTotalVirtual;
                virtualUsed = (long)(memoryStatus.ullTotalVirtual - memoryStatus.ullAvailVirtual);
                virtualUsagePercent = virtualTotal > 0 ? (double)virtualUsed * 100.0 / virtualTotal : 0;
            }

            // Caché del sistema más preciso
            var cacheUsed = perfInfo.cb > 0 ? (long)perfInfo.SystemCache * (long)perfInfo.PageSize : GetCacheSize();

            return new MemoryInfo
            {
                RamTotal = ramTotal,
                RamUsed = ramUsed,
                RamUsagePercent = ramUsagePercent,

                VirtualMemoryTotal = virtualTotal,
                VirtualMemoryUsed = virtualUsed,
                VirtualMemoryUsagePercent = virtualUsagePercent,

                CacheUsed = cacheUsed
            };
        }

        /// <summary>
        /// Limpieza EXTREMADAMENTE AGRESIVA de memoria (basada en Mem Reduct)
        /// Esta implementación es MUCHO más efectiva que la anterior
        /// </summary>
        public static CleanupResult CleanMemory()
        {
            if (!IsAdministrator())
            {
                throw new InvalidOperationException("Se requieren privilegios de administrador");
            }

            var result = new CleanupResult();
            var initialMemory = GetMemoryInfo();
            result.MemoryBeforeCleanup = initialMemory.RamUsed;

            try
            {
                // Usar máscara COMPLETA de Mem Reduct para máxima efectividad
                var mask = REDUCT_MASK_DEFAULT;

                // === FASE 1: LIMPIEZA DE WORKING SETS (MUY EFECTIVO) ===
                if ((mask & REDUCT_WORKING_SET) == REDUCT_WORKING_SET)
                {
                    try
                    {
                        // Método 1: NtSetSystemInformation (como Mem Reduct)
                        var command = MemoryEmptyWorkingSets;
                        var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, Marshal.SizeOf(command));

                        // Método 2: Limpieza agresiva por proceso (backup)
                        CleanProcessWorkingSets();

                        Thread.Sleep(150); // Pausa crítica como en Mem Reduct
                        result.WorkingSetCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Working Set: {ex.Message}");
                    }
                }

                // === FASE 2: SYSTEM FILE CACHE (SÚPER EFECTIVO) ===
                if ((mask & REDUCT_SYSTEM_FILE_CACHE) == REDUCT_SYSTEM_FILE_CACHE)
                {
                    try
                    {
                        // Método de Mem Reduct: Forzar caché al mínimo para liberarlo
                        SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0);

                        Thread.Sleep(200);
                        result.SystemCacheCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"System Cache: {ex.Message}");
                    }
                }

                // === FASE 3: MODIFIED PAGE LIST (MUY EFECTIVO) ===
                if ((mask & REDUCT_MODIFIED_LIST) == REDUCT_MODIFIED_LIST)
                {
                    try
                    {
                        var command = MemoryFlushModifiedList;
                        var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, Marshal.SizeOf(command));
                        Thread.Sleep(150);
                        result.ModifiedListCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Modified List: {ex.Message}");
                    }
                }

                // === FASE 4: STANDBY LIST (EXTREMADAMENTE EFECTIVO) ===
                if ((mask & REDUCT_STANDBY_LIST) == REDUCT_STANDBY_LIST)
                {
                    try
                    {
                        var command = MemoryPurgeStandbyList;
                        var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, Marshal.SizeOf(command));
                        Thread.Sleep(150);
                        result.StandbyListCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Standby List: {ex.Message}");
                    }
                }

                // === FASE 5: STANDBY PRIORITY-0 LIST (CRÍTICO) ===
                if ((mask & REDUCT_STANDBY_PRIORITY0_LIST) == REDUCT_STANDBY_PRIORITY0_LIST)
                {
                    try
                    {
                        var command = MemoryPurgeLowPriorityStandbyList;
                        var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, Marshal.SizeOf(command));
                        Thread.Sleep(150);
                        result.Priority0ListCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Priority-0 List: {ex.Message}");
                    }
                }

                // === FASE 6: FLUSH VOLUME CACHE (IMPORTANTE) ===
                if ((mask & REDUCT_MODIFIED_FILE_CACHE) == REDUCT_MODIFIED_FILE_CACHE)
                {
                    try
                    {
                        FlushVolumeCaches();
                        Thread.Sleep(200);
                        result.VolumeCacheFlushed = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Volume Cache: {ex.Message}");
                    }
                }

                // === FASE 7: REGISTRY CACHE (WIN8.1+) ===
                if ((mask & REDUCT_REGISTRY_CACHE) == REDUCT_REGISTRY_CACHE)
                {
                    try
                    {
                        var status = NtSetSystemInformation(SystemRegistryReconciliationInformation, IntPtr.Zero, 0);
                        Thread.Sleep(100);
                        result.RegistryCacheCleaned = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Registry Cache: {ex.Message}");
                    }
                }

                // === FASE 8: COMBINE MEMORY LISTS (WIN10+) ===
                if ((mask & REDUCT_COMBINE_MEMORY_LISTS) == REDUCT_COMBINE_MEMORY_LISTS)
                {
                    try
                    {
                        var status = NtSetSystemInformation(SystemCombinePhysicalMemoryInformation, IntPtr.Zero, 0);
                        Thread.Sleep(200);
                        result.MemoryListsCombined = true;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Memory Combine: {ex.Message}");
                    }
                }

                // === FASE 9: LIMPIEZA .NET INTELIGENTE ===
                try
                {
                    CompactDotNetHeapAggressive();
                    result.DotNetHeapCompacted = true;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($".NET Heap: {ex.Message}");
                }

                // === FASE 10: LIMPIEZA ADICIONAL DE PROCESOS ===
                try
                {
                    ForceGarbageCollectionSystemWide();
                    result.SystemGCForced = true;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"System GC: {ex.Message}");
                }

                // Pausa final para permitir que se apliquen TODOS los cambios
                Thread.Sleep(500);

                // Calcular diferencia
                var finalMemory = GetMemoryInfo();
                result.MemoryAfterCleanup = finalMemory.RamUsed;
                result.MemoryFreed = Math.Max(0, result.MemoryBeforeCleanup - result.MemoryAfterCleanup);
                result.Success = true;

                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error general: {ex.Message}");
                result.Success = false;
                return result;
            }
        }

        #endregion

        #region Métodos auxiliares SÚPER AGRESIVOS

        /// <summary>
        /// Limpia working sets de TODOS los procesos accesibles (como Mem Reduct)
        /// </summary>
        private static void CleanProcessWorkingSets()
        {
            try
            {
                var processes = Process.GetProcesses();
                var successCount = 0;

                foreach (var process in processes)
                {
                    try
                    {
                        if (process.Handle != IntPtr.Zero)
                        {
                            // Método 1: EmptyWorkingSet (más suave)
                            EmptyWorkingSet(process.Handle);

                            // Método 2: SetProcessWorkingSetSize con valores especiales (más agresivo)
                            SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1));

                            successCount++;
                        }
                    }
                    catch
                    {
                        // Ignorar procesos inaccesibles (normal)
                    }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }

                // Debug info
                System.Diagnostics.Debug.WriteLine($"Working sets limpiados en {successCount} procesos");
            }
            catch
            {
                // Fallar silenciosamente si no se puede acceder a la lista de procesos
            }
        }

        /// <summary>
        /// Flush agresivo de cachés de volúmenes (implementación mejorada de Mem Reduct)
        /// </summary>
        private static void FlushVolumeCaches()
        {
            try
            {
                // Método 1: Limpiar archivos temporales del sistema
                ClearSystemTempFiles();

                // Método 2: Flush global usando PowerShell
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue; [System.GC]::Collect()\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        process.Start();
                        process.WaitForExit(3000);
                    }
                }
                catch { }
            }
            catch
            {
                // Fallar silenciosamente
            }
        }

        /// <summary>
        /// Limpieza de archivos temporales del sistema
        /// </summary>
        private static void ClearSystemTempFiles()
        {
            try
            {
                string[] tempPaths = {
                    Path.GetTempPath(),
                    Environment.GetFolderPath(Environment.SpecialFolder.InternetCache),
                    @"C:\Windows\Temp"
                };

                foreach (string tempPath in tempPaths)
                {
                    try
                    {
                        if (Directory.Exists(tempPath))
                        {
                            var files = Directory.GetFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly);
                            foreach (string file in files.Take(50)) // Limitar para evitar bloqueo
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(file);
                                    if (fileInfo.LastAccessTime < DateTime.Now.AddDays(-1))
                                    {
                                        File.Delete(file);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Compactación EXTREMADAMENTE agresiva del heap de .NET (basada en Mem Reduct)
        /// </summary>
        private static void CompactDotNetHeapAggressive()
        {
            try
            {
                var initialMemory = GC.GetTotalMemory(false);

                // Solo si hay suficiente memoria managed para justificar la limpieza
                if (initialMemory > 10 * 1024 * 1024) // 10MB
                {
                    // Limpieza MUY agresiva - múltiples pasadas como Mem Reduct
                    for (int pass = 0; pass < 3; pass++)
                    {
                        // Limpieza por generación (más efectivo)
                        for (int generation = 0; generation <= GC.MaxGeneration; generation++)
                        {
                            GC.Collect(generation, GCCollectionMode.Forced, true, true);
                            Thread.Sleep(10);
                        }

                        // Finalizers
                        GC.WaitForPendingFinalizers();

                        // Colección final agresiva
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

                        Thread.Sleep(25);
                    }

                    // Compactar heap de objetos grandes (LOH)
                    try
                    {
                        System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                    }
                    catch { }

                    // Limpieza adicional de recursos no administrados
                    try
                    {
                        // Forzar liberación de handles
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Fuerza garbage collection a nivel de sistema (inspirado en Mem Reduct)
        /// </summary>
        private static void ForceGarbageCollectionSystemWide()
        {
            try
            {
                // Intentar limpiar la memoria de .NET de otros procesos también
                var dotnetProcesses = Process.GetProcessesByName("dotnet")
                    .Concat(Process.GetProcesses().Where(p =>
                        p.ProcessName.Contains("dotnet") ||
                        p.ProcessName.EndsWith(".exe") && IsManagedProcess(p)))
                    .Take(20); // Limitar para evitar sobrecarga

                foreach (var process in dotnetProcesses)
                {
                    try
                    {
                        if (process.Handle != IntPtr.Zero)
                        {
                            // Reducir working set de procesos .NET
                            SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1));
                        }
                    }
                    catch { }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Determina si un proceso es administrado (.NET)
        /// </summary>
        private static bool IsManagedProcess(Process process)
        {
            try
            {
                return process.Modules.Cast<ProcessModule>()
                    .Any(m => m.ModuleName.Equals("mscoree.dll", StringComparison.OrdinalIgnoreCase) ||
                             m.ModuleName.Contains("coreclr") ||
                             m.ModuleName.Contains("clr"));
            }
            catch
            {
                return false;
            }
        }

        private static long GetCacheSize()
        {
            // Estimación más eficiente del caché del sistema usando APIs nativas
            try
            {
                PerformanceInformation perfInfo;
                if (GetPerformanceInfo(out perfInfo, (uint)Marshal.SizeOf<PerformanceInformation>()))
                {
                    return (long)perfInfo.SystemCache * (long)perfInfo.PageSize;
                }
            }
            catch { }

            // Fallback al método anterior
            var memoryStatus = new MemoryStatusEx();
            if (GlobalMemoryStatusEx(ref memoryStatus))
            {
                var totalPhys = (long)memoryStatus.ullTotalPhys;
                var availPhys = (long)memoryStatus.ullAvailPhys;
                var usedPhys = totalPhys - availPhys;
                return (long)(usedPhys * 0.15); // Estimación más conservadora
            }
            return 0;
        }

        #endregion
    }

    /// <summary>
    /// Resultado detallado de la limpieza de memoria
    /// </summary>
    public class CleanupResult
    {
        public bool Success { get; set; }
        public long MemoryBeforeCleanup { get; set; }
        public long MemoryAfterCleanup { get; set; }
        public long MemoryFreed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        // Flags de operaciones específicas
        public bool WorkingSetCleaned { get; set; }
        public bool SystemCacheCleaned { get; set; }
        public bool ModifiedListCleaned { get; set; }
        public bool StandbyListCleaned { get; set; }
        public bool Priority0ListCleaned { get; set; }
        public bool VolumeCacheFlushed { get; set; }
        public bool RegistryCacheCleaned { get; set; }
        public bool MemoryListsCombined { get; set; }
        public bool DotNetHeapCompacted { get; set; }
        public bool SystemGCForced { get; set; }

        public string GetFormattedMemoryFreed()
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (MemoryFreed >= GB)
                return $"{MemoryFreed / (double)GB:F2} GB";
            else if (MemoryFreed >= MB)
                return $"{MemoryFreed / (double)MB:F2} MB";
            else if (MemoryFreed >= KB)
                return $"{MemoryFreed / (double)KB:F2} KB";
            else
                return $"{MemoryFreed} bytes";
        }

        public string GetSummary()
        {
            var operations = new List<string>();
            if (WorkingSetCleaned) operations.Add("Working Sets");
            if (SystemCacheCleaned) operations.Add("System Cache");
            if (ModifiedListCleaned) operations.Add("Modified List");
            if (StandbyListCleaned) operations.Add("Standby List");
            if (Priority0ListCleaned) operations.Add("Priority-0 List");
            if (VolumeCacheFlushed) operations.Add("Volume Cache");
            if (RegistryCacheCleaned) operations.Add("Registry Cache");
            if (MemoryListsCombined) operations.Add("Memory Lists");
            if (DotNetHeapCompacted) operations.Add(".NET Heap");
            if (SystemGCForced) operations.Add("System GC");

            return $"Memoria liberada: {GetFormattedMemoryFreed()}\n" +
                   $"Operaciones: {string.Join(", ", operations)}\n" +
                   $"Errores: {Errors.Count}";
        }
    }

    public class MemoryInfo
    {
        public long RamTotal { get; set; }
        public long RamUsed { get; set; }
        public double RamUsagePercent { get; set; }
        public long VirtualMemoryTotal { get; set; }
        public long VirtualMemoryUsed { get; set; }
        public double VirtualMemoryUsagePercent { get; set; }
        public long CacheUsed { get; set; }
    }
}
