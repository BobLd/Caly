using Avalonia;
using Avalonia.Controls.Primitives;

namespace Caly.Core.Controls;

public class PdfDocumentPropertiesControl : TemplatedControl
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