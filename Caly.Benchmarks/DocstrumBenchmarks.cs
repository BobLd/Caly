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
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Caly.Benchmarks
{
    [MemoryDiagnoser]
    public class DocstrumBenchmarks
    {
        //private const string _path = "fseprd1102849.pdf";
        private const string _path = "2559 words.pdf";

        private readonly Word[] _words;
        private readonly PdfWord[] _calyWords;

        public DocstrumBenchmarks()
        {
            using (var doc = PdfDocument.Open(_path))
            {
                doc.AddPageFactory<PageTextLayerContent, TextLayerFactory>();

                var page = doc.GetPage(1);
                _words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToArray();

                var layer = doc.GetPage<PageTextLayerContent>(1);
                _calyWords = CalyNNWordExtractor.Instance.GetWords(layer.Letters, CancellationToken.None).ToArray();
            }
        }

        [Benchmark(Baseline = true)]
        public IReadOnlyList<TextBlock> PdfPig()
        {
            return DocstrumBoundingBoxes.Instance.GetBlocks(_words);
        }

        [Benchmark]
        public IReadOnlyList<PdfTextBlock> Caly()
        {
            return CalyDocstrum.Instance.GetBlocks(_calyWords, CancellationToken.None);
        }


        //[Benchmark]
        //public IReadOnlyList<PdfTextBlock> Caly2()
        //{
        //    return CalyDocstrum2.Instance.GetBlocks(_calyWords); //, CancellationToken.None);
        //}
    }
}