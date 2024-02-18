using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Geometry;

namespace Caly.Pdf.Models
{
    public sealed record PdfTextLine : IPdfTextElement
    {
#if DEBUG
        public override string ToString()
        {
            return string.Join(' ', Words.Select(l => l.ToString()));
        }
#endif

        internal int WordStartIndex { get; set; }

        public TextOrientation TextOrientation { get; }

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public PdfRectangle BoundingBox { get; }

        /// <summary>
        /// Text line index in the page.
        /// </summary>
        public int IndexInPage { get; internal set; }

        /// <summary>
        /// Text block index in the page the text line belongs to.
        /// </summary>
        public int TextBlockIndex { get; internal set; }

        /// <summary>
        /// The words contained in the line.
        /// </summary>
        public IReadOnlyList<PdfWord> Words { get; }

        public PdfTextLine(IReadOnlyList<PdfWord> words, PdfRectangle boundingBox, int indexInPage, int textBlockIndex, int wordStartIndex)
        {
            ArgumentNullException.ThrowIfNull(words, nameof(words));

            if (words.Count == 0)
            {
                throw new ArgumentException("Cannot construct text line if no word provided.", nameof(words));
            }

            Words = words;
            BoundingBox = boundingBox;
            IndexInPage = indexInPage;
            TextBlockIndex = textBlockIndex;
            WordStartIndex = wordStartIndex;

            if (Words.Count == 1)
            {
                // This is not correct
                BoundingBox = Words[0].BoundingBox;
                TextOrientation = Words[0].TextOrientation;
            }
            else
            {
                TextOrientation = PdfTextLayerHelper.GetTextOrientation(words);

                switch (TextOrientation)
                {
                    case TextOrientation.Horizontal:
                        BoundingBox = GetBoundingBoxH(words);
                        break;

                    case TextOrientation.Rotate180:
                        BoundingBox = GetBoundingBox180(words);
                        break;

                    case TextOrientation.Rotate90:
                        BoundingBox = GetBoundingBox90(words);
                        break;

                    case TextOrientation.Rotate270:
                        BoundingBox = GetBoundingBox270(words);
                        break;

                    default: // Other
                        BoundingBox = GetBoundingBoxOther(words);
                        break;
                }
            }

            //BoundingBox = boundingBox;

            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.TopLeft.X - BoundingBox.TopLeft.X) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.TopLeft.Y - BoundingBox.TopLeft.Y) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.TopRight.X - BoundingBox.TopRight.X) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.TopRight.Y - BoundingBox.TopRight.Y) < 10e-5);

            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.BottomLeft.X - BoundingBox.BottomLeft.X) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.BottomLeft.Y - BoundingBox.BottomLeft.Y) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.BottomRight.X - BoundingBox.BottomRight.X) < 10e-5);
            //System.Diagnostics.Debug.Assert(Math.Abs(boundingBox.BottomRight.Y - BoundingBox.BottomRight.Y) < 10e-5);
        }

        public bool Contains(double x, double y)
        {
            return BoundingBox.Contains(new PdfPoint(x, y), true);
        }

        public PdfWord? FindWordOver(double x, double y)
        {
            if (Words == null || Words.Count == 0)
            {
                return null;
            }

            var point = new PdfPoint(x, y);
            foreach (var word in Words)
            {
                if (word.BoundingBox.Contains(point, true))
                {
                    return word;
                }
            }

            return null;
        }

        public PdfWord? FindNearestWord(double x, double y)
        {
            if (Words == null || Words.Count == 0)
            {
                return null;
            }

            // TODO - Improve performance
            var point = new PdfPoint(x, y);
            double dist = double.MaxValue;
            PdfWord? w = null;

            foreach (var word in Words)
            {
                double localDist = Math.Min(Distances.Euclidean(point, word.BoundingBox.BottomLeft),
                                            Distances.Euclidean(point, word.BoundingBox.BottomRight));
                if (localDist < dist)
                {
                    dist = localDist;
                    w = word;
                }
            }

            return w;
        }

        public PdfLetter? FindLetterOver(double x, double y)
        {
            return FindWordOver(x, y)?.FindLetterOver(x, y);
        }

        public PdfWord GetWordInPageAt(int indexInPage)
        {
            int indexInLine = indexInPage - WordStartIndex;

            if (indexInLine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(indexInPage));
            }

            if (indexInLine > Words.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(indexInPage));
            }

            return Words[indexInLine];
        }

        #region Bounding box
        private static PdfRectangle GetBoundingBoxH(IReadOnlyList<PdfWord> words)
        {
            var blX = double.MaxValue;
            var trX = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MinValue;
            var trY = double.MaxValue;

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.BoundingBox.BottomLeft.X < blX)
                {
                    blX = word.BoundingBox.BottomLeft.X;
                }

                if (word.BoundingBox.BottomLeft.Y > blY)
                {
                    blY = word.BoundingBox.BottomLeft.Y;
                }

                var right = word.BoundingBox.BottomLeft.X + word.BoundingBox.Width;
                if (right > trX)
                {
                    trX = right;
                }

                if (word.BoundingBox.TopLeft.Y < trY)
                {
                    trY = word.BoundingBox.TopLeft.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox180(IReadOnlyList<PdfWord> words)
        {
            var blX = double.MinValue;
            var trX = double.MaxValue;

            // Inverse Y axis - (0, 0) is top left
            var blY = double.MaxValue;
            var trY = double.MinValue;

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.BoundingBox.BottomLeft.X > blX)
                {
                    blX = word.BoundingBox.BottomLeft.X;
                }

                if (word.BoundingBox.BottomLeft.Y < blY)
                {
                    blY = word.BoundingBox.BottomLeft.Y;
                }

                var right = word.BoundingBox.BottomLeft.X - word.BoundingBox.Width;
                if (right < trX)
                {
                    trX = right;
                }

                if (word.BoundingBox.TopRight.Y > trY)
                {
                    trY = word.BoundingBox.TopRight.Y;
                }
            }

            return new PdfRectangle(blX, blY, trX, trY);
        }

        private static PdfRectangle GetBoundingBox90(IReadOnlyList<PdfWord> words)
        {
            var b = double.MaxValue;
            var t = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var r = double.MinValue;
            var l = double.MaxValue;

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.BoundingBox.BottomLeft.X < b)
                {
                    b = word.BoundingBox.BottomLeft.X;
                }

                if (word.BoundingBox.BottomRight.Y > r)
                {
                    r = word.BoundingBox.BottomRight.Y;
                }

                var right = word.BoundingBox.BottomLeft.X + word.BoundingBox.Height;
                if (right > t)
                {
                    t = right;
                }

                if (word.BoundingBox.BottomLeft.Y < l)
                {
                    l = word.BoundingBox.BottomLeft.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                                    new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBox270(IReadOnlyList<PdfWord> words)
        {
            var t = double.MaxValue;
            var b = double.MinValue;

            // Inverse Y axis - (0, 0) is top left
            var l = double.MinValue; // y
            var r = double.MaxValue; // y

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.BoundingBox.BottomLeft.X > b)
                {
                    b = word.BoundingBox.BottomLeft.X;
                }

                if (word.BoundingBox.BottomLeft.Y > l)
                {
                    l = word.BoundingBox.BottomLeft.Y;
                }

                var right = word.BoundingBox.BottomLeft.X - word.BoundingBox.Height;
                if (right < t)
                {
                    t = right;
                }

                if (word.BoundingBox.BottomRight.Y < r)
                {
                    r = word.BoundingBox.BottomRight.Y;
                }
            }

            return new PdfRectangle(new PdfPoint(t, l), new PdfPoint(t, r),
                                    new PdfPoint(b, l), new PdfPoint(b, r));
        }

        private static PdfRectangle GetBoundingBoxOther(IReadOnlyList<PdfWord> words)
        {
            if (words.Count == 1)
            {
                return words[0].BoundingBox;
            }

            var baseLinePoints = words.SelectMany(r => new[]
            {
                r.BoundingBox.BottomLeft,
                r.BoundingBox.BottomRight,
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
                // not a vertical line
                double angleRad = Math.Atan(sumProduct / sumDiffSquaredX); // -π/2 ≤ θ ≤ π/2
                cos = Math.Cos(angleRad);
                sin = Math.Sin(angleRad);
            }

            // Rotate the points to build the axis-aligned bounding box (AABB)
            var inverseRotation = new TransformationMatrix(
                cos, -sin, 0,
                sin, cos, 0,
                0, 0, 1);

            var transformedPoints = words.SelectMany(r => new[]
            {
                r.BoundingBox.BottomLeft,
                r.BoundingBox.BottomRight,
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
            var firstWord = words[0];
            var lastWord = words[words.Count - 1];

            var baseLineAngle = Distances.Angle(firstWord.BoundingBox.BottomLeft, lastWord.BoundingBox.BottomRight);

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
