using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.TextLayer
{
    internal sealed record NoOpColorSpaceContext : IColorSpaceContext
    {
        public static readonly NoOpColorSpaceContext Instance = new();

        public ColorSpaceDetails CurrentStrokingColorSpace => DeviceGrayColorSpaceDetails.Instance;

        public ColorSpaceDetails CurrentNonStrokingColorSpace => DeviceGrayColorSpaceDetails.Instance;

        public void SetStrokingColorspace(NameToken colorspace, DictionaryToken dictionary = null)
        {
            // No op
        }

        public void SetNonStrokingColorspace(NameToken colorspace, DictionaryToken dictionary = null)
        {
            // No op
        }

        public void SetStrokingColor(IReadOnlyList<double> operands, NameToken patternName = null)
        {
            // No op
        }

        public void SetStrokingColorGray(double gray)
        {
            // No op
        }

        public void SetStrokingColorRgb(double r, double g, double b)
        {
            // No op
        }

        public void SetStrokingColorCmyk(double c, double m, double y, double k)
        {
            // No op
        }

        public void SetNonStrokingColor(IReadOnlyList<double> operands, NameToken patternName = null)
        {
            // No op
        }

        public void SetNonStrokingColorGray(double gray)
        {
            // No op
        }

        public void SetNonStrokingColorRgb(double r, double g, double b)
        {
            // No op
        }

        public void SetNonStrokingColorCmyk(double c, double m, double y, double k)
        {
            // No op
        }

        public IColorSpaceContext DeepClone()
        {
            return Instance;
        }
    }
}
