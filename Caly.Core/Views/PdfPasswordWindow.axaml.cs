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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Caly.Core.Views
{
    public sealed partial class PdfPasswordWindow : Window
    {
        private TextBox _textBoxPassword;

        public PdfPasswordWindow()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _textBoxPassword = this.Find<TextBox>("PART_TextBoxPassword")!;
            ArgumentNullException.ThrowIfNull(_textBoxPassword, nameof(_textBoxPassword));
            _textBoxPassword.Loaded += TextBox_Loaded;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void OkButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxPassword?.Text))
            {
                Close(_textBoxPassword.Text);
            }
        }

        private static void TextBox_Loaded(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            textBox.Loaded -= TextBox_Loaded;

            if (!textBox.Focus())
            {
                System.Diagnostics.Debug.WriteLine("Something wrong happened while setting focus on password box.");
            }
        }
    }
}
