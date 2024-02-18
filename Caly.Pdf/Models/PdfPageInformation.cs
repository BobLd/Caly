namespace Caly.Pdf.Models
{
    public readonly struct PdfPageInformation
    {
        public int PageNumber { get; init; }

        public double Height { get; init; }

        public double Width { get; init; }
    }
}
