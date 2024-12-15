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
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;

namespace Caly.Tests
{
    public class DuplicateOverlappingTextTests
    {
        [Fact]
        public void Document_PublisherError_1()
        {
            using (var doc = PdfDocument.Open("Document-PublisherError-1.pdf"))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                var expectedLetters = CalyDuplicateOverlappingTextProcessor.Get(layer.Letters, CancellationToken.None);
                var expectedWords = CalyNNWordExtractor.Instance.GetWords(expectedLetters, CancellationToken.None).OrderByReadingOrder().ToArray();
                var expectedParagraphs = CalyDocstrum.Instance.GetBlocks(expectedWords, CancellationToken.None).ToArray();

                layer = doc.GetPage<PageTextLayerContent>(1);
                var actualLetters = CalyDuplicateOverlappingTextProcessor.GetInPlace(layer.Letters.ToList(), CancellationToken.None);
                var actualWords = CalyNNWordExtractor.Instance.GetWords(actualLetters, CancellationToken.None).OrderByReadingOrder().ToArray();
                var actualParagraphs = CalyDocstrum.Instance.GetBlocks(actualWords, CancellationToken.None).ToArray();

                Assert.Equal(expectedParagraphs.Length, actualParagraphs.Length);

                for (int i = 0; i < expectedParagraphs.Length; ++i)
                {
                    var expected = expectedParagraphs[i];
                    var actual = actualParagraphs[i];
                    Assert.Equal(expected.TextLines.Count, actual.TextLines.Count);

                    for (int l = 0; l < expected.TextLines.Count; ++l)
                    {
                        var expectedLine = expected.TextLines[l];
                        var actualLine = actual.TextLines[l];
                        Assert.Equal(expectedLine.Words.Count, actualLine.Words.Count);

                        for (int w = 0; w < expectedLine.Words.Count; ++w)
                        {
                            var expectedWord = expectedLine.Words[w];
                            var actualWord = actualLine.Words[w];
                            Assert.True(actualWord.Value.Span.SequenceEqual(expectedWord.Value.Span));
                        }
                    }
                }
            }
        }
    }
}