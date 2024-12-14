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

using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Geometry;

namespace Caly.Pdf.Models
{
    public sealed class PdfTextBlock : IPdfTextElement
    {
        internal int WordStartIndex { get; set; }
        internal int WordEndIndex { get; set; }

        public TextOrientation TextOrientation { get; }

        private readonly PdfRectangle _boundingBox;
        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public ref readonly PdfRectangle BoundingBox => ref _boundingBox;

        public int IndexInPage { get; internal set; }

        /// <summary>
        /// The text lines contained in the block.
        /// </summary>
        public IReadOnlyList<PdfTextLine> TextLines { get; }

        public PdfTextBlock(IReadOnlyList<PdfTextLine> textLines)
        {
            ArgumentNullException.ThrowIfNull(textLines, nameof(textLines));

            if (textLines.Count == 0)
            {
                throw new ArgumentException("Cannot construct text block if no text line provided.", nameof(textLines));
            }

            TextLines = textLines;
            TextOrientation = PdfTextLayerHelper.GetTextOrientation(textLines);

            _boundingBox = TextOrientation switch
            {
                TextOrientation.Horizontal => GetBoundingBoxH(textLines),
                TextOrientation.Rotate180 => GetBoundingBox180(textLines),
                TextOrientation.Rotate90 => GetBoundingBox90(textLines),
                TextOrientation.Rotate270 => GetBoundingBox270(textLines),
                _ => GetBoundingBoxOther(textLines)
            };
        }

        public bool Contains(double x, double y)
        {
            return BoundingBox.Contains(new PdfPoint(x, y), true);
        }

        public PdfTextLine? FindTextLineOver(double x, double y)
        {
            if (TextLines.Count == 0)
            {
                return null;
            }

            System.Diagnostics.Debug.Assert(Contains(x, y));

            PdfPoint point = new PdfPoint(x, y);

            foreach (PdfTextLine line in TextLines)
            {
                if (line.BoundingBox.Contains(point, true))
                {
                    return line;
                }
            }

            return null;
        }

        public PdfWord? FindWordOver(double x, double y)
        {
            System.Diagnostics.Debug.Assert(Contains(x, y));

            PdfTextLine? line = FindTextLineOver(x, y);
            var word = line?.FindWordOver(x, y);

#if DEBUG
            if (word is not null)
            {
                System.Diagnostics.Debug.Assert(BoundingBox.Contains(new PdfPoint(x,y), true));
            }
#endif

            return word;
        }

        public PdfWord GetWordInPageAt(int indexInPage)
        {
            int index = indexInPage - WordStartIndex;

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(indexInPage));
            }

            int count = 0;
            foreach (PdfTextLine line in TextLines)
            {
                count += line.Words.Count;
                if (index < count)
                {
                    return line.GetWordInPageAt(indexInPage);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(indexInPage));
        }

        internal bool ContainsWord(int indexInPage)
        {
            return indexInPage >= WordStartIndex && indexInPage <= WordEndIndex;
        }

        #region Bounding box
        private static PdfRectangle GetBoundingBoxH(IReadOnlyList<PdfTextLine> lines)
        {
            double blX = double.MaxValue;
            double trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            double blY = double.MinValue;
            double trY = double.MaxValue;

            foreach (var line in lines)
            {
                if (line.BoundingBox.BottomLeft.X < blX)
                {
                    blX = line.BoundingBox.BottomLeft.X;
                }

                if (line.BoundingBox.BottomLeft.Y > blY)
                {
                    blY = line.BoundingBox.BottomLeft.Y;
                }

                double right = line.BoundingBox.BottomLeft.X + line.BoundingBox.Width;
                if (right > trX)
                {
                    trX = right;
                }

                if (line.BoundingBox.TopLeft.Y < trY)
                {
                    trY = line.BoundingBox.TopLeft.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox180(IReadOnlyList<PdfTextLine> lines)
        {
            double blX = double.MinValue;
            double trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            double blY = double.MaxValue;
            double trY = double.MinValue;

            foreach (var line in lines)
            {
                if (line.BoundingBox.BottomLeft.X > blX)
                {
                    blX = line.BoundingBox.BottomLeft.X;
                }

                if (line.BoundingBox.BottomLeft.Y < blY)
                {
                    blY = line.BoundingBox.BottomLeft.Y;
                }

                double right = line.BoundingBox.BottomLeft.X - line.BoundingBox.Width;
                if (right < trX)
                {
                    trX = right;
                }

                if (line.BoundingBox.TopRight.Y > trY)
                {
                    trY = line.BoundingBox.TopRight.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox90(IReadOnlyList<PdfTextLine> lines)
        {
            var b = double.MaxValue;
            var r = double.MaxValue;
            var t = double.MinValue;
            var l = double.MinValue;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.BoundingBox.TopLeft.X < b)
                {
                    b = line.BoundingBox.TopLeft.X;
                }

                if (line.BoundingBox.BottomRight.Y < r)
                {
                    r = line.BoundingBox.BottomRight.Y;
                }

                var right = line.BoundingBox.BottomRight.X;
                if (right > t)
                {
                    t = right;
                }

                if (line.BoundingBox.BottomLeft.Y > l)
                {
                    l = line.BoundingBox.BottomLeft.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(b, l), new PdfPoint(b, r),
                new PdfPoint(t, l), new PdfPoint(t, r));
        }

        private static PdfRectangle GetBoundingBox270(IReadOnlyList<PdfTextLine> lines)
        {
            var t = double.MaxValue;
            var b = double.MinValue;
            var l = double.MaxValue;
            var r = double.MinValue;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.BoundingBox.TopLeft.X > b)
                {
                    b = line.BoundingBox.TopLeft.X;
                }

                if (line.BoundingBox.BottomLeft.Y < l)
                {
                    l = line.BoundingBox.BottomLeft.Y;
                }

                var right = line.BoundingBox.BottomRight.X;
                if (right < t)
                {
                    t = right;
                }

                if (line.BoundingBox.BottomRight.Y > r)
                {
                    r = line.BoundingBox.BottomRight.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(b, l), new PdfPoint(b, r),
                new PdfPoint(t, l), new PdfPoint(t, r));
        }

        private static PdfRectangle GetBoundingBoxOther(IReadOnlyList<PdfTextLine> lines)
        {
            if (lines.Count == 1)
            {
                return lines[0].BoundingBox;
            }

            IEnumerable<PdfPoint> points = lines.SelectMany(l => new[]
            {
                l.BoundingBox.BottomLeft,
                l.BoundingBox.BottomRight,
                l.BoundingBox.TopLeft,
                l.BoundingBox.TopRight
            });

            // Candidates bounding boxes
            PdfRectangle mar = GeometryExtensions.MinimumAreaRectangle(points);

            PdfRectangle obb = new PdfRectangle(mar.TopRight, mar.TopLeft, mar.BottomRight, mar.BottomLeft);
            PdfRectangle obb1 = new PdfRectangle(obb.BottomLeft, obb.TopLeft, obb.BottomRight, obb.TopRight);
            PdfRectangle obb2 = new PdfRectangle(obb.BottomRight, obb.BottomLeft, obb.TopRight, obb.TopLeft);
            PdfRectangle obb3 = new PdfRectangle(obb.TopRight, obb.BottomRight, obb.TopLeft, obb.BottomLeft);

            // Find the orientation of the OBB, using the baseline angle
            // Assumes line order is correct
            PdfTextLine lastLine = lines[lines.Count - 1];

            double baseLineAngle = Distances.BoundAngle180(Distances.Angle(lastLine.BoundingBox.BottomLeft, lastLine.BoundingBox.BottomRight));

            double deltaAngle = Math.Abs(Distances.BoundAngle180(obb.Rotation - baseLineAngle));
            double deltaAngle1 = Math.Abs(Distances.BoundAngle180(obb1.Rotation - baseLineAngle));
            if (deltaAngle1 < deltaAngle)
            {
                deltaAngle = deltaAngle1;
                obb = obb1;
            }

            double deltaAngle2 = Math.Abs(Distances.BoundAngle180(obb2.Rotation - baseLineAngle));
            if (deltaAngle2 < deltaAngle)
            {
                deltaAngle = deltaAngle2;
                obb = obb2;
            }

            double deltaAngle3 = Math.Abs(Distances.BoundAngle180(obb3.Rotation - baseLineAngle));
            if (deltaAngle3 < deltaAngle)
            {
                obb = obb3;
            }

            return obb;
        }
        #endregion
    }
}
