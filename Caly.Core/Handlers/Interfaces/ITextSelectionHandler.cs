using Avalonia.Input;
using Avalonia.Media;
using Caly.Core.Controls;

namespace Caly.Core.Handlers.Interfaces
{
    public interface ITextSelectionHandler
    {
        void OnPointerMoved(PointerEventArgs e);

        void OnPointerPressed(PointerPressedEventArgs e);

        void OnPointerReleased(PointerReleasedEventArgs e);

        void SelectAllTextInPage(PdfPageTextLayerControl control);

        /// <summary>
        /// TODO - Should not be in selection handler.
        /// </summary>
        /// <param name="control"></param>
        /// <param name="context"></param>
        void RenderPage(PdfPageTextLayerControl control, DrawingContext context);
    }
}
