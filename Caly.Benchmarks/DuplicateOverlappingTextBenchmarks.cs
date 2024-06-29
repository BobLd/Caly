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
using Caly.Pdf.Layout;
using Caly.Pdf.Models;
using Caly.Pdf.PageFactories;
using UglyToad.PdfPig;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class DuplicateOverlappingTextBenchmarks
    {
        private const string _pathNoDup = "2559 words.pdf";
        private const string _pathDup = "Document-PublisherError-1.pdf";

        private readonly IReadOnlyList<PdfLetter> _calyWordsNoDup;
        private readonly IReadOnlyList<PdfLetter> _calyWordsDup;

        public DuplicateOverlappingTextBenchmarks()
        {
            using (var doc = PdfDocument.Open(_pathNoDup))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWordsNoDup = layer.Letters;
            }

            using (var doc = PdfDocument.Open(_pathDup))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWordsDup = layer.Letters;
            }
        }

        [Benchmark]
        public IReadOnlyList<PdfLetter> BaseNoDup()
        {
            return CalyDuplicateOverlappingTextProcessor.Get(_calyWordsNoDup);
        }

        [Benchmark]
        public IReadOnlyList<PdfLetter> BaseDup()
        {
            return CalyDuplicateOverlappingTextProcessor.Get(_calyWordsDup);
        }
    }
}
