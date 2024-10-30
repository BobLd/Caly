using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

[TemplatePart("PART_TextBoxSearch", typeof(TextBox))]
public class PdfSearchPanelControl : TemplatedControl
{
    private TextBox? _textBoxSearch;

    public PdfSearchPanelControl()
    {
#if DEBUG
        if (Design.IsDesignMode)
        {
            DataContext = new PdfDocumentViewModel(null);
        }
#endif
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _textBoxSearch = e.NameScope.FindFromNameScope<TextBox>("PART_TextBoxSearch");
        _textBoxSearch.KeyDown += TextBoxSearch_OnKeyDown;
        _textBoxSearch.Loaded += TextBox_Loaded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_textBoxSearch is not null)
        {
            _textBoxSearch.Loaded += TextBox_Loaded;
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (_textBoxSearch is not null)
        {
            _textBoxSearch.KeyDown -= TextBoxSearch_OnKeyDown;
            _textBoxSearch.Loaded -= TextBox_Loaded;
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
            System.Diagnostics.Debug.WriteLine("Something wrong happened while setting focus on search box.");
        }
    }

    private static void TextBoxSearch_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && e.Key == Key.Escape)
        {
            textBox.Clear();
        }
    }
}