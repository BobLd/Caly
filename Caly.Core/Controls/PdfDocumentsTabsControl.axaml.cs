using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Caly.Core.Controls;

public partial class PdfDocumentsTabsControl : UserControl
{
    private static readonly Cursor SizeWestEastCursor = new(StandardCursorType.SizeWestEast);

    private const int MaxPaneLength = 500;
    private const int MinPaneLength = 200;

    private Point? _lastPoint;
    private double _originalPaneLength;

    private SplitView? _splitView;

    public PdfDocumentsTabsControl()
    {
        InitializeComponent();
    }

    private void TreeView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || sender is not TreeView treeView)
        {
            return;
        }

        double width = treeView.Bounds.Width;

        foreach (var control in treeView.GetRealizedContainers().OfType<TreeViewItem>())
        {
            var stackPanel = control.GetVisualChildren().OfType<StackPanel>().FirstOrDefault();
            if (stackPanel is null)
            {
                continue;
            }

            stackPanel.SetCurrentValue(WidthProperty, width);

            foreach (TextBlock textBlock in stackPanel.GetVisualDescendants().OfType<TextBlock>())
            {
                textBlock.InvalidateMeasure();
            }
        }
    }

    #region Resize SplitView.Pane
    private void Rectangle_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Rectangle)
        {
            Cursor = SizeWestEastCursor;
        }
    }

    private void Rectangle_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Rectangle)
        {
            Cursor = Cursor.Default;
        }
    }

    private void Rectangle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Rectangle rect)
        {
            return;
        }

        if (_splitView is null)
        {
            _splitView = rect.FindAncestorOfType<SplitView>();
            if (_splitView is null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_splitView.Name) || !_splitView.Name.Equals("PART_SplitView"))
            {
                throw new Exception("The found split view does not have the correct name.");
            }
        }

        if (!_splitView.IsPaneOpen)
        {
            return;
        }

        _lastPoint = e.GetPosition(null);
        _originalPaneLength = _splitView.OpenPaneLength;
        e.Handled = true;
        e.PreventGestureRecognition();
    }

    private void Rectangle_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_lastPoint.HasValue || sender is not Rectangle)
        {
            return;
        }

        if (_splitView is null || !_splitView.IsPaneOpen)
        {
            return;
        }

        Point mouseMovement = (e.GetPosition(null) - _lastPoint).Value;
        _splitView.OpenPaneLength = Math.Max(Math.Min(_originalPaneLength + mouseMovement.X, MaxPaneLength), MinPaneLength);
    }

    private void Rectangle_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_lastPoint.HasValue || sender is not Rectangle)
        {
            return;
        }

        _lastPoint = null;
        _originalPaneLength = 0;
        e.Handled = true;
    }
    #endregion
}