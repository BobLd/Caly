using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Caly.Core.Views
{
    public partial class PdfPasswordWindow : Window
    {
        public PdfPasswordWindow()
        {
            InitializeComponent();
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
            var textBox = this.Find<TextBox>("PasswordTextBox");
            if (!string.IsNullOrEmpty(textBox?.Text))
            {
                Close(textBox.Text);
            }
        }
    }
}
