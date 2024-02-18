using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Caly.Pdf.Models
{
    public interface IPdfTextElement
    {
        public PdfRectangle BoundingBox { get; }

        public TextOrientation TextOrientation { get; }
    }
}
