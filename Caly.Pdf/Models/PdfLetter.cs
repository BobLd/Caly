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

namespace Caly.Pdf.Models
{
    public sealed class PdfLetter : IPdfTextElement
    {
        private readonly ReadOnlyMemory<char> _value;
        public ref readonly ReadOnlyMemory<char> Value => ref _value;

        public TextOrientation TextOrientation { get; }

        private readonly PdfRectangle _boundingBox;

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public ref readonly PdfRectangle BoundingBox => ref _boundingBox;

        /// <summary>
        /// The placement position of the character in PDF space (the start point of the baseline).
        /// </summary>
        public PdfPoint StartBaseLine => BoundingBox.BottomLeft;

        /// <summary>
        /// The end point of the baseline.
        /// </summary>
        public PdfPoint EndBaseLine => BoundingBox.BottomRight;

        /// <summary>
        /// The size of the font in points.
        /// </summary>
        public double PointSize { get; }

        /// <summary>
        /// Sequence number of the ShowText operation that printed this letter.
        /// </summary>
        public int TextSequence { get; }

        public PdfLetter(ReadOnlyMemory<char> value, PdfRectangle boundingBox, double pointSize, int textSequence)
        {
            _value = value;
            _boundingBox = boundingBox;
            PointSize = pointSize;
            TextSequence = textSequence;

            TextOrientation = GetTextOrientation();
        }

        private TextOrientation GetTextOrientation()
        {
            if (Math.Abs(StartBaseLine.Y - EndBaseLine.Y) < 10e-5)
            {
                if (Math.Abs(StartBaseLine.X - EndBaseLine.X) < 10e-5)
                {
                    // Start and End point are the same
                    return GetTextOrientationRot();
                }

                if (StartBaseLine.X > EndBaseLine.X)
                {
                    return TextOrientation.Rotate180;
                }

                return TextOrientation.Horizontal;
            }

            if (Math.Abs(StartBaseLine.X - EndBaseLine.X) < 10e-5)
            {
                if (Math.Abs(StartBaseLine.Y - EndBaseLine.Y) < 10e-5)
                {
                    // Start and End point are the same
                    return GetTextOrientationRot();
                }

                if (StartBaseLine.Y > EndBaseLine.Y)
                {
                    return TextOrientation.Rotate90;
                }

                return TextOrientation.Rotate270;
            }

            return TextOrientation.Other;
        }

        private TextOrientation GetTextOrientationRot()
        {
            double rotation = BoundingBox.Rotation;

            if (Math.Abs(rotation % 90) >= 10e-5)
            {
                return TextOrientation.Other;
            }

            int rotationInt = (int)Math.Round(rotation, MidpointRounding.AwayFromZero);
            switch (rotationInt)
            {
                case 0:
                    return TextOrientation.Horizontal;

                case -90:
                    return TextOrientation.Rotate90;

                case 180:
                case -180:
                    return TextOrientation.Rotate180;

                case 90:
                    return TextOrientation.Rotate270;
            }

            throw new Exception($"Could not find TextOrientation for rotation '{rotation}'.");
        }
    }
}
