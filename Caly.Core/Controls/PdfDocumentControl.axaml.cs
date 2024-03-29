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
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ScrollViewer", typeof(IScrollable))]
    [TemplatePart("PART_LayoutTransformControl", typeof(CalyLayoutTransformControl))]
    [TemplatePart("PART_ItemsControl", typeof(PdfPageItemsControl))]
    public class PdfDocumentControl : CalyTemplatedControl
    {
        private PdfPageItemsControl? _itemsControl;

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
        /// Defines the <see cref="TextSelectionHandler"/> property.
        /// </summary>
        public static readonly StyledProperty<ITextSelectionHandler?> TextSelectionHandlerProperty = AvaloniaProperty.Register<PdfDocumentControl, ITextSelectionHandler?>(nameof(TextSelectionHandler), defaultBindingMode: BindingMode.TwoWay);

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
                GoToPage(SelectedPageIndex);
            }
            else if (change.Property == SelectedBookmarkProperty)
            {
                if (SelectedBookmark?.PageNumber.HasValue == true && SelectedBookmark.PageNumber.Value != SelectedPageIndex)
                {
                    SetCurrentValue(SelectedPageIndexProperty, SelectedBookmark.PageNumber.Value);
                }
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _itemsControl = e.NameScope.FindFromNameScope<PdfPageItemsControl>("PART_ItemsControl");
        }

        /// <summary>
        /// Scrolls to the page number.
        /// </summary>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        public void GoToPage(int pageNumber)
        {
            _itemsControl?.GoToPage(pageNumber);
        }

        /// <summary>
        /// Get the page control for the page number.
        /// </summary>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        /// <returns>The page control, or <c>null</c> if not found.</returns>
        public PdfPageControl? GetPdfPageControl(int pageNumber)
        {
            return _itemsControl!.GetPdfPageControl(pageNumber);
        }

        public PdfPageControl? GetPdfPageControlOver(PointerEventArgs e)
        {
            return _itemsControl!.GetPdfPageControlOver(e);
        }
    }
}
