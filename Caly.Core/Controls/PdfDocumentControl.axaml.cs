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

using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ScrollViewer", typeof(IScrollable))]
    [TemplatePart("PART_LayoutTransformControl", typeof(CalyLayoutTransformControl))]
    [TemplatePart("PART_PdfPageItemsControl", typeof(PdfPageItemsControl))]
    public class PdfDocumentControl : CalyTemplatedControl
    {
        private PdfPageItemsControl? _pdfPageItemsControl;

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
        public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfDocumentControl, double>(nameof(ZoomLevel), 1, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedPageIndex"/> property. Starts at 1.
        /// </summary>
        public static readonly StyledProperty<int?> SelectedPageIndexProperty = AvaloniaProperty.Register<PdfDocumentControl, int?>(nameof(SelectedPageIndex), 1, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="SelectedBookmark"/> property.
        /// </summary>
        public static readonly StyledProperty<PdfBookmarkNode?> SelectedBookmarkProperty = AvaloniaProperty.Register<PdfDocumentControl, PdfBookmarkNode?>(nameof(SelectedBookmark));

        public static readonly StyledProperty<TextSearchResultViewModel?> SelectedTextSearchResultProperty = AvaloniaProperty.Register<PdfDocumentControl, TextSearchResultViewModel?>(nameof(SelectedTextSearchResult));

        /// <summary>
        /// Defines the <see cref="TextSelectionHandler"/> property.
        /// </summary>
        public static readonly StyledProperty<ITextSelectionHandler?> TextSelectionHandlerProperty = AvaloniaProperty.Register<PdfDocumentControl, ITextSelectionHandler?>(nameof(TextSelectionHandler));

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
        public int? SelectedPageIndex
        {
            get => GetValue(SelectedPageIndexProperty);
            set => SetValue(SelectedPageIndexProperty, value);
        }

        public PdfBookmarkNode? SelectedBookmark
        {
            get => GetValue(SelectedBookmarkProperty);
            set => SetValue(SelectedBookmarkProperty, value);
        }

        public TextSearchResultViewModel? SelectedTextSearchResult
        {
            get => GetValue(SelectedTextSearchResultProperty);
            set => SetValue(SelectedTextSearchResultProperty, value);
        }

        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public ITextSelectionHandler? TextSelectionHandler
        {
            get => GetValue(TextSelectionHandlerProperty);
            set => SetValue(TextSelectionHandlerProperty, value);
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
                if (change.NewValue is int p)
                {
                    GoToPage(p);
                }
            }
            else if (change.Property == SelectedBookmarkProperty)
            {
                if (SelectedBookmark?.PageNumber.HasValue == true &&
                    SelectedBookmark.PageNumber.Value != SelectedPageIndex)
                {
                    SetCurrentValue(SelectedPageIndexProperty, SelectedBookmark.PageNumber.Value);
                }
            }
            else if (change.Property == SelectedTextSearchResultProperty)
            {
                if (change.NewValue is TextSearchResultViewModel { PageNumber: > 0 } r)
                {
                    SetCurrentValue(SelectedPageIndexProperty, r.PageNumber);
                }
            }
            else if (change.Property == ZoomLevelProperty)
            {
                if (_pdfPageItemsControl?.LayoutTransformControl is null || change.NewValue is not double newZoom)
                {
                    return;
                }

                if (!_pdfPageItemsControl.LayoutTransformControl.IsAttachedToVisualTree())
                {
                    return;
                }

                var pixelPoint = new PixelPoint();
                if (!_pdfPageItemsControl.DesiredSize.IsEmpty())
                {
                    _pdfPageItemsControl.DesiredSize.Deconstruct(out double w, out double h);
                    pixelPoint = new PixelPoint((int)(w / 2.0), (int)(h / 2.0));
                }
                else if (!_pdfPageItemsControl.Bounds.Size.IsEmpty())
                {
                    _pdfPageItemsControl.Bounds.Size.Deconstruct(out double w, out double h);
                    pixelPoint = new PixelPoint((int)(w / 2.0), (int)(h / 2.0));
                }

                var point = _pdfPageItemsControl.LayoutTransformControl.PointToClient(pixelPoint);

                double oldZoom = (double?)change.OldValue ?? 1.0;
                double dZoom = newZoom / oldZoom;
                _pdfPageItemsControl?.ZoomTo(dZoom, point);
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _pdfPageItemsControl = e.NameScope.FindFromNameScope<PdfPageItemsControl>("PART_PdfPageItemsControl");
        }

        /// <summary>
        /// Scrolls to the page number.
        /// </summary>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        public void GoToPage(int pageNumber)
        {
            _pdfPageItemsControl?.GoToPage(pageNumber);
        }

        /// <summary>
        /// Get the page control for the page number.
        /// </summary>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        /// <returns>The page control, or <c>null</c> if not found.</returns>
        public PdfPageItem? GetPdfPageItem(int pageNumber)
        {
            return _pdfPageItemsControl!.GetPdfPageItem(pageNumber);
        }

        public PdfPageItem? GetPdfPageItemOver(PointerEventArgs e)
        {
            return _pdfPageItemsControl!.GetPdfPageItemOver(e);
        }
    }
}
