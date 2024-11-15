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

using Avalonia;
using Avalonia.Controls.Primitives;

namespace Caly.Core.Controls;

public sealed class PdfDocumentPropertiesControl : TemplatedControl
{
    private const int DefaultFirstColumnWidth = 95;

    /// <summary>
    /// Defines the <see cref="FirstColumnWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<int> FirstColumnWidthProperty = AvaloniaProperty.Register<PdfPageItem, int>(nameof(FirstColumnWidth));

    public int FirstColumnWidth
    {
        get => GetValue(FirstColumnWidthProperty);
        set => SetValue(FirstColumnWidthProperty, value);
    }

    static PdfDocumentPropertiesControl()
    {
        FirstColumnWidthProperty.OverrideDefaultValue<PdfDocumentPropertiesControl>(DefaultFirstColumnWidth);
    }
}