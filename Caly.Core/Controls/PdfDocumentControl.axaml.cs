using System;
using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Handlers;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ScrollViewer", typeof(IScrollable))]
    [TemplatePart("PART_LayoutTransformControl", typeof(LayoutTransformControl))]
    [TemplatePart("PART_ItemsRepeater", typeof(ItemsRepeater))]
    public class PdfDocumentControl : CalyTemplatedControl
    {
        private const double _zoomFactor = 1.1;

        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * The magnification factor of a view should be constrained to be between approximately 8 percent and 6400 percent.
         */
        private const double _minZoom = 0.08;
        private const double _maxZoom = 64;

        private ScrollViewer? _scrollViewer;
        private LayoutTransformControl? _layoutTransformControl;
        private ItemsRepeater? _itemsRepeater;

        private bool _isCheckingPageVisibility = false;
        private bool _isZooming = false;
        private Matrix _layoutTransformMatrix = Matrix.Identity;
        private Matrix _layoutTransformInverseMatrix = Matrix.Identity;

        internal ITextSelectionHandler? TextSelectionHandler;

        /// <summary>
        /// Defines the <see cref="ItemsSource"/> property.
        /// </summary>
        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<PdfDocumentControl, IEnumerable?>(nameof(ItemsSource));

        /// <summary>
        /// Defines the <see cref="PageCount"/> property.
        /// </summary>
        public static readonly StyledProperty<int> PageCountProperty = AvaloniaProperty.Register<PdfDocumentControl, int>(nameof(PageCount), 0);

        /// <summary>
        /// Defines the <see cref="ZoomLevel"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfDocumentControl, double>(nameof(ZoomLevel), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
        /// </summary>
        public static readonly StyledProperty<int> SelectedPageIndexProperty = AvaloniaProperty.Register<PdfDocumentControl, int>(nameof(SelectedPageIndex), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedBookmark"/> property.
        /// </summary>
        public static readonly StyledProperty<PdfBookmarkNode?> SelectedBookmarkProperty = AvaloniaProperty.Register<PdfDocumentControl, PdfBookmarkNode?>(nameof(SelectedBookmark), null);

        /// <summary>
        /// Defines the <see cref="PdfTextSelection"/> property.
        /// </summary>
        public static readonly StyledProperty<PdfTextSelection?> PdfTextSelectionProperty = AvaloniaProperty.Register<PdfDocumentControl, PdfTextSelection?>(nameof(PdfTextSelection));

        public int PageCount
        {
            get => GetValue(PageCountProperty);
            set => SetValue(PageCountProperty, value);
        }

        /// <summary>
        /// TODO
        /// </summary>
        public double ZoomLevel
        {
            get => GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, value);
        }

        /// <summary>
        /// Starts at 1.
        /// </summary>
        public int SelectedPageIndex
        {
            get => GetValue(SelectedPageIndexProperty);
            set => SetValue(SelectedPageIndexProperty, value);
        }

        public PdfBookmarkNode? SelectedBookmark
        {
            get => GetValue(SelectedBookmarkProperty);
            set => SetValue(SelectedBookmarkProperty, value);
        }

        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public PdfTextSelection? PdfTextSelection
        {
            get => GetValue(PdfTextSelectionProperty);
            set => SetValue(PdfTextSelectionProperty, value);
        }

        public PdfDocumentControl()
        {
#if DEBUG
            if (Design.IsDesignMode)
            {
                // Only if in design mode
                DataContext = new PdfDocumentViewModel(null);
            }
#endif
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedPageIndexProperty)
            {
                GoToPage(SelectedPageIndex);
            }
            else if (change.Property == SelectedBookmarkProperty)
            {
                if (SelectedBookmark?.PageNumber.HasValue == true && SelectedBookmark.PageNumber.Value != SelectedPageIndex)
                {
                    GoToPage(SelectedBookmark.PageNumber.Value);
                }
            }
            else if (change.Property == PdfTextSelectionProperty && change.NewValue is PdfTextSelection selection)
            {
                TextSelectionHandler = new TextSelectionHandler(selection);
                // We have a new TextSelectionHandler, we need to update the PdfPageTextLayerControls (virtualisation)
                foreach (var textLayer in this.GetVisualDescendants().OfType<PdfPageTextLayerControl>())
                {
                    // The text layer is already attached, we update the TextSelectionHandler
                    textLayer.AttachTextSelectionHandler(TextSelectionHandler);
                }
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _scrollViewer = e.NameScope.FindFromNameScope<ScrollViewer>("PART_ScrollViewer");
            _layoutTransformControl = e.NameScope.FindFromNameScope<LayoutTransformControl>("PART_LayoutTransformControl");
            _itemsRepeater = e.NameScope.FindFromNameScope<ItemsRepeater>("PART_ItemsRepeater");

            _scrollViewer.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) =>
            {
                System.Diagnostics.Debug.WriteLine("ScrollViewer.ScrollChangedEvent");
                CheckPagesVisibility();
            });
            _scrollViewer.AddHandler(SizeChangedEvent, (_, _) =>
            {
                System.Diagnostics.Debug.WriteLine("SizeChangedEvent");
                CheckPagesVisibility();
            }, RoutingStrategies.Direct);
            _scrollViewer.AddHandler(KeyDownEvent, _onKeyDownHandler);
            _layoutTransformControl.AddHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);
            _layoutTransformControl.PropertyChanged += _layoutTransformControl_PropertyChanged;
            _itemsRepeater.ElementPrepared += _onElementPrepared;
            _itemsRepeater.ElementClearing += _onElementClearing;

#if DEBUG
            _itemsRepeater.ElementIndexChanged += (s, e) =>
            {
                if (e.Element is PdfPageControl pageControl && pageControl.DataContext is PdfPageViewModel vm)
                {
                    System.Diagnostics.Debug.WriteLine($"ElementIndexChanged: {vm.PageNumber}");
                }
            };
#endif
        }

        private void _layoutTransformControl_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == LayoutTransformControl.LayoutTransformProperty)
            {
                if (e.NewValue is TransformOperations tos)
                {
                    if (tos.Value.IsIdentity)
                    {
                        _layoutTransformMatrix = Matrix.Identity;
                        _layoutTransformInverseMatrix = Matrix.Identity;
                    }
                    else
                    {
                        _layoutTransformMatrix = tos.Value;

                        if (!_layoutTransformMatrix.TryInvert(out _layoutTransformInverseMatrix))
                        {
                            throw new Exception("Cannot invert matrix");
                        }
                    }
                }
                else if (e.NewValue is null)
                {
                    _layoutTransformMatrix = Matrix.Identity;
                    _layoutTransformInverseMatrix = Matrix.Identity;
                }
            }
        }

        private void _onElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
        {
            if (e.Element is PdfPageControl pageControl)
            {
                pageControl.SetCurrentValue(PdfPageControl.IsPagePreparedProperty, true);
            }
        }

        private void _onElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
        {
            if (e.Element is PdfPageControl pageControl)
            {
                pageControl.SetCurrentValue(PdfPageControl.VisibleAreaProperty, null);
                pageControl.SetCurrentValue(PdfPageControl.IsPagePreparedProperty, false);
            }
        }

        private void _onKeyDownHandler(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                    _scrollViewer!.PageDown();
                    break;
                case Key.Down:
                    _scrollViewer!.LineDown();
                    break;
                case Key.Left:
                    _scrollViewer!.PageUp();
                    break;
                case Key.Up:
                    _scrollViewer!.LineUp();
                    break;
            }
        }

        private void _onPointerWheelChangedHandler(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.Control || e.Delta.Y == 0)
            {
                return;
            }

            ZoomTo(e);
            e.Handled = true;
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);

            _scrollViewer?.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => CheckPagesVisibility());
            _scrollViewer?.RemoveHandler(SizeChangedEvent, (_, _) => CheckPagesVisibility());
            _scrollViewer?.RemoveHandler(KeyDownEvent, _onKeyDownHandler);

            if (_layoutTransformControl is not null)
            {
                _layoutTransformControl.RemoveHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);
                _layoutTransformControl.PropertyChanged -= _layoutTransformControl_PropertyChanged;
            }

            if (_itemsRepeater is not null)
            {
                _itemsRepeater.ElementClearing -= _onElementClearing;
                _itemsRepeater.ElementPrepared -= _onElementPrepared;
            }
        }

        /// <summary>
        /// Scrolls the content downward by one page.
        /// </summary>
        public void GoToPage(int number)
        {
            if (_isCheckingPageVisibility || number <= 0 || number > PageCount || _scrollViewer is null ||
                _scrollViewer.Viewport.Height == 0 || _scrollViewer.Viewport.Width == 0 || _itemsRepeater is null)
            {
                return;
            }

            if (_itemsRepeater.ItemsSourceView == null || _itemsRepeater.ItemsSourceView.Count == 0)
            {
                // TODO - Why is this happening?
                return;
            }

            System.Diagnostics.Debug.WriteLine($"GoTo page {number:0000}");

            // TODO - Update the below once this PR is merged
            // https://github.com/AvaloniaUI/Avalonia/pull/14538

            // https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/ItemsRepeaterPage.xaml.cs
            var element = _itemsRepeater.GetOrCreateElement(number - 1);
            ((TopLevel)VisualRoot!).UpdateLayout();
            element.BringIntoView();
        }

        private void ZoomTo(PointerWheelEventArgs e)
        {
            if (_layoutTransformControl is null || _scrollViewer is null)
            {
                return;
            }

            if (_isZooming)
            {
                // TODO - Why is this happening?
                return;
            }

            try
            {
                _isZooming = true;
                TransformOperations.Builder builder = TransformOperations.CreateBuilder(2);
                builder.AppendMatrix(_layoutTransformMatrix);

                double scale = Math.Round(Math.Pow(_zoomFactor, e.Delta.Y), 4); // If IsScrollInertiaEnabled = false, Y is only 1 or -1
                builder.AppendScale(scale, scale);
                var newTransform = builder.Build();

                if (newTransform.Value.M11 < _minZoom || newTransform.Value.M11 > _maxZoom)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Start Zoom to {newTransform.Value.M11}");

                _layoutTransformControl.LayoutTransform = newTransform;

                Point point = e.GetPosition(_layoutTransformControl);

                if (e.Delta.Y > 0)
                {
                    // When zooming-in, we update the scroll viewer to have its updated offset before updating it
                    _scrollViewer.UpdateLayout(); // TODO - This needs to be optimised, see below
                    // - Triggers CheckPagesVisibility() (now handled with _isZooming flag)
                    // - Also triggers the following:
                    //  - SKDrawableControl.OnPropertyChanged: Picture
                    //  - SKDrawableControl.OnPropertyChanged: DataContext
                    //  - SKDrawableControl.OnPropertyChanged: Bounds

                    /*
                       -> Logs WITHOUT _scrollViewer.UpdateLayout();
                           Start Zoom to 2.8550289252639196
                           End Zoom to 2.8550289252639196
                           ScrollViewer.ScrollChangedEvent
                           CheckPagesVisibility.
                           Start Render Page
                           End Render Page

                       -> Logs WITH _scrollViewer.UpdateLayout();
                           Start Zoom to 3.1405318177903117
                           ElementClearing: 13
                           SKDrawableControl.OnPropertyChanged: Picture
                           SKDrawableControl.OnPropertyChanged: DataContext
                           SKDrawableControl.OnPropertyChanged: Bounds
                           ScrollViewer.ScrollChangedEvent
                           End Zoom to 3.1405318177903117
                           SKDrawableControl.OnPropertyChanged: DataContext
                           ElementPrepared: 13
                           SKDrawableControl.OnPropertyChanged: Bounds
                           ScrollViewer.ScrollChangedEvent
                           CheckPagesVisibility.
                           Start Render Page
                           End Render Page
                           LoadPagePicture 13
                           SKDrawableControl.OnPropertyChanged: Picture
                           SKDrawableControl.OnPropertyChanged: Bounds
                    */
                }

                _scrollViewer.Offset -= GetOffset(scale, point.X, point.Y);

                SetCurrentValue(ZoomLevelProperty, newTransform.Value.M11);
                System.Diagnostics.Debug.WriteLine($"End Zoom to {newTransform.Value.M11}");
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

        public PdfPageControl? TryGetPdfPageControl(int pageNumber)
        {
            System.Diagnostics.Debug.WriteLine($"GetPdfPageControl {pageNumber}.");
            if (_itemsRepeater!.TryGetElement(pageNumber - 1) is PdfPageControl pdfPage)
            {
                return pdfPage;
            }
            return null;
        }

        public PdfPageControl? GetPdfPageControlOver(PointerEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("GetPdfPageControlOver.");
            var point = e.GetPosition(_itemsRepeater);

            int startIndex = SelectedPageIndex - 1; // Switch from one-indexed to zero-indexed

            bool isAfterSelectedPage = false;

            // Check selected current page
            if (_itemsRepeater!.TryGetElement(startIndex) is PdfPageControl currentPdfPage)
            {
                System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {startIndex + 1}.");
                if (currentPdfPage.Bounds.Contains(point))
                {
                    return currentPdfPage;
                }

                isAfterSelectedPage = point.Y > currentPdfPage.Bounds.Bottom;
            }

            if (isAfterSelectedPage)
            {
                // Start with checking forward
                for (int p = startIndex + 1; p < PageCount; ++p)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                    if (_itemsRepeater!.TryGetElement(p) is PdfPageControl pdfPage)
                    {
                        if (pdfPage.Bounds.Contains(point))
                        {
                            return pdfPage;
                        }

                        if (point.Y < pdfPage.Bounds.Top)
                        {
                            return null;
                        }
                    }
                }
            }
            else
            {
                // Continue with checking backward
                for (int p = startIndex - 1; p >= 0; --p)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                    if (_itemsRepeater!.TryGetElement(p) is PdfPageControl pdfPage)
                    {
                        if (pdfPage.Bounds.Contains(point))
                        {
                            return pdfPage;
                        }

                        if (point.Y > pdfPage.Bounds.Bottom)
                        {
                            return null;
                        }
                    }
                }
            }

            return null;
        }

        private void CheckPagesVisibility()
        {
            // TODO - There's a bug in Avalonia (it seems) where the offset is incorrectly reset to 0,0

            if (_isCheckingPageVisibility)
            {
                return; // Should never happen - log error
            }

            if (_isZooming)
            {
                return;
            }

            if (_layoutTransformControl is null || _scrollViewer is null || _itemsRepeater is null ||
                _scrollViewer.Viewport.Height == 0 || _scrollViewer.Viewport.Width == 0)
            {
                return;
            }

            if (_itemsRepeater.ItemsSourceView == null || _itemsRepeater.ItemsSourceView.Count == 0)
            {
                // TODO - Why is this happening?
                return;
            }

            System.Diagnostics.Debug.WriteLine("CheckPagesVisibility.");

            Rect viewPort = new Rect(_scrollViewer.Offset.X, _scrollViewer.Offset.Y, _scrollViewer.Viewport.Width, _scrollViewer.Viewport.Height)
                .TransformToAABB(_layoutTransformInverseMatrix); // Inverse transform

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

                if (_itemsRepeater.TryGetElement(p) is not PdfPageControl cp)
                {
                    // Page is not realised
                    return !isPreviousPageVisible;
                }

                System.Diagnostics.Debug.Assert(cp.DataContext is PdfPageViewModel);

                if (!needMoreChecks || cp.Bounds.IsEmpty())
                {
                    if (!cp.IsPageVisible)
                    {
                        // Page is not visible and no need for more checks.
                        // All following pages are already set to IsPageVisible = false
                        return false;
                    }
                    cp.SetCurrentValue(PdfPageControl.VisibleAreaProperty, null);
                    return true;
                }

                Rect view = cp.Bounds;

                if (view.Height == 0)
                {
                    // No need for further checks, not visible
                    cp.SetCurrentValue(PdfPageControl.VisibleAreaProperty, null);
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
                        cp.SetCurrentValue(PdfPageControl.VisibleAreaProperty, null);
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
                    cp.SetCurrentValue(PdfPageControl.VisibleAreaProperty, view.Translate(new Vector(-left, -top)));
                    return true;
                }

                cp.SetCurrentValue(PdfPageControl.VisibleAreaProperty, null);
                // If previous page was visible but current page is not, we have the last visible page
                needMoreChecks = !isPreviousPageVisible;
                return true;
            }

            // Check current page visibility
            int startIndex = SelectedPageIndex - 1; // Switch from one-indexed to zero-indexed
            CheckPageVisibility(startIndex, out bool isSelectedPageVisible);

            // Start with checking forward.
            // TODO - While scrolling down, the current selected page can become invisible and force
            // a full iteration if starting backward
            isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
            int forwardIndex = startIndex + 1;
            while (forwardIndex < PageCount && CheckPageVisibility(forwardIndex, out _))
            {
                forwardIndex++;
            }

            // Continue with checking backward
            isPreviousPageVisible = isSelectedPageVisible; // Previous page is SelectedPageIndex
            needMoreChecks = true;
            int backwardIndex = startIndex - 1;
            while (backwardIndex >= 0 && CheckPageVisibility(backwardIndex, out _))
            {
                backwardIndex--;
            }

            indexMaxOverlap++; // Switch to base 1 indexing

            if (indexMaxOverlap == 0)
            {
                if (_scrollViewer.Offset != default)
                {
                    // TODO - This is a different bug from the one mentioned above
                }

                try
                {
                    // TODO - Check why this is happening. It seems sometime the offset is incorrectly 0,0.
                    // Hack to display the current page when no page is visible.
                    // This is related to a bug (I think) in the item repeater
                    _isCheckingPageVisibility = true;
                    // https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/ItemsRepeaterPage.xaml.cs
                    var element = _itemsRepeater.GetOrCreateElement(SelectedPageIndex - 1);
                    ((TopLevel)VisualRoot!).UpdateLayout();
                    element.BringIntoView();
                    _isCheckingPageVisibility = false;

                    // We need to check visibility again...
                    // We need to be careful about stack overflow here as we
                    // are calling the method from itself and _isCheckingPageVisibility is false
                    Dispatcher.UIThread.Post(CheckPagesVisibility);
                    return;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR Something went wrong in the hack: {e}");
                    return;
                }
            }

            if (indexMaxOverlap != -1 && SelectedPageIndex != indexMaxOverlap)
            {
                _isCheckingPageVisibility = true;
                SetCurrentValue(SelectedPageIndexProperty, indexMaxOverlap);
                _isCheckingPageVisibility = false;
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
    }
}
