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

using Caly.Pdf.Models;
using Caly.Pdf.TextLayer;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
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
    public sealed class TextLayerNoDupFactory : BasePageFactory<PageTextLayerContent>
    {
        public TextLayerNoDupFactory(IPdfTokenScanner pdfScanner, IResourceStore resourceStore,
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

            var annotationProvider = new AnnotationProvider(PdfScanner,
                dictionary,
                initialMatrix,
                namedDestinations,
                ParsingOptions.Logger);

            var context = new TextLayerNoDupStreamProcessor(pageNumber, ResourceStore, PdfScanner, PageContentParser,
                FilterProvider, cropBox, userSpaceUnit, rotation, initialMatrix,
                effectiveCropBox.Width, effectiveCropBox.Height,
                ParsingOptions, annotationProvider);

            return context.Process(pageNumber, operations);
        }
    }
}
