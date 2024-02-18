using System.Collections;

namespace Caly.Pdf.Models
{
    public sealed record PdfTextLayer : IReadOnlyList<PdfWord>
    {
        internal static readonly PdfTextLayer Empty = new(Array.Empty<PdfTextBlock>());

        public PdfTextLayer(IReadOnlyList<PdfTextBlock>? textBlocks)
        {
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
        public IReadOnlyList<PdfTextBlock>? TextBlocks { get; }

        public PdfWord? FindWordOver(double x, double y)
        {
            if (TextBlocks is null || TextBlocks.Count == 0) return null;

            foreach (PdfTextBlock block in TextBlocks)
            {
                var candidate = block.FindWordOver(x, y);
                if (candidate != null)
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
                var candidate = block.FindTextLineOver(x, y);
                if (candidate != null)
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
                foreach (var word in this)
                {
                    yield return word;
                }
                yield break;
            }

            // Handle single block
            if (start.TextBlockIndex == end.TextBlockIndex)
            {
                var block = TextBlocks[start.TextBlockIndex];

                int lineStartIndex = block.TextLines[0].IndexInPage;

                int startIndex = start.TextLineIndex - lineStartIndex;
                int endIndex = end.TextLineIndex - lineStartIndex;

                for (int l = startIndex; l <= endIndex; ++l)
                {
                    var line = block.TextLines[l];
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
                        foreach (var word in line.Words)
                        {
                            yield return word;
                        }
                    }
                }

                yield break;
            }

            for (int b = start.TextBlockIndex; b <= end.TextBlockIndex; ++b)
            {
                var block = TextBlocks[b];

                int lineStartIndex = block.TextLines[0].IndexInPage;

                if (b == start.TextBlockIndex)
                {
                    // we are in first block
                    int startIndex = start.TextLineIndex - lineStartIndex;
                    for (int l = startIndex; l < block.TextLines.Count; ++l)
                    {
                        var line = block.TextLines[l];
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
                            foreach (var word in line.Words)
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
                            foreach (var word in line.Words)
                            {
                                yield return word;
                            }
                        }
                    }
                }
                else
                {
                    // we are in a block in the middle
                    foreach (var line in block.TextLines)
                    {
                        foreach (var word in line.Words)
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

            foreach (var block in TextBlocks)
            {
                if (block?.TextLines is null) continue;

                foreach (var line in block.TextLines)
                {
                    if (line?.Words is null) continue;

                    foreach (var word in line.Words)
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

                var block = TextBlocks.FirstOrDefault(f => f.ContainsWord(index));
                return block.GetWordInPageAt(index);
            }
        }
    }
}
