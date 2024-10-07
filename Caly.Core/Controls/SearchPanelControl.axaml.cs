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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

public sealed partial class SearchPanelControl : UserControl
{
    public SearchPanelControl()
    {
#if DEBUG
        if (Design.IsDesignMode)
        {
            DataContext = new PdfDocumentViewModel(null);
        }
#endif

        InitializeComponent();
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        TextBoxSearch.Loaded += TextBox_Loaded;
        return;
        
        void TextBox_Loaded(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Loaded -= TextBox_Loaded;
                if (!textBox.Focus())
                {
                    System.Diagnostics.Debug.WriteLine("Something wrong happened while setting focus on search box.");
                }
            }
        }
    }

    private void PART_TextBoxSearch_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && e.Key == Key.Escape)
        {
            textBox.Clear();
        }
    }
}