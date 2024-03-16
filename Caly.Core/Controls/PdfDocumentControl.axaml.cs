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
using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caly.Core.Handlers;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ScrollViewer", typeof(IScrollable))]
    [TemplatePart("PART_LayoutTransformControl", typeof(CalyLayoutTransformControl))]
    [TemplatePart("PART_ItemsControl", typeof(ItemsControl))]
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
        private CalyLayoutTransformControl? _layoutTransformControl;
        private ItemsControl? _itemsControl;

        private bool _isCheckingPageVisibility = false;
        private bool _isZooming = false;

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
                    SetCurrentValue(SelectedPageIndexProperty, SelectedBookmark.PageNumber.Value);
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
            _layoutTransformControl = e.NameScope.FindFromNameScope<CalyLayoutTransformControl>("PART_LayoutTransformControl");
            _itemsControl = e.NameScope.FindFromNameScope<ItemsControl>("PART_ItemsControl");

            _scrollViewer.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
            _scrollViewer.AddHandler(SizeChangedEvent, (_, _) => SetPagesVisibility(), RoutingStrategies.Direct);
            _scrollViewer.AddHandler(KeyDownEvent, _onKeyDownHandler);
            _layoutTransformControl.AddHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);

            _itemsControl.ContainerPrepared += _onContainerPrepared;
            _itemsControl.ContainerClearing += _onContainerClearing;
        }

        private static void _onContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container is not ContentPresenter { Content: PdfPageViewModel vm } cp)
            {
                return;
            }

            cp.PropertyChanged += _onContainerPropertyChanged;
            vm.IsPagePrepared = true;
        }

        private static void _onContainerClearing(object? sender, ContainerClearingEventArgs e)
        {
            if (e.Container is not ContentPresenter cp)
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

        // TODO use:
        // public static KeyGesture? PasteGesture => Application.Current?.PlatformSettings?.HotkeyConfiguration.WholeWordTextActionModifiers.FirstOrDefault();

        private void _onPointerWheelChangedHandler(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Delta.Y != 0)
            {
                ZoomTo(e);
                e.Handled = true;
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);

            _scrollViewer?.RemoveHandler(ScrollViewer.ScrollChangedEvent, (_, _) => SetPagesVisibility());
            _scrollViewer?.RemoveHandler(SizeChangedEvent, (_, _) => SetPagesVisibility());
            _scrollViewer?.RemoveHandler(KeyDownEvent, _onKeyDownHandler);
            _layoutTransformControl?.RemoveHandler(PointerWheelChangedEvent, _onPointerWheelChangedHandler);

            if (_itemsControl is not null)
            {
                _itemsControl.ContainerPrepared -= _onContainerPrepared;
                _itemsControl.ContainerClearing -= _onContainerClearing;
            }
        }

        /// <summary>
        /// Scrolls the content downward by one page.
        /// </summary>
        public void GoToPage(int number)
        {
            if (_scrollViewer is null || _itemsControl is null || _isCheckingPageVisibility ||
                number <= 0 || number > PageCount || _scrollViewer.Viewport.IsEmpty() ||
                _itemsControl.ItemsView.Count == 0)
            {
                return;
            }

            _itemsControl.ScrollIntoView(number - 1);
        }

        private void ZoomTo(PointerWheelEventArgs e)
        {
            if (_layoutTransformControl is null || _scrollViewer is null)
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
                double dZoom =  Math.Round(Math.Pow(_zoomFactor, delta), 4); // If IsScrollInertiaEnabled = false, Y is only 1 or -1
                double newZoom = (_layoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0) * dZoom;

                if (newZoom < _minZoom || newZoom > _maxZoom)
                {
                    return;
                }

                var builder = TransformOperations.CreateBuilder(1);
                builder.AppendScale(newZoom, newZoom);
                _layoutTransformControl.LayoutTransform = builder.Build();

                Point point = e.GetPosition(_layoutTransformControl);

                var offset = _scrollViewer.Offset - GetOffset(dZoom, point.X, point.Y);
                if (delta > 0)
                {
                    // When zooming-in, we need to re-arrange the scroll viewer
                    _scrollViewer.Measure(Size.Infinity);
                    _scrollViewer.Arrange(new Rect(_scrollViewer.DesiredSize));
                }

                _scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, offset);

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
        /// Get the page control for the page number.
        /// </summary>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        /// <returns>The page control, or <c>null</c> if not found.</returns>
        public PdfPageControl? GetPdfPageControl(int pageNumber)
        {
            System.Diagnostics.Debug.WriteLine($"GetPdfPageControl {pageNumber}.");
            if (_itemsControl!.ContainerFromIndex(pageNumber - 1) is ContentPresenter presenter)
            {
                return presenter.GetVisualDescendants().OfType<PdfPageControl>().SingleOrDefault();
            }

            return null;
        }

        public PdfPageControl? GetPdfPageControlOver(PointerEventArgs e)
        {
            Point point = e.GetPosition(_itemsControl);

            // Quick reject
            if (!_itemsControl!.Bounds.Contains(point))
            {
                System.Diagnostics.Debug.WriteLine("GetPdfPageControlOver Quick reject.");
                return null;
            }

            int startIndex = SelectedPageIndex - 1; // Switch from one-indexed to zero-indexed

            bool isAfterSelectedPage = false;

            // Check selected current page
            if (_itemsControl.ContainerFromIndex(startIndex) is ContentPresenter presenter)
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
                for (int p = startIndex + 1; p < PageCount; ++p)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                    if (_itemsControl!.ContainerFromIndex(p) is not ContentPresenter cp)
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
                for (int p = startIndex - 1; p >= 0; --p)
                {
                    System.Diagnostics.Debug.WriteLine($"GetPdfPageControlOver page {p + 1}.");
                    if (_itemsControl!.ContainerFromIndex(p) is not ContentPresenter cp)
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

        private void SetPagesVisibility()
        {
            if (_isCheckingPageVisibility)
            {
                return;
            }

            if (_isZooming)
            {
                return;
            }

            if (_layoutTransformControl is null || _scrollViewer is null || _itemsControl is null ||
                _scrollViewer.Viewport.IsEmpty() || _itemsControl.ItemsView.Count == 0)
            {
                return;
            }

            Debug.AssertIsNullOrScale(_layoutTransformControl.LayoutTransform?.Value);

            double invScale = 1.0 / (_layoutTransformControl.LayoutTransform?.Value.M11 ?? 1.0);
            Matrix fastInverse = Matrix.CreateScale(invScale, invScale);

            Rect viewPort = new Rect((Point)_scrollViewer.Offset ,_scrollViewer.Viewport).TransformToAABB(fastInverse);

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

                if (_itemsControl.ContainerFromIndex(p) is not ContentPresenter { Content: PdfPageViewModel vm } cp)
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

            // TODO - There's a bug in Avalonia (it seems) where the offset is incorrectly reset to 0,0
            // It should have been improved with commits 86bfc26 and acc3c3c but seems it's still there
            // Use DeviceN_CS_test.pdf from PdfPig integration test
            if (indexMaxOverlap == 0 && _scrollViewer.Offset == default)
            {
                try
                {
                    // TODO - Check why this is happening. It seems sometime the offset is incorrectly 0,0.
                    // Hack to display the current page when no page is visible.
                    _isCheckingPageVisibility = true;

                    _itemsControl.ScrollIntoView(SelectedPageIndex - 1);

                    _isCheckingPageVisibility = false;

                    // We need to check visibility again...
                    // We need to be careful about stack overflow here as we
                    // are calling the method from itself and _isCheckingPageVisibility is false
                    Dispatcher.UIThread.Post(SetPagesVisibility);
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
