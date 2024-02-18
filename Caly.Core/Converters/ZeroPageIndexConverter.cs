using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Caly.Core.Converters
{
    public sealed class ZeroPageIndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return 0;
            }

            if (value is int i)
            {
                return Math.Max(0, i - 1);
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i + 1;
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
    }
}
