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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Geometry;

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

        public static StreamGeometry? GetGeometry(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex > -1);
            System.Diagnostics.Debug.Assert(endIndex > -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            PdfRectangle[] rects = new PdfRectangle[endIndex - startIndex + 1];

            for (int l = startIndex; l <= endIndex; ++l)
            {
                rects[l - startIndex] = word.LettersBoundingBoxes[l];
            }

            if (rects.Length == 1)
            {
                return GetGeometry(rects[0], true);
            }

            PdfRectangle bbox = word.TextOrientation switch
            {
                TextOrientation.Horizontal => GetBoundingBoxH(rects),
                TextOrientation.Rotate180 => GetBoundingBox180(rects),
                TextOrientation.Rotate90 => GetBoundingBox90(rects),
                TextOrientation.Rotate270 => GetBoundingBox270(rects),
                _ => GetBoundingBoxOther(rects)
            };

            return GetGeometry(bbox, true);
        }

        #region Bounding box - Same as PdfWord
        private static PdfRectangle GetBoundingBoxH(PdfRectangle[] letters)
        {
            var blX = double.MaxValue;
            var trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MinValue;
            var trY = double.MaxValue;

            for (var i = 0; i < letters.Length; i++)
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

        private static PdfRectangle GetBoundingBox180(PdfRectangle[] letters)
        {
            var blX = double.MinValue;
            var trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MaxValue;
            var trY = double.MinValue;

            for (var i = 0; i < letters.Length; i++)
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

        private static PdfRectangle GetBoundingBox90(PdfRectangle[] letters)
        {
            var b = double.MaxValue; // x
            var t = double.MinValue; // x

            // Inverse Y axis - (0, 0) is top left
            var r = double.MinValue; // y
            var l = double.MaxValue; // y

            for (var i = 0; i < letters.Length; i++)
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

        private static PdfRectangle GetBoundingBox270(PdfRectangle[] letters)
        {
            var t = double.MaxValue;
            var b = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var l = double.MinValue; // y
            var r = double.MaxValue; // y

            for (var i = 0; i < letters.Length; i++)
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

        private static PdfRectangle GetBoundingBoxOther(PdfRectangle[] letters)
        {
            var baseLinePoints = letters.SelectMany(r => new[]
            {
                r.BottomLeft,
                r.BottomRight,
            }).ToList();

            // Fitting a line through the base lines points
            // to find the orientation (slope)
            double x0 = baseLinePoints.Average(p => p.X);
            double y0 = baseLinePoints.Average(p => p.Y);
            double sumProduct = 0;
            double sumDiffSquaredX = 0;

            for (int i = 0; i < baseLinePoints.Count; i++)
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
            var lastLetter = letters[letters.Length - 1];

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
        }
        #endregion
    }
}
