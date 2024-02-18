namespace Caly.Pdf.Models
{
    public sealed record PageTextLayerContent
    {
        public IReadOnlyList<PdfLetter> Letters { get; init; }
    }
}
