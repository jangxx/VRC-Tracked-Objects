using System;
using System.Globalization;
using System.Windows.Data;

namespace VRC_OSC_ExternallyTrackedObject.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    internal class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = (bool)value;
            return !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
