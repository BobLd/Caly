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
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;

namespace Caly.Core.Utilities
{
    internal static class PdfPointExtensions
    {
        // https://stackoverflow.com/questions/54009832/scala-orthogonal-projection-of-a-point-onto-a-line
        /* Projects point `p` on line going through two points `line1` and `line2`. */
        public static PdfPoint? ProjectPointOnLine(this in PdfPoint p, in PdfPoint line1, in PdfPoint line2, out double s)
        {
            PdfPoint v = p.Subtract(line1);
            PdfPoint d = line2.Subtract(line1);

            double den = d.X * d.X + d.Y * d.Y;

            if (Math.Abs(den) <= double.Epsilon)
            {
                s = 0;
                return null;
            }
            s = v.DotProduct(d) / den;

            return line1.Add(new PdfPoint(d.X * s, d.Y * s));
        }

        public static double ProjectPointOnLineM(this in PdfPoint p, in PdfPoint line1, in PdfPoint line2)
        {
            PdfPoint v = p.Subtract(line1);
            PdfPoint d = line2.Subtract(line1);

            double den = d.X * d.X + d.Y * d.Y;

            if (Math.Abs(den) <= double.Epsilon)
            {
                return 0;
            }

            return v.DotProduct(d) / den;
        }
    }
}
