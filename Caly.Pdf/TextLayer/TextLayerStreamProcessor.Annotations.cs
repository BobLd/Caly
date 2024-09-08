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
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.TextLayer
{
    public partial class TextLayerStreamProcessor
    {
        /// <summary>
        /// Raw page annotations.
        /// </summary>
        private readonly Lazy<Annotation[]> _annotations;

        /// <summary>
        /// Processed page annotations.
        /// </summary>
        private readonly List<PdfAnnotation> _pdfAnnotations = new();

        private static bool IsInteractive(Annotation annotation)
        {
            return annotation.Type == AnnotationType.Link;
        }

        private void DrawAnnotations()
        {
            foreach (Annotation annotation in _annotations.Value.Where(IsInteractive))
            {
                if (annotation.Action is null)
                {
                    continue;
                }

                PdfRectangle rect = annotation.Rectangle;

                if (rect.Width > 0 && rect.Height > 0)
                {
                    var matrix = TransformationMatrix.Identity;
                    if (annotation.AnnotationDictionary.TryGet<ArrayToken>(NameToken.Matrix, PdfScanner, out var matrixToken))
                    {
                        matrix = TransformationMatrix.FromArray(matrixToken.Data.OfType<NumericToken>()
                            .Select(x => x.Double).ToArray());
                    }

                    PdfRectangle? bbox = rect;

                    // https://github.com/apache/pdfbox/blob/47867f7eee275e9e54a87222b66ab14a8a3a062a/pdfbox/src/main/java/org/apache/pdfbox/contentstream/PDFStreamEngine.java#L310
                    // transformed appearance box  fixme: may be an arbitrary shape
                    PdfRectangle transformedBox = InverseYAxis(matrix.Transform(bbox.Value).Normalise(), _pageHeight);

                    _pdfAnnotations.Add(new PdfAnnotation()
                    {
                        BoundingBox = transformedBox,
                        Action = annotation.Action
                    });
                }
            }
        }
    }
}
