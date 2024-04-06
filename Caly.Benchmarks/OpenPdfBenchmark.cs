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

using BenchmarkDotNet.Attributes;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class OpenPdfBenchmark
    {
        private const string _path = "fseprd1102849.pdf";

        [Benchmark(Baseline = true)]
        public IReadOnlyList<IPdfImage> PdfPig()
        {
            using (var doc = PdfDocument.Open(_path))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var page = doc.GetPage(1);
                return page.GetImages().ToArray();
            }
        }
    }
}
