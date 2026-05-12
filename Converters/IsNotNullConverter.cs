using System;
using System.Globalization;
using System.Windows.Data;

namespace VRC_OSC_ExternallyTrackedObject.Converters
{
    internal class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
