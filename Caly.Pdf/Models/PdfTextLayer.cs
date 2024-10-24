﻿// Copyright (C) 2024 BobLd
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

using System.Collections;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;

namespace Caly.Pdf.Models
{
    public sealed record PdfTextLayer : IReadOnlyList<PdfWord>
    {
        internal static readonly PdfTextLayer Empty = new(Array.Empty<PdfTextBlock>(), Array.Empty<PdfAnnotation>());

        public PdfTextLayer(IReadOnlyList<PdfTextBlock> textBlocks, IReadOnlyList<PdfAnnotation> annotations)
        {
            Annotations = annotations;
            TextBlocks = textBlocks;
            if (textBlocks?.Count > 0)
            {
                Count = textBlocks.Sum(b => b.TextLines.Sum(l => l.Words.Count));
                System.Diagnostics.Debug.Assert(Count == textBlocks.SelectMany(b => b.TextLines.SelectMany(l => l.Words)).Count());
            }
        }

        /// <summary>
        /// The text lines contained in the block.
        /// </summary>
        public IReadOnlyList<PdfTextBlock> TextBlocks { get; }

        public IReadOnlyList<PdfAnnotation> Annotations { get; }

        public PdfAnnotation? FindAnnotationOver(double x, double y)
        {
            if (Annotations is null || Annotations.Count == 0) return null;

            var point = new PdfPoint(x, y);

            foreach (PdfAnnotation annotation in Annotations)
            {
                if (annotation.BoundingBox.Contains(point, true))
                {
                    return annotation;
                }
            }

            return null;
        }

        public PdfWord? FindWordOver(double x, double y)
        {
            if (TextBlocks is null || TextBlocks.Count == 0) return null;

            foreach (PdfTextBlock block in TextBlocks)
            {
                if (!block.Contains(x, y))
                {
                    continue;
                }

                PdfWord? candidate = block.FindWordOver(x, y);
                if (candidate is not null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public PdfTextLine? FindLineOver(double x, double y)
        {
            if (TextBlocks is null || TextBlocks.Count == 0) return null;

            foreach (PdfTextBlock block in TextBlocks)
            {
                if (!block.Contains(x, y))
                {
                    continue;
                }

                PdfTextLine? candidate = block.FindTextLineOver(x, y);
                if (candidate is not null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public IEnumerable<PdfWord> GetWords(PdfWord start, PdfWord end)
        {
            System.Diagnostics.Debug.Assert(start.IndexInPage <= end.IndexInPage);

            if (TextBlocks is null || TextBlocks.Count == 0)
            {
                yield break;
            }

            // Handle single word selected
            if (start == end)
            {
                yield return start;
                yield break;
            }

            // Handle whole page selected
            if (start.IndexInPage == 0 && end.IndexInPage == this.Count - 1)
            {
                foreach (PdfWord word in this)
                {
                    yield return word;
                }

                yield break;
            }

            // Handle single block
            if (start.TextBlockIndex == end.TextBlockIndex)
            {
                PdfTextBlock block = TextBlocks[start.TextBlockIndex];

                int lineStartIndex = block.TextLines[0].IndexInPage;

                int startIndex = start.TextLineIndex - lineStartIndex;
                int endIndex = end.TextLineIndex - lineStartIndex;

                for (int l = startIndex; l <= endIndex; ++l)
                {
                    PdfTextLine line = block.TextLines[l];
                    if (l == startIndex)
                    {
                        // we are in first line
                        int wordIndex = start.IndexInPage - line.WordStartIndex;

                        // Check if there is a single line selected
                        int lastIndex = start.TextLineIndex != end.TextLineIndex ? line.Words.Count : end.IndexInPage - line.WordStartIndex + 1;

                        for (int i = wordIndex; i < lastIndex; ++i)
                        {
                            yield return line.Words[i];
                        }
                    }
                    else if (l == endIndex)
                    {
                        // we are in last line
                        int wordIndex = end.IndexInPage - line.WordStartIndex;
                        for (int i = 0; i <= wordIndex; ++i)
                        {
                            yield return line.Words[i];
                        }
                    }
                    else
                    {
                        foreach (PdfWord word in line.Words)
                        {
                            yield return word;
                        }
                    }
                }

                yield break;
            }

            for (int b = start.TextBlockIndex; b <= end.TextBlockIndex; ++b)
            {
                PdfTextBlock block = TextBlocks[b];

                int lineStartIndex = block.TextLines[0].IndexInPage;

                if (b == start.TextBlockIndex)
                {
                    // we are in first block
                    int startIndex = start.TextLineIndex - lineStartIndex;
                    for (int l = startIndex; l < block.TextLines.Count; ++l)
                    {
                        PdfTextLine line = block.TextLines[l];
                        if (l == startIndex)
                        {
                            // we are in first line (no need to check if there is a
                            // single line selected as it's not possible)
                            int wordIndex = start.IndexInPage - line.WordStartIndex;
                            for (int i = wordIndex; i < line.Words.Count; ++i)
                            {
                                yield return line.Words[i];
                            }
                        }
                        else
                        {
                            foreach (PdfWord word in line.Words)
                            {
                                yield return word;
                            }
                        }
                    }
                }
                else if (b == end.TextBlockIndex)
                {
                    // we are in last block
                    int endIndex = end.TextLineIndex - lineStartIndex;
                    for (int l = 0; l <= endIndex; ++l)
                    {
                        var line = block.TextLines[l];
                        if (l == endIndex)
                        {
                            // we are in last line
                            int wordIndex = end.IndexInPage - line.WordStartIndex;
                            for (int i = 0; i <= wordIndex; ++i)
                            {
                                yield return line.Words[i];
                            }
                        }
                        else
                        {
                            foreach (PdfWord word in line.Words)
                            {
                                yield return word;
                            }
                        }
                    }
                }
                else
                {
                    // we are in a block in the middle
                    foreach (PdfTextLine line in block.TextLines)
                    {
                        foreach (PdfWord word in line.Words)
                        {
                            yield return word;
                        }
                    }
                }
            }
        }

        public IEnumerator<PdfWord> GetEnumerator()
        {
            if (TextBlocks is null)
            {
                yield break;
            }

            foreach (PdfTextBlock block in TextBlocks)
            {
                foreach (PdfTextLine line in block.TextLines)
                {
                    foreach (PdfWord word in line.Words)
                    {
                        yield return word;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get; }

        public PdfWord this[int index]
        {
            get
            {
                System.Diagnostics.Debug.Assert(Count > 0);
                System.Diagnostics.Debug.Assert(index >= 0 && index < Count);

                if (TextBlocks is null)
                {
                    throw new NullReferenceException($"Cannot access word at index {index} because TextBlocks is null.");
                }

                foreach (PdfTextBlock block in TextBlocks)
                {
                    if (block.ContainsWord(index))
                    {
                        return block.GetWordInPageAt(index);
                    }
                }

                throw new NullReferenceException($"Cannot find word at index {index}.");
            }
        }
    }
}
