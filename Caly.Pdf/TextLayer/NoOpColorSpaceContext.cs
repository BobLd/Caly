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

using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Tokens;

namespace Caly.Pdf.TextLayer
{
    internal sealed class NoOpColorSpaceContext : IColorSpaceContext
    {
        public static readonly NoOpColorSpaceContext Instance = new();

        public ColorSpaceDetails CurrentStrokingColorSpace => DeviceGrayColorSpaceDetails.Instance;

        public ColorSpaceDetails CurrentNonStrokingColorSpace => DeviceGrayColorSpaceDetails.Instance;

        public void SetStrokingColorspace(NameToken colorspace, DictionaryToken? dictionary = null)
        {
            // No op
        }

        public void SetNonStrokingColorspace(NameToken colorspace, DictionaryToken? dictionary = null)
        {
            // No op
        }

        public void SetStrokingColor(IReadOnlyList<double> operands, NameToken? patternName = null)
        {
            // No op
        }

        public void SetStrokingColorGray(double gray)
        {
            // No op
        }

        public void SetStrokingColorRgb(double r, double g, double b)
        {
            // No op
        }

        public void SetStrokingColorCmyk(double c, double m, double y, double k)
        {
            // No op
        }

        public void SetNonStrokingColor(IReadOnlyList<double> operands, NameToken? patternName = null)
        {
            // No op
        }

        public void SetNonStrokingColorGray(double gray)
        {
            // No op
        }

        public void SetNonStrokingColorRgb(double r, double g, double b)
        {
            // No op
        }

        public void SetNonStrokingColorCmyk(double c, double m, double y, double k)
        {
            // No op
        }

        public IColorSpaceContext DeepClone()
        {
            return Instance;
        }
    }
}
