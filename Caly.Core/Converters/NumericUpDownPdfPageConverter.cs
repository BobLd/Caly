// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Caly.Core.Converters
{
    public sealed class NumericUpDownPdfPageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                null => null,
                int i => i.ToString("D", CultureInfo.InvariantCulture),
                _ => new BindingNotification(new InvalidCastException($"Got '{value}'."), BindingErrorType.Error)
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || string.IsNullOrEmpty((string)value))
            {
                return null;
            }

            if (value is int i)
            {
                if (i <= 0)
                {
                    return null;
                }

                return i;
            }

            if (value is not string s)
            {
                return null;
            }

            if (int.TryParse(s, out i))
            {
                if (i <= 0)
                {
                    return null;
                }

                return i;
            }

            if (!double.TryParse(s, out double d))
            {
                return null;
            }

            if (d > int.MaxValue)
            {
                return null; // TODO - Should be max page
            }

            if (d <= 0)
            {
                return null;
            }

            if (double.IsNaN(d))
            {
                return null; // Corner case
            }

            if (double.IsInfinity(d))
            {
                return null; // Corner case
            }

            return (int)d;
        }
    }
}
