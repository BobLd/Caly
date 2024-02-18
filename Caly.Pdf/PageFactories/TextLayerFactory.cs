using Caly.Pdf.Models;
using Caly.Pdf.TextLayer;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Filters;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Parser;
using UglyToad.PdfPig.Tokenization.Scanner;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.PageFactories
{
    public sealed class TextLayerFactory : BasePageFactory<PageTextLayerContent>
    {
        public TextLayerFactory(IPdfTokenScanner pdfScanner, IResourceStore resourceStore,
            ILookupFilterProvider filterProvider, IPageContentParser pageContentParser, ParsingOptions parsingOptions)
            : base(pdfScanner, resourceStore, filterProvider, pageContentParser,
                parsingOptions)
        {
        }

        protected override PageTextLayerContent ProcessPage(int pageNumber, DictionaryToken dictionary,
            NamedDestinations namedDestinations, MediaBox mediaBox,
            CropBox cropBox, UserSpaceUnit userSpaceUnit, PageRotationDegrees rotation,
            TransformationMatrix initialMatrix,
            IReadOnlyList<IGraphicsStateOperation> operations)
        {
            // Special case where cropbox is outside mediabox: use cropbox instead of intersection
            var effectiveCropBox = mediaBox.Bounds.Intersect(cropBox.Bounds) ?? cropBox.Bounds;

            var context = new TextLayerStreamProcessor(pageNumber, ResourceStore, PdfScanner, PageContentParser,
                FilterProvider, cropBox, userSpaceUnit, rotation, initialMatrix,
                effectiveCropBox.Width, effectiveCropBox.Height,
                ParsingOptions);

            var content = context.Process(pageNumber, operations);

            return content;
        }
    }
}
