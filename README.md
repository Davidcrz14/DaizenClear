# DaizenClear

## Características

- **Limpieza agresiva**: Sistema de limpieza en múltiples fases que libera más memoria que las soluciones convencionales
- **Interfaz moderna**: Diseño limpio con ModernWPF UI para una experiencia visual actualizada
- **Monitoreo en tiempo real**: Visualización del uso de RAM, memoria virtual y caché actualizada cada 5 segundos
- **Limpieza manual**: Botón para limpiar memoria al instante con retroalimentación detallada
- **Configuración flexible**: Ajustes personalizables para limpieza automática basada en umbrales
- **Notificaciones**: Alertas visuales y sonoras después de cada limpieza
- **Inicio con Windows**: Opción para iniciar automáticamente con el sistema y minimizar a la bandeja

## Requisitos del Sistema

- Windows 10 o superior
- .NET 8.0 Runtime
- Privilegios de administrador (obligatorio para las técnicas avanzadas de limpieza de memoria)

## Instalación

1. Descarga la última versión desde la carpeta de compilación (`bin/Debug/net8.0-windows`)
2. Ejecuta `DaizenClear.exe` como administrador.
3. La primera vez, Windows puede mostrar una advertencia de seguridad debido al acceso de bajo nivel

## Uso

### Limpieza Manual
1. Abre la aplicación como administrador
2. Observa el panel que muestra el uso actual de memoria
3. Haz clic en "Limpiar Memoria"
4. El sistema ejecutará una limpieza en múltiples fases que incluye:
   - Limpieza de la memoria caché del sistema
   - Liberación de la working set de procesos
   - Purga de listas standby y modificadas
   - Compactación de montones .NET
5. Una ventana mostrará el resumen de la memoria liberada

### Limpieza Automática
1. Abre la aplicación
2. Haz clic en "Configuración"
3. Activa "Habilitar limpieza automática"
4. Ajusta el umbral de uso (por defecto: 75%)
5. Establece el intervalo de limpieza (por defecto: 30 minutos)
6. La aplicación monitoreará el uso de RAM y limpiará automáticamente cuando sea necesario

## Configuración

### Limpieza Automática
- **Habilitar limpieza automática**: Activa/desactiva el sistema de limpieza automática
- **Umbral de uso de memoria**: Porcentaje de uso de RAM que activará la limpieza automática (por defecto: 75%)
- **Intervalo de limpieza**: Tiempo entre comprobaciones de limpieza automática (minutos)

### Notificaciones
- **Mostrar notificaciones después de limpiar**: Activa/desactiva las notificaciones del sistema
- **Reproducir sonido al limpiar**: Activa/desactiva la retroalimentación sonora

### Inicio del Sistema
- **Iniciar con Windows**: Configura la aplicación para que inicie automáticamente con Windows
- **Iniciar minimizado en la bandeja del sistema**: Inicia la aplicación oculta en el área de notificaciones



# Restaurar dependencias
dotnet restore

# Construir
dotnet build



### Arquitectura

- **WPF**: Interfaz de usuario moderna con .NET 8.0
- **MVVM**: Patrón de diseño para separación de responsabilidades
- **ModernWPF**: Biblioteca para estilos modernos de Windows (v0.9.6)
- **Windows APIs nativas**: Utilizadas a través de P/Invoke para limpieza avanzada de memoria
- **Técnicas de limpieza agresiva**: Implementación basada en los métodos de Mem Reduct
- **Privilegios elevados**: Utiliza manifest para solicitar ejecución como administrador

## Técnicas de Limpieza Implementadas
DaizenClear utiliza una estrategia de limpieza en múltiples fases:

1. **Working Sets**: Reduce los conjuntos de trabajo de todos los procesos
2. **Standby Lists**: Vacía las listas standby y de prioridad cero
3. **Modified Lists**: Limpia listas de páginas modificadas
4. **System Cache**: Reduce la caché del sistema de archivos
5. **Registry Cache**: Optimiza la caché del registro
6. **Combined Memory Lists**: Combina y optimiza varias listas de memoria
7. **Process Working Sets**: Limpieza específica de procesos individuales
8. **Volume Caches**: Limpia caché de volúmenes del sistema
9. **System Temporary Files**: Elimina archivos temporales innecesarios
10. **.NET Heap**: Compacta los montones de .NET y fuerza la recolección de basura



