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
    public sealed record PdfLetter : IPdfTextElement
    {
        public ReadOnlyMemory<char> Value { get; init; }

        public TextOrientation TextOrientation { get; }

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public PdfRectangle BoundingBox { get; }

        /// <summary>
        /// The placement position of the character in PDF space. See <see cref="StartBaseLine"/>
        /// </summary>
        public PdfPoint Location => StartBaseLine;

        /// <summary>
        /// The placement position of the character in PDF space (the start point of the baseline). See <see cref="Location"/>
        /// </summary>
        public PdfPoint StartBaseLine => BoundingBox.BottomLeft;

        /// <summary>
        /// The end point of the baseline.
        /// </summary>
        public PdfPoint EndBaseLine => BoundingBox.BottomRight;

        /// <summary>
        /// The width occupied by the character within the PDF content.
        /// </summary>
        public double Width => BoundingBox.Width;

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
            Value = value;
            BoundingBox = boundingBox;
            PointSize = pointSize;
            TextSequence = textSequence;

            TextOrientation = GetTextOrientation();
        }

        private TextOrientation GetTextOrientation()
        {
            if (Math.Abs(StartBaseLine.Y - EndBaseLine.Y) < 10e-5)
            {
                if (StartBaseLine.X > EndBaseLine.X)
                {
                    return TextOrientation.Rotate180;
                }

                return TextOrientation.Horizontal;
            }

            if (Math.Abs(StartBaseLine.X - EndBaseLine.X) < 10e-5)
            {
                // Inverse Y axis - (0, 0) is top left
                if (StartBaseLine.Y < EndBaseLine.Y)
                {
                    return TextOrientation.Rotate90;
                }

                return TextOrientation.Rotate270;
            }

            return TextOrientation.Other;
        }
    }
}
