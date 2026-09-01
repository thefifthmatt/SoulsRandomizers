using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon.Converters
{
    public class ContainsItemConverter : IValueConverter
    {
        public static readonly ContainsItemConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                // Don't consider this an error if the list is nullable
                return false;
            }
            if (value is System.Collections.IEnumerable set && targetType == typeof(bool))
            {
                return set.Cast<object>().Contains(parameter);
            }
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return new BindingNotification(new InvalidOperationException(), BindingErrorType.Error);
        }
    }
}
