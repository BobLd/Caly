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
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Caly.Core.Converters
{
    // This is very hackish - need to implement our own NumericUpDown control
    public sealed class NumericUpDownPdfPageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return 1.ToString();
            }

            if (value is int i)
            {
                return i.ToString();
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int max = int.MaxValue;
            if (parameter is Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindingExtension ext && ext.DefaultAnchor?.Target is NumericUpDown numUpDown && numUpDown.Maximum < max)
            {
                max = (int)numUpDown.Maximum;
            }

            if (value is null || string.IsNullOrEmpty((string)value))
            {
                return max + 1;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is string s)
            {
                if (int.TryParse(s, out i))
                {
                    if (i <= 0)
                    {
                        return 1;
                    }

                    if (i > max)
                    {
                        return max + 1;
                    }

                    return i;
                }

                if (double.TryParse(s, out double d))
                {
                    if (d > max)
                    {
                        return max;
                    }

                    if (d <= 0)
                    {
                        return 1;
                    }

                    return (int)d;
                }
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }
    }
}
