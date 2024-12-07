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

using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Media;
using Caly.Core.Controls;
using Caly.Core.Models;
using Caly.Core.ViewModels;

namespace Caly.Core.Handlers.Interfaces
{
    public interface ITextSelectionHandler
    {
        PdfTextSelection Selection { get; }

        void OnPointerMoved(PointerEventArgs e);

        void OnPointerPressed(PointerPressedEventArgs e);

        void OnPointerReleased(PointerReleasedEventArgs e);
        
        /// <summary>
        /// Add results to existing ones.
        /// </summary>
        void AddTextSearchResults(PdfDocumentViewModel documentViewModel, IReadOnlyCollection<TextSearchResultViewModel> searchResults);

        /// <summary>
        /// Clear current results.
        /// </summary>
        void ClearTextSearchResults(PdfDocumentViewModel documentViewModel);

        /// <summary>
        /// TODO - Should not be in selection handler.
        /// </summary>
        void RenderPage(PdfPageTextLayerControl control, DrawingContext context);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(PdfDocumentControl pdfDocumentControl);

        /// <summary>
        /// Clear the current selection.
        /// </summary>
        void ClearSelection(PdfPageTextLayerControl currentTextLayer);
    }
}
