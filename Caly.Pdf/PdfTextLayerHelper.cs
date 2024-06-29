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

using Caly.Pdf.Layout;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Caly.Pdf
{
    public static class PdfTextLayerHelper
    {
        public static bool IsStroke(this TextRenderingMode textRenderingMode)
        {
            switch (textRenderingMode)
            {
                case TextRenderingMode.Stroke:
                case TextRenderingMode.StrokeClip:
                case TextRenderingMode.FillThenStroke:
                case TextRenderingMode.FillThenStrokeClip:
                    return true;

                case TextRenderingMode.Fill:
                case TextRenderingMode.FillClip:
                case TextRenderingMode.NeitherClip:
                case TextRenderingMode.Neither:
                    return false;

                default:
                    return false;
            }
        }

        public static bool IsFill(this TextRenderingMode textRenderingMode)
        {
            switch (textRenderingMode)
            {
                case TextRenderingMode.Fill:
                case TextRenderingMode.FillClip:
                case TextRenderingMode.FillThenStroke:
                case TextRenderingMode.FillThenStrokeClip:
                    return true;

                case TextRenderingMode.Stroke:
                case TextRenderingMode.StrokeClip:
                case TextRenderingMode.NeitherClip:
                case TextRenderingMode.Neither:
                    return false;

                default:
                    return false;
            }
        }

        public static PdfTextLayer GetTextLayer(PageTextLayerContent page, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (page.Letters.Count == 0)
            {
                return PdfTextLayer.Empty;
            }

            var letters = CalyDuplicateOverlappingTextProcessor.Get(page.Letters);

            var words = CalyNNWordExtractor.Instance.GetWords(letters, cancellationToken);
            var pdfBlocks = CalyDocstrum.Instance.GetBlocks(words, cancellationToken);

            int wordIndex = 0;
            int lineIndex = 0;
            int blockIndex = 0;

            for (int b = 0; b < pdfBlocks.Count; b++)
            {
                var block = pdfBlocks[b];
                var lines = block.TextLines;
                int blockStartIndex = wordIndex;

                for (int l = 0; l < lines.Count; l++)
                {
                    var line = block.TextLines[l];
                    var pdfWords = line.Words;
                    int lineStartIndex = wordIndex;

                    for (int w = 0; w < pdfWords.Count; w++)
                    {
                        // throw if cancelled every now and then
                        if (wordIndex % 100 == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        pdfWords[w].IndexInPage = wordIndex++;
                        pdfWords[w].TextLineIndex = lineIndex;
                        pdfWords[w].TextBlockIndex = blockIndex;
                    }

                    lines[l].IndexInPage = lineIndex++;
                    lines[l].TextBlockIndex = blockIndex;
                    lines[l].WordStartIndex = lineStartIndex;
                }

                pdfBlocks[b].IndexInPage = blockIndex++;
                pdfBlocks[b].WordStartIndex = blockStartIndex;
                pdfBlocks[b].WordEndIndex = wordIndex - 1;
            }

            return new PdfTextLayer(pdfBlocks);
        }

        private static PdfPoint InverseYAxis(PdfPoint point, double height)
        {
            return new PdfPoint(point.X, height - point.Y);
        }

        private static PdfRectangle InverseYAxis(PdfRectangle rectangle, double height)
        {
            var topLeft = InverseYAxis(rectangle.TopLeft, height);
            var topRight = InverseYAxis(rectangle.TopRight, height);
            var bottomLeft = InverseYAxis(rectangle.BottomLeft, height);
            var bottomRight = InverseYAxis(rectangle.BottomRight, height);
            return new PdfRectangle(topLeft, topRight, bottomLeft, bottomRight);
        }

        internal static TextOrientation GetTextOrientation(IReadOnlyList<IPdfTextElement> letters)
        {
            if (letters.Count == 1)
            {
                return letters[0].TextOrientation;
            }

            var tempTextOrientation = letters[0].TextOrientation;
            if (tempTextOrientation != TextOrientation.Other)
            {
                foreach (var letter in letters)
                {
                    if (letter.TextOrientation != tempTextOrientation)
                    {
                        return TextOrientation.Other;
                    }
                }
            }
            return tempTextOrientation;
        }
    }
}
