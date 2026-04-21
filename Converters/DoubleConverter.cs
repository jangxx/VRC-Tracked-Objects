using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VRC_OSC_ExternallyTrackedObject.Converters
{
    [ValueConversion(typeof(double), typeof(string))]
    internal class DoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = (double)value;
            return val.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? stringVal = value as string;

            if (stringVal == null)
            {
                return DependencyProperty.UnsetValue;
            }

            double resultValue;
            if (double.TryParse(stringVal, NumberStyles.Float, CultureInfo.InvariantCulture, out resultValue))
            {
                return resultValue;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
