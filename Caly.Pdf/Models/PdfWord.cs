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

using System.Buffers;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Geometry;

namespace Caly.Pdf.Models
{
    public sealed class PdfWord : IPdfTextElement
    {
#if DEBUG
        public override string ToString()
        {
            if (Value.IsEmpty)
            {
                return string.Empty;
            }

            Span<char> output = Value.Length < 512 ? stackalloc char[(int)Value.Length] : new char[Value.Length];

            Value.CopyTo(output);
            return new string(output);
        }
#endif

        public TextOrientation TextOrientation { get; }

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public PdfRectangle BoundingBox { get; }

        /// <summary>
        /// Word index in the page.
        /// </summary>
        public int IndexInPage { get; internal set; }

        /// <summary>
        /// Text line index in the page the word belongs to.
        /// </summary>
        public int TextLineIndex { get; internal set; }

        /// <summary>
        /// Text block index in the page the word belongs to.
        /// </summary>
        public int TextBlockIndex { get; internal set; }

        public PdfRectangle[] LettersBoundingBoxes { get; }

        public ReadOnlySequence<char> Value { get; }

        public int Count { get; }

        public PdfWord(IReadOnlyList<PdfLetter> letters)
        {
            ArgumentNullException.ThrowIfNull(letters, nameof(letters));

            if (letters.Count == 0)
            {
                throw new ArgumentException("Cannot construct word if no letters provided.", nameof(letters));
            }

            TextOrientation = PdfTextLayerHelper.GetTextOrientation(letters);

            Count = letters.Count;

            LettersBoundingBoxes = new PdfRectangle[letters.Count];

            var firstLetter = letters[0];
            LettersBoundingBoxes[0] = firstLetter.BoundingBox;

            var first = new MemorySegment<char>(firstLetter.Value);
            var current = first;
            for (int i = 1; i < letters.Count; i++)
            {
                var letter = letters[i];
                LettersBoundingBoxes[i] = letter.BoundingBox;
                current = current.Append(letter.Value);
            }

            Value = new ReadOnlySequence<char>(first, 0, current, current.Memory.Length);

            switch (TextOrientation)
            {
                case TextOrientation.Horizontal:
                    BoundingBox = GetBoundingBoxH(letters);
                    break;

                case TextOrientation.Rotate180:
                    BoundingBox = GetBoundingBox180(letters);
                    break;

                case TextOrientation.Rotate90:
                    BoundingBox = GetBoundingBox90(letters);
                    break;

                case TextOrientation.Rotate270:
                    BoundingBox = GetBoundingBox270(letters);
                    break;

                default: // Other
                    BoundingBox = GetBoundingBoxOther(letters);
                    break;
            }
        }

        public bool Contains(double x, double y)
        {
            return BoundingBox.Contains(new PdfPoint(x, y), true);
        }

        public int FindLetterIndexOver(double x, double y)
        {
            if (LettersBoundingBoxes.Length == 0)
            {
                return -1;
            }

            var point = new PdfPoint(x, y);
            for (int i = 0; i < LettersBoundingBoxes.Length; i++)
            {
                if (LettersBoundingBoxes[i].Contains(point, true))
                {
                    return i;
                }
            }
            return -1;
        }

        public int FindNearestLetterIndex(double x, double y)
        {
            if (LettersBoundingBoxes.Length == 0)
            {
                return -1;
            }

            var point = new PdfPoint(x, y);
            double dist = double.MaxValue;
            int index = -1;

            for (int i = 0; i < LettersBoundingBoxes.Length; i++)
            {
                var letter = LettersBoundingBoxes[i];
                double localDist = Distances.Euclidean(point, letter.BottomRight);
                if (localDist < dist)
                {
                    dist = localDist;
                    index = i;
                }
            }
            return index;
        }

        #region Bounding box
        private static PdfRectangle GetBoundingBoxH(IReadOnlyList<PdfLetter> letters)
        {
            var blX = double.MaxValue;
            var trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MinValue;
            var trY = double.MaxValue;

            for (var i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];

                if (letter.StartBaseLine.X < blX)
                {
                    blX = letter.StartBaseLine.X;
                }

                if (letter.StartBaseLine.Y > blY)
                {
                    blY = letter.StartBaseLine.Y;
                }

                var right = letter.StartBaseLine.X + letter.BoundingBox.Width;
                if (right > trX)
                {
                    trX = right;
                }

                if (letter.BoundingBox.TopLeft.Y < trY)
                {
                    trY = letter.BoundingBox.TopLeft.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox180(IReadOnlyList<PdfLetter> letters)
        {
            var blX = double.MinValue;
            var trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MaxValue;
            var trY = double.MinValue;

            for (var i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];

                if (letter.StartBaseLine.X > blX)
                {
                    blX = letter.StartBaseLine.X;
                }

                if (letter.StartBaseLine.Y < blY)
                {
                    blY = letter.StartBaseLine.Y;
                }

                var right = letter.StartBaseLine.X - letter.BoundingBox.Width;
                if (right < trX)
                {
                    trX = right;
                }

                if (letter.BoundingBox.TopRight.Y > trY)
                {
                    trY = letter.BoundingBox.TopRight.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox90(IReadOnlyList<PdfLetter> letters)
        {
            var t = double.MinValue;
            var b = double.MaxValue;

            var r = double.MaxValue;
            var l = double.MinValue;

            for (var i = 0; i < letters.Count; ++i)
            {
                var letter = letters[i];

                if (letter.StartBaseLine.X < b)
                {
                    b = letter.StartBaseLine.X;
                }

                if (letter.EndBaseLine.Y < r)
                {
                    r = letter.EndBaseLine.Y;
                }

                var right = letter.StartBaseLine.X - letter.BoundingBox.Height;
                if (right > t)
                {
                    t = right;
                }

                if (letter.BoundingBox.BottomLeft.Y > l)
                {
                    l = letter.BoundingBox.BottomLeft.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBox270(IReadOnlyList<PdfLetter> letters)
        {
            var t = double.MaxValue;
            var b = double.MinValue;
            var l = double.MaxValue;
            var r = double.MinValue;

            for (var i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];

                if (letter.StartBaseLine.X > b)
                {
                    b = letter.StartBaseLine.X;
                }

                if (letter.StartBaseLine.Y < l)
                {
                    l = letter.StartBaseLine.Y;
                }

                var right = letter.StartBaseLine.X + letter.BoundingBox.Height;
                if (right < t)
                {
                    t = right;
                }

                if (letter.BoundingBox.BottomRight.Y > r)
                {
                    r = letter.BoundingBox.BottomRight.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBoxOther(IReadOnlyList<PdfLetter> letters)
        {
            if (letters.Count == 1)
            {
                return letters[0].BoundingBox;
            }

            var baseLinePoints = letters.SelectMany(r => new[]
            {
                r.StartBaseLine,
                r.EndBaseLine,
            }).ToArray();

            // Fitting a line through the base lines points
            // to find the orientation (slope)
            double x0 = baseLinePoints.Average(p => p.X);
            double y0 = baseLinePoints.Average(p => p.Y);
            double sumProduct = 0;
            double sumDiffSquaredX = 0;

            for (int i = 0; i < baseLinePoints.Length; i++)
            {
                var point = baseLinePoints[i];
                var x_diff = point.X - x0;
                var y_diff = point.Y - y0;
                sumProduct += x_diff * y_diff;
                sumDiffSquaredX += x_diff * x_diff;
            }

            double cos = 0;
            double sin = 1;
            if (sumDiffSquaredX > 1e-3)
            {
                // not vertical line
                double angleRad = Math.Atan(sumProduct / sumDiffSquaredX); // -π/2 ≤ θ ≤ π/2
                cos = Math.Cos(angleRad);
                sin = Math.Sin(angleRad);
            }

            // Rotate the points to build the axis-aligned bounding box (AABB)
            var inverseRotation = new TransformationMatrix(
                cos, -sin, 0,
                sin, cos, 0,
                0, 0, 1);

            var transformedPoints = letters.SelectMany(r => new[]
            {
                r.StartBaseLine,
                r.EndBaseLine,
                r.BoundingBox.TopLeft,
                r.BoundingBox.TopRight
            }).Distinct().Select(p => inverseRotation.Transform(p));

            // Inverse Y axis - (0, 0) is top left
            var aabb = new PdfRectangle(transformedPoints.Min(p => p.X),
                                        transformedPoints.Max(p => p.Y),
                                        transformedPoints.Max(p => p.X),
                                        transformedPoints.Min(p => p.Y));

            // Rotate back the AABB to obtain to oriented bounding box (OBB)
            var rotateBack = new TransformationMatrix(
                cos, sin, 0,
                -sin, cos, 0,
                0, 0, 1);

            // Candidates bounding boxes
            var obb = rotateBack.Transform(aabb);
            var obb1 = new PdfRectangle(obb.BottomLeft, obb.TopLeft, obb.BottomRight, obb.TopRight);
            var obb2 = new PdfRectangle(obb.BottomRight, obb.BottomLeft, obb.TopRight, obb.TopLeft);
            var obb3 = new PdfRectangle(obb.TopRight, obb.BottomRight, obb.TopLeft, obb.BottomLeft);

            // Find the orientation of the OBB, using the baseline angle
            // Assumes word order is correct
            var firstLetter = letters[0];
            var lastLetter = letters[letters.Count - 1];

            var baseLineAngle = Math.Atan2(
                lastLetter.EndBaseLine.Y - firstLetter.StartBaseLine.Y,
                lastLetter.EndBaseLine.X - firstLetter.StartBaseLine.X) * 180 / Math.PI;

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
