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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Tabalonia.Controls;

namespace Caly.Core.Controls;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_LayoutTransformControl", typeof(LayoutTransformControl))]
public sealed class PdfPageItemsControl : ItemsControl
{
    private const double _zoomFactor = 1.1;

    private bool _isSettingPageVisibility = false;
    private bool _isZooming = false;
    private bool _isTabDragging = false;

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
    public static readonly DirectProperty<PdfPageItemsControl, LayoutTransformControl?> LayoutTransformControlProperty =
        AvaloniaProperty.RegisterDirect<PdfPageItemsControl, LayoutTransformControl?>(nameof(LayoutTransformControl), o => o.LayoutTransformControl);

    /// <summary>
    /// Defines the <see cref="PageCount"/> property.
    /// </summary>
    public static readonly StyledProperty<int> PageCountProperty = AvaloniaProperty.Register<PdfPageItemsControl, int>(nameof(PageCount));

    /// <summary>
    /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
    /// </summary>
    public static readonly StyledProperty<int?> SelectedPageIndexProperty = AvaloniaProperty.Register<PdfPageItemsControl, int?>(nameof(SelectedPageIndex), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="MinZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(MinZoomLevel));

    /// <summary>
    /// Defines the <see cref="MaxZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MaxZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(MaxZoomLevel), 1);

    /// <summary>
    /// Defines the <see cref="ZoomLevel"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfPageItemsControl, double>(nameof(ZoomLevel), 1, defaultBindingMode: BindingMode.TwoWay);

    private ScrollViewer? _scroll;
    private LayoutTransformControl? _layoutTransformControl;
    private TabsControl? _tabsControl;

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
    public LayoutTransformControl? LayoutTransformControl
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
    public int? SelectedPageIndex
    {
        get => GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    public double MinZoomLevel
    {
        get => GetValue(MinZoomLevelProperty);
        set => SetValue(MinZoomLevelProperty, value);
    }

    public double MaxZoomLevel
    {
        get => GetValue(MaxZoomLevelProperty);
        set => SetValue(MaxZoomLevelProperty, value);
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
    public PdfPageItem? GetPdfPageItem(int pageNumber)
    {
        System.Diagnostics.Debug.WriteLine($"GetPdfPageItem {pageNumber}.");
        if (ContainerFromIndex(pageNumber - 1) is PdfPageItem presenter)
        {
            return presenter;
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

        if (_isTabDragging || _isSettingPageVisibility ||
            container is not PdfPageItem cp ||
            item is not PdfPageViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine($"Skipping LoadPage() for page {index + 1} (IsSettingPageVisibility: {_isSettingPageVisibility}, IsTabDragging: {_isTabDragging})");
            return;
        }

        cp.PropertyChanged += _onContainerPropertyChanged;
        vm.VisibleArea = null;
        vm.LoadPage();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);

        if (container is not PdfPageItem cp)
        {
            return;
        }

        cp.PropertyChanged -= _onContainerPropertyChanged;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new PdfPageItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<PdfPageItem>(item, out recycleKey);
    }

    private static void _onContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ContentProperty &&
            e.OldValue is PdfPageViewModel vm)
        {
            vm.VisibleArea = null;
            vm.UnloadPage();
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

    public PdfPageItem? GetPdfPageItemOver(PointerEventArgs e)
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
            System.Diagnostics.Debug.WriteLine("GetPdfPageItemOver Quick reject.");
            return null;
        }

        int minPageIndex = GetMinPageIndex();
        int maxPageIndex = GetMaxPageIndex(); // Exclusive

        int startIndex = SelectedPageIndex.HasValue ? SelectedPageIndex.Value - 1 : 0; // Switch from one-indexed to zero-indexed

        bool isAfterSelectedPage = false;

        // Check selected current page
        if (ContainerFromIndex(startIndex) is PdfPageItem presenter)
        {
            System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {startIndex + 1}.");
            if (presenter.Bounds.Contains(point))
            {
                return presenter;
            }

            isAfterSelectedPage = point.Y > presenter.Bounds.Bottom;
        }

        if (isAfterSelectedPage)
        {
            // Start with checking forward
            for (int p = startIndex + 1; p < maxPageIndex; ++p)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {p + 1}.");
                if (ContainerFromIndex(p) is not PdfPageItem cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp;
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
                System.Diagnostics.Debug.WriteLine($"GetPdfPageItemOver page {p + 1}.");
                if (ContainerFromIndex(p) is not PdfPageItem cp)
                {
                    continue;
                }

                if (cp.Bounds.Contains(point))
                {
                    return cp;
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
        LayoutTransformControl = e.NameScope.FindFromNameScope<LayoutTransformControl>("PART_LayoutTransformControl");

        Scroll.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
        Scroll.AddHandler(SizeChangedEvent, (_, _) => SetPagesVisibility(), RoutingStrategies.Direct);
        Scroll.AddHandler(KeyDownEvent, _onKeyDownHandler);
        LayoutTransformControl.AddHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);

        _tabsControl = this.FindAncestorOfType<TabsControl>();
        if (_tabsControl is not null)
        {
            _tabsControl.OnTabDragStarted += TabControlOnTabDragStarted;
            _tabsControl.OnTabDragCompleted += TabControlOnTabDragCompleted;
        }

        if (CalyExtensions.IsMobilePlatform())
        {
            LayoutTransformControl.GestureRecognizers.Add(new PinchGestureRecognizer());
            Gestures.AddPinchHandler(LayoutTransformControl, _onPinchChangedHandler);
            Gestures.AddPinchEndedHandler(LayoutTransformControl, _onPinchEndedHandler);
            Gestures.AddHoldingHandler(LayoutTransformControl, _onHoldingChangedHandler);
        }
    }

    private void TabControlOnTabDragStarted(object? sender, Tabalonia.Events.DragTabDragStartedEventArgs e)
    {
        _isTabDragging = true;
    }

    private void TabControlOnTabDragCompleted(object? sender, Tabalonia.Events.DragTabDragCompletedEventArgs e)
    {
        _isTabDragging = false;
        foreach (Control cp in this.GetRealizedContainers())
        {
            if (cp.DataContext is PdfPageViewModel vm)
            {
                cp.PropertyChanged += _onContainerPropertyChanged;
                vm.VisibleArea = null;
                vm.LoadPage();
            }
        }
        SetPagesVisibility();
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

            // Ensure the pages visibility is set when OnApplyTemplate()
            // is not called, i.e. when a new document is opened but the
            // page has exactly the same dimension of the visible page
            SetPagesVisibility();
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

        if (_tabsControl is not null)
        {
            _tabsControl.OnTabDragStarted -= TabControlOnTabDragStarted;
            _tabsControl.OnTabDragCompleted -= TabControlOnTabDragCompleted;
        }

        if (CalyExtensions.IsMobilePlatform() && LayoutTransformControl is not null)
        {
            Gestures.RemovePinchHandler(LayoutTransformControl, _onPinchChangedHandler);
            Gestures.RemovePinchEndedHandler(LayoutTransformControl, _onPinchEndedHandler);
            LayoutTransformControl.RemoveHandler(Gestures.HoldingEvent, _onHoldingChangedHandler);
            //Gestures.RemoveHoldingHandler(LayoutTransformControl, _onHoldingChangedHandler);
        }
    }

    private void SetPagesVisibility()
    {
        if (_isSettingPageVisibility || _isTabDragging)
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

            if (ContainerFromIndex(p) is not PdfPageItem { Content: PdfPageViewModel vm } cp)
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
        int startIndex = SelectedPageIndex.HasValue ? SelectedPageIndex.Value - 1 : 0; // Switch from one-indexed to zero-indexed
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
            try
            {
                _isSettingPageVisibility = true;
                SetCurrentValue(SelectedPageIndexProperty, indexMaxOverlap);
            }
            finally
            {
                _isSettingPageVisibility = false;
            }
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

    #region Mobile handling

    private void _onHoldingChangedHandler(object? sender, HoldingRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Holding {e.HoldingState}: {e.Position.X}, {e.Position.Y}");
    }

    private double _pinchZoomReference = 1.0;
    private void _onPinchEndedHandler(object? sender, PinchEndedEventArgs e)
    {
        _pinchZoomReference = ZoomLevel;
    }

    private void _onPinchChangedHandler(object? sender, PinchEventArgs e)
    {
        if (e.Scale != 0)
        {
            ZoomTo(e);
            e.Handled = true;
        }
    }

    private void ZoomTo(PinchEventArgs e)
    {
        if (LayoutTransformControl is null)
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

            // Pinch zoom always starts with a scale of 1, then increase/decrease until PinchEnded
            double dZoom = (e.Scale * _pinchZoomReference) / ZoomLevel;

            // TODO - Origin still not correct
            var point = LayoutTransformControl.PointToClient(new PixelPoint((int)e.ScaleOrigin.X, (int)e.ScaleOrigin.Y));
            ZoomToInternal(dZoom, point);
            SetCurrentValue(ZoomLevelProperty, LayoutTransformControl.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }
    #endregion

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
        if (LayoutTransformControl is null)
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
            double dZoom = Math.Round(Math.Pow(_zoomFactor, e.Delta.Y), 4); // If IsScrollInertiaEnabled = false, Y is only 1 or -1
            ZoomToInternal(dZoom, e.GetPosition(LayoutTransformControl));
            SetCurrentValue(ZoomLevelProperty, LayoutTransformControl.LayoutTransform?.Value.M11);
        }
        finally
        {
            _isZooming = false;
        }
    }

    internal void ZoomTo(double dZoom, Point point)
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
            ZoomToInternal(dZoom, point);
        }
        finally
        {
            _isZooming = false;
        }
    }

    private void ZoomToInternal(double dZoom, Point point)
    {
        if (LayoutTransformControl is null || Scroll is null)
        {
            return;
        }

        double oldZoom = LayoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0;
        double newZoom = oldZoom * dZoom;

        if (newZoom < MinZoomLevel)
        {
            if (oldZoom.Equals(MinZoomLevel))
            {
                return;
            }

            newZoom = MinZoomLevel;
            dZoom = newZoom / oldZoom;
        }
        else if (newZoom > MaxZoomLevel)
        {
            if (oldZoom.Equals(MaxZoomLevel))
            {
                return;
            }

            newZoom = MaxZoomLevel;
            dZoom = newZoom / oldZoom;
        }

        var builder = TransformOperations.CreateBuilder(1);
        builder.AppendScale(newZoom, newZoom);
        LayoutTransformControl.LayoutTransform = builder.Build();

        var offset = Scroll.Offset - GetOffset(dZoom, point.X, point.Y);
        if (newZoom > oldZoom)
        {
            // When zooming-in, we need to re-arrange the scroll viewer
            Scroll.Measure(Size.Infinity);
            Scroll.Arrange(new Rect(Scroll.DesiredSize));
        }

        Scroll.SetCurrentValue(ScrollViewer.OffsetProperty, offset);
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
        int currentPage = SelectedPageIndex.HasValue ? SelectedPageIndex.Value - 1 : 0;

        try
        {
            _isSettingPageVisibility = true;

            // There's a bug in VirtualizingStackPanel. Scroll bars do not display correctly
            // This hack fixes that by scrolling into view a page that's not realised
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
            }
        }
        finally
        {
            _isSettingPageVisibility = false;

            ScrollIntoView(currentPage);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty && change.OldValue is PdfDocumentViewModel oldVm)
        {
            oldVm.ClearAllPagePictures();
        }
    }
}