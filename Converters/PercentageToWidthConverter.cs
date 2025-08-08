using System;
using System.Globalization;
using System.Windows.Data;

namespace MemoryCleaner.Converters
{
    public class PercentageToWidthConverter : IMultiValueConverter, IValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is double percentage && values[1] is double maxWidth)
            {
                // Convertir porcentaje a ancho proporcional
                return Math.Max(0, Math.Min(maxWidth, (percentage / 100.0) * maxWidth));
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // Usar ancho por defecto si no hay parámetro
                double maxWidth = parameter is double width ? width : 200;
                return Math.Max(0, Math.Min(maxWidth, (percentage / 100.0) * maxWidth));
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
