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

using System;
using System.Buffers;
using Avalonia;
using Avalonia.Media;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Caly.Core.Utilities
{
    internal static class PdfWordHelpers
    {
        public static StreamGeometry GetGeometry(PdfRectangle rect, bool isFilled = false)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(rect.BottomLeft.X, rect.BottomLeft.Y), isFilled);
                ctx.LineTo(new Point(rect.TopLeft.X, rect.TopLeft.Y));
                ctx.LineTo(new Point(rect.TopRight.X, rect.TopRight.Y));
                ctx.LineTo(new Point(rect.BottomRight.X, rect.BottomRight.Y));
                ctx.EndFigure(true);
            }

            return sg;
        }

        public static StreamGeometry GetGeometry(PdfWord word)
        {
            return GetGeometry(word.BoundingBox, true);
        }

        //private static readonly ArrayPool<PdfRectangle> _rectPool = ArrayPool<PdfRectangle>.Shared;

        public static StreamGeometry? GetGeometry(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex > -1);
            System.Diagnostics.Debug.Assert(endIndex > -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            int length = endIndex - startIndex + 1;

            Span<PdfRectangle> rects = length < 128 ? stackalloc PdfRectangle[length]
                : new PdfRectangle[length]; // This allocates and could be improved using ArrayPool<T>

            //PdfRectangle[] rects = _rectPool.Rent(length);

            try
            {
                for (int l = startIndex; l <= endIndex; ++l)
                {
                    rects[l - startIndex] = word.LettersBoundingBoxes[l];
                }

                if (length == 1)
                {
                    return GetGeometry(rects[0], true);
                }

                PdfRectangle bbox = word.TextOrientation switch
                {
                    TextOrientation.Horizontal => GetBoundingBoxH(rects, length),
                    TextOrientation.Rotate180 => GetBoundingBox180(rects, length),
                    TextOrientation.Rotate90 => GetBoundingBox90(rects, length),
                    TextOrientation.Rotate270 => GetBoundingBox270(rects, length),
                    _ => GetBoundingBoxOther(rects, length)
                };

                return GetGeometry(bbox, true);
            }
            finally
            {
                //_rectPool.Return(rects);
            }
        }

        #region Bounding box - Same as PdfWord
        private static PdfRectangle GetBoundingBoxH(ReadOnlySpan<PdfRectangle> letters, int length)
        {
            var blX = double.MaxValue;
            var trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MinValue;
            var trY = double.MaxValue;

            for (var i = 0; i < length; i++)
            {
                var letter = letters[i];

                if (letter.BottomLeft.X < blX)
                {
                    blX = letter.BottomLeft.X;
                }

                if (letter.BottomLeft.Y > blY)
                {
                    blY = letter.BottomLeft.Y;
                }

                var right = letter.BottomLeft.X + letter.Width;
                if (right > trX)
                {
                    trX = right;
                }

                if (letter.TopLeft.Y < trY)
                {
                    trY = letter.TopLeft.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox180(ReadOnlySpan<PdfRectangle> letters, int length)
        {
            var blX = double.MinValue;
            var trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MaxValue;
            var trY = double.MinValue;

            for (var i = 0; i < length; i++)
            {
                var letter = letters[i];

                if (letter.BottomLeft.X > blX)
                {
                    blX = letter.BottomLeft.X;
                }

                if (letter.BottomLeft.Y < blY)
                {
                    blY = letter.BottomLeft.Y;
                }

                var right = letter.BottomLeft.X - letter.Width;
                if (right < trX)
                {
                    trX = right;
                }

                if (letter.TopRight.Y > trY)
                {
                    trY = letter.TopRight.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox90(ReadOnlySpan<PdfRectangle> letters, int length)
        {
            var b = double.MaxValue; // x
            var t = double.MinValue; // x

            // Inverse Y axis - (0, 0) is top left
            var r = double.MinValue; // y
            var l = double.MaxValue; // y

            for (var i = 0; i < length; i++)
            {
                var letter = letters[i];

                if (letter.BottomLeft.X < b)
                {
                    b = letter.BottomLeft.X;
                }

                if (letter.BottomRight.Y > r)
                {
                    r = letter.BottomRight.Y;
                }

                var right = letter.BottomLeft.X + letter.Height;
                if (right > t)
                {
                    t = right;
                }

                if (letter.BottomLeft.Y < l)
                {
                    l = letter.BottomLeft.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                                    new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBox270(ReadOnlySpan<PdfRectangle> letters, int length)
        {
            var t = double.MaxValue;
            var b = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var l = double.MinValue; // y
            var r = double.MaxValue; // y

            for (var i = 0; i < length; i++)
            {
                var letter = letters[i];

                if (letter.BottomLeft.X > b)
                {
                    b = letter.BottomLeft.X;
                }

                if (letter.BottomLeft.Y > l)
                {
                    l = letter.BottomLeft.Y;
                }

                var right = letter.BottomLeft.X - letter.Height;
                if (right < t)
                {
                    t = right;
                }

                if (letter.BottomRight.Y < r)
                {
                    r = letter.BottomRight.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                                    new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBoxOther(ReadOnlySpan<PdfRectangle> letters, int length)
        {
            throw new Exception();
            /*
            Span<PdfPoint> baseLinePoints = (length * 2) < 128 ? stackalloc PdfPoint[length * 2]
                : new PdfPoint[length * 2]; // This allocates and could be improved using ArrayPool<T>

            // Fitting a line through the base lines points
            // to find the orientation (slope)
            double x0 = 0;
            double y0 = 0;

            for (var i = 0; i < length; ++i)
            {
                var r = letters[i];
                baseLinePoints[i * 2] = r.BottomLeft;
                baseLinePoints[i * 2 + 1] = r.BottomRight;
                x0 += r.BottomLeft.X + r.BottomRight.X;
                y0 += r.BottomLeft.Y + r.BottomRight.Y;
            }

            x0 /= (length * 2);
            y0 /= (length * 2);

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

            var transformedPoints = letters.Take(length).SelectMany(r => new[]
            {
                r.BottomLeft,
                r.BottomRight,
                r.TopLeft,
                r.TopRight
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
            var lastLetter = letters[length - 1];

            var baseLineAngle = Math.Atan2(
                lastLetter.BottomRight.Y - firstLetter.BottomLeft.Y,
                lastLetter.BottomRight.X - firstLetter.BottomLeft.X) * 180 / Math.PI;

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
            */
        }
        #endregion
    }
}
