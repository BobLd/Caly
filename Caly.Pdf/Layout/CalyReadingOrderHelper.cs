using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;

namespace Caly.Pdf.Layout
{
    /*
     * From PdfPig
     */

    /// <summary>
    /// Helper functions for words and lines ordering.
    /// </summary>
    public static class CalyReadingOrderHelper
    {
        /// <summary>
        /// Order words by reading order in a line.
        /// <para>Assumes LtR and accounts for rotation.</para>
        /// </summary>
        /// <param name="words"></param>
        public static IEnumerable<PdfWord> OrderByReadingOrder(this IEnumerable<PdfWord> words)
        {
            if (words.Count() <= 1)
            {
                return words;
            }

            var textOrientation = words.First().TextOrientation;
            if (textOrientation != TextOrientation.Other)
            {
                foreach (var word in words)
                {
                    if (word.TextOrientation != textOrientation)
                    {
                        textOrientation = TextOrientation.Other;
                        break;
                    }
                }
            }

            switch (textOrientation)
            {
                case TextOrientation.Horizontal:
                    return words.OrderBy(w => w.BoundingBox.BottomLeft.X);

                case TextOrientation.Rotate180:
                    return words.OrderByDescending(w => w.BoundingBox.BottomLeft.X);

                case TextOrientation.Rotate90:
                    // Inverse Y axis - (0, 0) is top left
                    return words.OrderByDescending(w => w.BoundingBox.BottomLeft.Y);

                case TextOrientation.Rotate270:
                    // Inverse Y axis - (0, 0) is top left
                    return words.OrderBy(w => w.BoundingBox.BottomLeft.Y);

                case TextOrientation.Other:
                default:
                    // We consider the words roughly have the same rotation.
                    var avgAngle = words.Average(w => w.BoundingBox.Rotation);
                    if (double.IsNaN(avgAngle))
                    {
                        throw new NotFiniteNumberException("OrderByReadingOrder: NaN bounding box rotation found when ordering words.", avgAngle);
                    }

                    if (0 < avgAngle && avgAngle <= 90)
                    {
                        // quadrant 1, 0 < θ < π/2
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = words.OrderBy(w => w.BoundingBox.BottomLeft.X)
                            .ThenByDescending(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }

                    if (90 < avgAngle && avgAngle <= 180)
                    {
                        // quadrant 2, π/2 < θ ≤ π
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = words.OrderByDescending(w => w.BoundingBox.BottomLeft.X)
                            .ThenByDescending(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }

                    if (-180 < avgAngle && avgAngle <= -90)
                    {
                        // quadrant 3, -π < θ < -π/2
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = words.OrderByDescending(w => w.BoundingBox.BottomLeft.X)
                            .ThenBy(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }

                    if (-90 < avgAngle && avgAngle <= 0)
                    {
                        // quadrant 4, -π/2 < θ < 0
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = words.OrderBy(w => w.BoundingBox.BottomLeft.X)
                            .ThenBy(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }

                    throw new ArgumentException("OrderByReadingOrder: unknown bounding box rotation found when ordering words.", nameof(avgAngle));
            }
        }

        /// <summary>
        /// Order lines by reading order in a block.
        /// <para>Assumes TtB and accounts for rotation.</para>
        /// </summary>
        /// <param name="lines"></param>
        public static IEnumerable<PdfTextLine> OrderByReadingOrder(this IEnumerable<PdfTextLine> lines)
        {
            if (lines.Count() <= 1)
            {
                return lines;
            }

            var textOrientation = lines.First().TextOrientation;
            if (textOrientation != TextOrientation.Other)
            {
                foreach (var line in lines)
                {
                    if (line.TextOrientation != textOrientation)
                    {
                        textOrientation = TextOrientation.Other;
                        break;
                    }
                }
            }

            switch (textOrientation)
            {
                case TextOrientation.Horizontal:
                    // Inverse Y axis - (0, 0) is top left
                    return lines.OrderBy(w => w.BoundingBox.BottomLeft.Y);

                case TextOrientation.Rotate180:
                    // Inverse Y axis - (0, 0) is top left
                    return lines.OrderByDescending(w => w.BoundingBox.BottomLeft.Y);

                case TextOrientation.Rotate90:
                    return lines.OrderBy(w => w.BoundingBox.BottomLeft.X);

                case TextOrientation.Rotate270:
                    return lines.OrderByDescending(w => w.BoundingBox.BottomLeft.X);

                case TextOrientation.Other:
                default:
                    // We consider the lines roughly have the same rotation.
                    var avgAngle = lines.Average(w => w.BoundingBox.Rotation);
                    if (double.IsNaN(avgAngle))
                    {
                        throw new NotFiniteNumberException("OrderByReadingOrder: NaN bounding box rotation found when ordering lines.", avgAngle);
                    }

                    if (0 < avgAngle && avgAngle <= 90)
                    {
                        // quadrant 1, 0 < θ < π/2
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = lines.OrderBy(w => w.BoundingBox.BottomLeft.Y).ThenBy(w => w.BoundingBox.BottomLeft.X);
                        return ordered;
                    }
                    
                    if (90 < avgAngle && avgAngle <= 180)
                    {
                        // quadrant 2, π/2 < θ ≤ π
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = lines.OrderByDescending(w => w.BoundingBox.BottomLeft.X).ThenByDescending(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }
                    
                    if (-180 < avgAngle && avgAngle <= -90)
                    {
                        // quadrant 3, -π < θ < -π/2
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = lines.OrderByDescending(w => w.BoundingBox.BottomLeft.Y).ThenByDescending(w => w.BoundingBox.BottomLeft.X);
                        return ordered;
                    }
                    
                    if (-90 < avgAngle && avgAngle <= 0)
                    {
                        // quadrant 4, -π/2 < θ < 0
                        // Inverse Y axis - (0, 0) is top left
                        var ordered = lines.OrderBy(w => w.BoundingBox.BottomLeft.X).ThenBy(w => w.BoundingBox.BottomLeft.Y);
                        return ordered;
                    }

                    throw new ArgumentException("OrderByReadingOrder: unknown bounding box rotation found when ordering lines.", nameof(avgAngle));
            }
        }
    }
}
