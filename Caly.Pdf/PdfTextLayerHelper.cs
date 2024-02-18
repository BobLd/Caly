using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caly.Pdf.Layout;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Caly.Pdf
{
    public static class PdfTextLayerHelper
    {
        private static readonly NearestNeighbourWordExtractor _wordExtractor = NearestNeighbourWordExtractor.Instance;
        private static readonly DocstrumBoundingBoxes _pageSegmenter = DocstrumBoundingBoxes.Instance;

        public static PdfTextLayer GetTextLayer(IReadOnlyList<TextBlock> blocks, double pageHeight)
        {
            int wordIndex = 0;
            int lineIndex = 0;
            int blockIndex = 0;

            PdfTextBlock[] pdfBlocks = new PdfTextBlock[blocks.Count];

            for (int b = 0; b < pdfBlocks.Length; b++)
            {
                var block = blocks[b];
                var lines = new PdfTextLine[block.TextLines.Count];
                int blockStartIndex = wordIndex;

                for (int l = 0; l < lines.Length; l++)
                {
                    var line = block.TextLines[l];
                    var pdfWords = new PdfWord[line.Words.Count];
                    int lineStartIndex = wordIndex;

                    for (int w = 0; w < pdfWords.Length; w++)
                    {
                        var word = line.Words[w];
                        var pdfLetters = new PdfLetter[word.Letters.Count];

                        for (int le = 0; le < word.Letters.Count; le++)
                        {
                            var letter = word.Letters[le];

                            pdfLetters[le] = new PdfLetter(letter.Value.AsMemory(),
                                InverseYAxis(letter.GlyphRectangle, pageHeight),
                                letter.PointSize,
                                letter.TextSequence);
                        }

                        pdfWords[w] = new PdfWord(pdfLetters, InverseYAxis(word.BoundingBox, pageHeight), wordIndex++, lineIndex, blockIndex);
                    }

                    lines[l] = new PdfTextLine(pdfWords, InverseYAxis(line.BoundingBox, pageHeight), lineIndex++, blockIndex, lineStartIndex);
                }

                pdfBlocks[b] = new PdfTextBlock(lines, InverseYAxis(block.BoundingBox, pageHeight), blockIndex++, blockStartIndex, wordIndex - 1);
            }

            return new PdfTextLayer(pdfBlocks);
        }

        public static PdfTextLayer GetTextLayer(Page page)
        {
            var words = _wordExtractor.GetWords(page.Letters);
            var blocks = _pageSegmenter.GetBlocks(words);

            return GetTextLayer(blocks, page.Height);
        }

        public static PdfTextLayer GetTextLayer(PageTextLayerContent page)
        {
            if (page.Letters.Count == 0)
            {
                return PdfTextLayer.Empty;
            }

            var words = CalyNNWordExtractor.Instance.GetWords(page.Letters);
            var pdfBlocks = CalyDocstrum.Instance.GetBlocks(words);

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
