using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_LayoutTransformControl", typeof(CalyLayoutTransformControl))]
public class PdfPageItemsControl : ItemsControl
{
    private const double _zoomFactor = 1.1;

    /*
     * See PDF Reference 1.7 - C.2 Architectural limits
     * The magnification factor of a view should be constrained to be between approximately 8 percent and 6400 percent.
     */
    private const double _minZoom = 0.08;
    private const double _maxZoom = 64;

    private bool _isSettingPageVisibility = false;
    private bool _isZooming = false;

    /// <summary>
    /// The default value for the <see cref="ItemsControl.ItemsPanel"/> property.
    /// </summary>
    private static readonly FuncTemplate<Panel?> DefaultPanel = new(() => new VirtualizingStackPanel());

    /// <summary>
    /// Defines the <see cref="Scroll"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, ScrollViewer?> ScrollProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, ScrollViewer?>(nameof(Scroll), o => o.Scroll);

    /// <summary>
    /// Defines the <see cref="LayoutTransformControl"/> property.
    /// </summary>
    public static readonly DirectProperty<PdfPageItemsControl, CalyLayoutTransformControl?> LayoutTransformControlProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, CalyLayoutTransformControl?>(nameof(LayoutTransformControl), o => o.LayoutTransformControl);

    /// <summary>
    /// Defines the <see cref="PageCount"/> property.
    /// </summary>
    public static readonly StyledProperty<int> PageCountProperty = AvaloniaProperty.Register<PdfPageItemsControl, int>(nameof(PageCount));

    /// <summary>
    /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<int> SelectedPageIndexProperty = AvaloniaProperty.Register<PdfPageItemsControl, int>(nameof(SelectedPageIndex), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="ZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(ZoomLevel), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private ScrollViewer? _scroll;
    private CalyLayoutTransformControl? _layoutTransformControl;

    static PdfPageItemsControl()
    {
        ItemsPanelProperty.OverrideDefaultValue<PdfPageItemsControl>(DefaultPanel);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue(typeof(PdfPageItemsControl),
            KeyboardNavigationMode.Once);
    }

    /// <summary>
    /// Gets the scroll information for the <see cref="ListBox"/>.
    /// </summary>
    public ScrollViewer? Scroll
    {
        get => _scroll;
        private set => SetAndRaise(ScrollProperty, ref _scroll, value);
    }

    /// <summary>
    /// Gets the scroll information for the <see cref="ListBox"/>.
    /// </summary>
    public CalyLayoutTransformControl? LayoutTransformControl
    {
        get => _layoutTransformControl;
        private set => SetAndRaise(LayoutTransformControlProperty, ref _layoutTransformControl, value);
    }

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    /// <summary>
    /// Starts at 1.
    /// </summary>
    public int SelectedPageIndex
    {
        get => GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// Get the page control for the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    /// <returns>The page control, or <c>null</c> if not found.</returns>
    public PdfPageControl? GetPdfPageControl(int pageNumber)
    {
        System.Diagnostics.Debug.WriteLine($"GetPdfPageControl {pageNumber}.");
        if (ContainerFromIndex(pageNumber - 1) is ContentPresenter presenter)
        {
            return presenter.GetVisualDescendants().OfType<PdfPageControl>().SingleOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Scrolls to the page number.
    /// </summary>
    /// <param name="pageNumber">The page number. Starts at 1.</param>
    public void GoToPage(int pageNumber)
    {
        if (_isSettingPageVisibility || pageNumber <= 0 || pageNumber > PageCount ||  ItemsView.Count == 0)
        {
            return;
        }

        ScrollIntoView(pageNumber - 1);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (container is not ContentPresenter cp || item is not PdfPageViewModel vm)
        {
            return;
        }

        cp.PropertyChanged += _onContainerPropertyChanged;
        vm.VisibleArea = null;
        vm.IsPagePrepared = true;
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);

        if (container is not ContentPresenter cp)
        {
            return;
        }

        cp.PropertyChanged -= _onContainerPropertyChanged;
    }

    private static void _onContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ContentProperty && e.OldValue is PdfPageViewModel vm)
        {
            vm.VisibleArea = null;
            vm.IsPagePrepared = false;
        }
    }

    /// <summary>
    /// Starts at 0. Inclusive.
    /// </summary>
    private int GetMinPageIndex()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel v)
        {
            return Math.Max(0, v.FirstRealizedIndex);
        }
        return 0;
    }

    /// <summary>
    /// Starts at 0. Exclusive.
    /// </summary>
    private int GetMaxPageIndex()
    {
        if (ItemsPanelRoot is VirtualizingStackPanel v && v.LastRealizedIndex != -1)
        {
            return Math.Min(PageCount, v.LastRealizedIndex + 1);
        }

        return PageCount;
    }

    public PdfPageControl? GetPdfPageControlOver(PointerEventArgs e)
    {
        if (Presenter is null)
        {
            // Should never happen
            return null;
        }

        Point point = e.GetPosition(Presenter);

        // Quick reject
        if (!Presenter.Bounds.Contains(point))
        {
            System.Diagnostics.Debug.WriteLine("GetPdfPageControlOver Quick reject.");
            return null;
        }

        int minPageIndex = GetMinPageIndex();
        int maxPageIndex = GetMaxPageIndex(); // Exclusive

        int startIndex = SelectedPageIndex - 1; // Switch from one-indexed to zero-indexed

        bool isAfterSelectedPage = false;

        // Check selected current page
        if (ContainerFromIndex(startIndex) is ContentPresenter presenter)
        {
            System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {startIndex + 1}.");
            if (presenter.Bounds.Contains(point))
            {
                return presenter.GetVisualDescendants().OfType<PdfPageControl>().SingleOrDefault();
            }

            isAfterSelectedPage = point.Y > presenter.Bounds.Bottom;
        }

        if (isAfterSelectedPage)
        {
            // Start with checking forward
            for (int p = startIndex + 1; p < maxPageIndex; ++p)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                if (ContainerFromIndex(p) is not ContentPresenter cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp.GetVisualDescendants().OfType<PdfPageControl>().SingleOrDefault();
                }

                if (point.Y < cp.Bounds.Top)
                {
                    return null;
                }
            }
        }
        else
        {
            // Continue with checking backward
            for (int p = startIndex - 1; p >= minPageIndex; --p)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                if (ContainerFromIndex(p) is not ContentPresenter cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp.GetVisualDescendants().OfType<PdfPageControl>().SingleOrDefault();
                }

                if (point.Y > cp.Bounds.Bottom)
                {
                    return null;
                }
            }
        }

        return null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Scroll = e.NameScope.FindFromNameScope<ScrollViewer>("PART_ScrollViewer");
        LayoutTransformControl = e.NameScope.FindFromNameScope<CalyLayoutTransformControl>("PART_LayoutTransformControl");

        Scroll.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll.AddHandler(SizeChangedEvent, (_, _) => SetPagesVisibility(), RoutingStrategies.Direct);
        Scroll.AddHandler(KeyDownEvent, _onKeyDownHandler);
        LayoutTransformControl.AddHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EnsureScrollBars();
        ItemsPanelRoot!.DataContextChanged += ItemsPanelRoot_DataContextChanged;
    }

    private void ItemsPanelRoot_DataContextChanged(object? sender, EventArgs e)
    {
        void ExecuteScrollWhenLayoutUpdated(object? sender, EventArgs e)
        {
            LayoutUpdated -= ExecuteScrollWhenLayoutUpdated;
            EnsureScrollBars();
        }

        LayoutUpdated += ExecuteScrollWhenLayoutUpdated;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);

        Scroll?.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll?.RemoveHandler(SizeChangedEvent, (_, _) => SetPagesVisibility());
        Scroll?.RemoveHandler(KeyDownEvent, _onKeyDownHandler);
        LayoutTransformControl?.RemoveHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);
        ItemsPanelRoot!.DataContextChanged -= ItemsPanelRoot_DataContextChanged;
    }

    private void SetPagesVisibility()
    {
        if (_isSettingPageVisibility)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        if (LayoutTransformControl is null || Scroll is null || Scroll.Viewport.IsEmpty() || ItemsView.Count == 0)
        {
            return;
        }

        Debug.AssertIsNullOrScale(LayoutTransformControl.LayoutTransform?.Value);

        double invScale = 1.0 / (LayoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0);
        Matrix fastInverse = Matrix.CreateScale(invScale, invScale);

        Rect viewPort = new Rect((Point)Scroll.Offset, Scroll.Viewport).TransformToAABB(fastInverse);

        // Use the following: not visible pages cannot be between visible pages
        // We cannot have:
        // nv - v - v - nv - v - nv
        // We will always have:
        // nv - v - v - v - nv - nv
        //
        // There are 3 possible splits:
        // [nv] | [v] [nv]
        // [nv] [v] | [nv]
        //
        // [nv] | [nv] [v] [nv]
        // [nv] [v] [nv] | [nv]
        //
        // [nv] [v] | [v] [nv]

        bool isPreviousPageVisible = false;
        bool needMoreChecks = true;

        double maxOverlap = double.MinValue;
        int indexMaxOverlap = -1;

        bool CheckPageVisibility(int p, out bool isPageVisible)
        {
            isPageVisible = false;

            if (ContainerFromIndex(p) is not ContentPresenter { Content: PdfPageViewModel vm } cp)
            {
                // Page is not realised
                return !isPreviousPageVisible;
            }

            if (!needMoreChecks || cp.Bounds.IsEmpty())
            {
                if (!vm.IsPageVisible)
                {
                    // Page is not visible and no need for more checks.
                    // All following pages are already set to IsPageVisible = false
                    return false;
                }

                vm.VisibleArea = null;
                return true;
            }

            Rect view = cp.Bounds;

            if (view.Height == 0)
            {
                // No need for further checks, not visible
                vm.VisibleArea = null;
                return true;
            }

            double top = view.Top;
            double left = view.Left;
            double bottom = view.Bottom;

            // Quick check if height overlap
            if (OverlapsHeight(viewPort.Top, viewPort.Bottom, top, bottom))
            {
                // Compute overlap
                view = view.Intersect(viewPort);

                double overlapArea = view.Height * view.Width;

                // Actual check if page is visible
                if (overlapArea == 0)
                {
                    vm.VisibleArea = null;
                    // If previous page was visible but current page is not, we have the last visible page
                    needMoreChecks = !isPreviousPageVisible;
                    return true;
                }

                System.Diagnostics.Debug.Assert(view.Height.Equals(Overlap(viewPort.Top, viewPort.Bottom, top, bottom)));

                if (overlapArea > maxOverlap)
                {
                    maxOverlap = overlapArea;
                    indexMaxOverlap = p;
                }

                isPreviousPageVisible = true;
                isPageVisible = true;

                // Set overlap area (Translate and inverse transform)
                vm.VisibleArea = view.Translate(new Vector(-left, -top));

                return true;
            }

            vm.VisibleArea = null;
            // If previous page was visible but current page is not, we have the last visible page
            needMoreChecks = !isPreviousPageVisible;
            return true;
        }

        // Check current page visibility
        int startIndex = SelectedPageIndex - 1; // Switch from one-indexed to zero-indexed
        CheckPageVisibility(startIndex, out bool isSelectedPageVisible);

        int minPageIndex = GetMinPageIndex();
        int maxPageIndex = GetMaxPageIndex(); // Exclusive

        // Start with checking forward.
        // TODO - While scrolling down, the current selected page can become invisible and force
        // a full iteration if starting backward
        isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
        int forwardIndex = startIndex + 1;
        while (forwardIndex < maxPageIndex && CheckPageVisibility(forwardIndex, out _))
        {
            forwardIndex++;
        }

        // Continue with checking backward
        isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
        needMoreChecks = true;
        int backwardIndex = startIndex - 1;
        while (backwardIndex >= minPageIndex && CheckPageVisibility(backwardIndex, out _))
        {
            backwardIndex--;
        }

        indexMaxOverlap++; // Switch to base 1 indexing

        if (indexMaxOverlap != -1 && SelectedPageIndex != indexMaxOverlap)
        {
            _isSettingPageVisibility = true;
            SetCurrentValue(SelectedPageIndexProperty, indexMaxOverlap);
            _isSettingPageVisibility = false;
        }
    }

    private static double Overlap(double top1, double bottom1, double top2, double bottom2)
    {
        return Math.Max(0, Math.Min(bottom1, bottom2) - Math.Max(top1, top2));
    }

    /// <summary>
    /// Works for vertical scrolling.
    /// </summary>
    private static bool OverlapsHeight(double top1, double bottom1, double top2, double bottom2)
    {
        return !(top1 > bottom2 || bottom1 < top2);
    }

    private void _onKeyDownHandler(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
                Scroll!.PageDown();
                break;
            case Key.Down:
                Scroll!.LineDown();
                break;
            case Key.Left:
                Scroll!.PageUp();
                break;
            case Key.Up:
                Scroll!.LineUp();
                break;
        }
    }

    private void _onPointerWheelChangedHandler(object? sender, PointerWheelEventArgs e)
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        var ctrl = hotkeys is not null && e.KeyModifiers.HasFlag(hotkeys.CommandModifiers);

        if (ctrl && e.Delta.Y != 0)
        {
            ZoomTo(e);
            e.Handled = true;
        }
    }

    private void ZoomTo(PointerWheelEventArgs e)
    {
        if (LayoutTransformControl is null || Scroll is null)
        {
            return;
        }

        if (_isZooming)
        {
            return;
        }

        try
        {
            _isZooming = true;

            double delta = e.Delta.Y;
            double dZoom = Math.Round(Math.Pow(_zoomFactor, delta), 4); // If IsScrollInertiaEnabled = false, Y is only 1 or -1
            double newZoom = (LayoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0) * dZoom;

            if (newZoom < _minZoom || newZoom > _maxZoom)
            {
                return;
            }

            var builder = TransformOperations.CreateBuilder(1);
            builder.AppendScale(newZoom, newZoom);
            LayoutTransformControl.LayoutTransform = builder.Build();

            Point point = e.GetPosition(LayoutTransformControl);

            var offset = Scroll.Offset - GetOffset(dZoom, point.X, point.Y);
            if (delta > 0)
            {
                // When zooming-in, we need to re-arrange the scroll viewer
                Scroll.Measure(Size.Infinity);
                Scroll.Arrange(new Rect(Scroll.DesiredSize));
            }

            Scroll.SetCurrentValue(ScrollViewer.OffsetProperty, offset);

            SetCurrentValue(ZoomLevelProperty, newZoom);
        }
        finally
        {
            _isZooming = false;
        }
    }

    private static Vector GetOffset(double scale, double x, double y)
    {
        double s = 1 - scale;
        return new Vector(x * s, y * s);
    }

    /// <summary>
    /// Ensure the scroll bars are correctly set.
    /// </summary>
    private void EnsureScrollBars()
    {
        // There's a bug in VirtualizingStackPanel. Scroll bars do not display correctly
        // This hack fixes that by scrolling into view a page that's not realised
        int currentPage = SelectedPageIndex - 1;
        if (currentPage >= GetMinPageIndex() && currentPage <= GetMaxPageIndex())
        {
            // Current page is realised
            if (currentPage != 0)
            {
                ScrollIntoView(0);
            }
            else if (currentPage != PageCount - 1)
            {
                ScrollIntoView(PageCount - 1);
            }

            ScrollIntoView(currentPage);
        }
        else
        {
            ScrollIntoView(currentPage);
        }
    }
}