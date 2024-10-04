using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

public partial class SearchPanelControl : UserControl
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
        var textBox = this.FindLogicalDescendantOfType<TextBox>();

        if (textBox?.Name == "PART_TextBoxSearch")
        {
            textBox.Loaded += TextBox_Loaded;
        }
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