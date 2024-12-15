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

namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig - optimised
     */

    /// <summary>
    /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
    /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
    /// </summary>
    public static class CalyDuplicateOverlappingTextProcessor
    {
        /// <summary>
        /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
        /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
        /// </summary>
        /// <param name="letters">Letters to be processed.</param>
        /// <param name="token"/>
        /// <returns>Letters with no duplicate overlapping.</returns>
        public static IReadOnlyList<PdfLetter> Get(IReadOnlyList<PdfLetter> letters, CancellationToken token)
        {
            if (letters is null || letters.Count == 0)
            {
                return letters;
            }

            var cleanLetters = new List<PdfLetter>() { letters[0] };

            for (int i = 1; i < letters.Count; ++i)
            {
                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = letters[i];
                double tolerance = letter.BoundingBox.Width / (letter.Value.Length == 0 ? 1 : letter.Value.Length) / 3.0;
                double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                var duplicates = cleanLetters
                    .Where(l => minX <= l.BoundingBox.BottomLeft.X &&
                                maxX >= l.BoundingBox.BottomLeft.X &&
                                minY <= l.BoundingBox.BottomLeft.Y &&
                                maxY >= l.BoundingBox.BottomLeft.Y); // do other checks?

                var duplicatesOverlapping = duplicates.Any(l => l.Value.Span.SequenceEqual(letter.Value.Span));

                if (!duplicatesOverlapping)
                {
                    cleanLetters.Add(letter);
                }
            }

            return cleanLetters;
        }

        /// <summary>
        /// Checks if each letter is a duplicate and overlaps any other letter and remove the duplicate, and flag the remaining as bold.
        /// <para>Logic inspired from PdfBox's PDFTextStripper class.</para>
        /// </summary>
        /// <param name="letters">Letters to be processed.</param>
        /// <param name="token"/>
        /// <returns>Letters with no duplicate overlapping.</returns>
        public static IReadOnlyList<PdfLetter> GetInPlace(IReadOnlyList<PdfLetter> letters, CancellationToken token)
        {
            if (letters is null || letters.Count == 0)
            {
                return letters;
            }

            if (letters is not List<PdfLetter> cleanLetters)
            {
                cleanLetters = letters.ToList();
            }

            for (int i = 0; i < cleanLetters.Count; ++i)
            {
                if (i % 1000 == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                var letter = cleanLetters[i];
                double tolerance = letter.BoundingBox.Width / (letter.Value.Length == 0 ? 1 : letter.Value.Length) / 3.0;
                double minX = letter.BoundingBox.BottomLeft.X - tolerance;
                double maxX = letter.BoundingBox.BottomLeft.X + tolerance;
                double minY = letter.BoundingBox.BottomLeft.Y - tolerance;
                double maxY = letter.BoundingBox.BottomLeft.Y + tolerance;

                var duplicatesOverlapping = cleanLetters
                    .Skip(i + 1)
                    .Any(l => minX <= l.BoundingBox.BottomLeft.X &&
                              maxX >= l.BoundingBox.BottomLeft.X &&
                              minY <= l.BoundingBox.BottomLeft.Y &&
                              maxY >= l.BoundingBox.BottomLeft.Y &&
                              l.Value.Span.SequenceEqual(letter.Value.Span));

                if (!duplicatesOverlapping)
                {
                    continue;
                }

                cleanLetters.RemoveAt(i);
                i--;
            }

            return cleanLetters;
        }
    }
}
