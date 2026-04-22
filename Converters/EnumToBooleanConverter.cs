using System;
using System.Globalization;
using System.Windows.Data;

namespace VRC_OSC_ExternallyTrackedObject.Converters
{
    //[ValueConversion(typeof(EPlayerType), typeof(bool))]
    internal class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return false;
            } else
            {
                return value.Equals(parameter);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? parameter : Binding.DoNothing;
        }
    }
}
