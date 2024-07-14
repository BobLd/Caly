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
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Lifti;
using Lifti.Tokenization;

namespace Caly.Core.Services
{
    internal sealed class LiftiTextSearchService : ITextSearchService
    {
        private readonly FullTextIndex<int> _index;

        /// <summary>
        /// The Caly <see cref="IndexTokenizer"/> that does not split tokens.
        /// </summary>
        private sealed class CalyIndexTokenizer : IndexTokenizer
        {
            public CalyIndexTokenizer(TokenizationOptions tokenizationOptions)
                : base(tokenizationOptions)
            {
            }

            public override bool IsSplitCharacter(char character)
            {
                return false;
            }
        }

        public LiftiTextSearchService()
        {
            _index = new FullTextIndexBuilder<int>()
                .WithObjectTokenization<PdfPageViewModel>(
                    options => options
                        .WithKey(p => p.PageNumber)
                        .WithField("w", async (p, ct) =>
                        {
                            return await Task.Run(async () =>
                            {
                                var textLayer = p.PdfTextLayer;
                                if (textLayer is null)
                                {
                                    await p.LoadInteractiveLayer(ct);
                                    textLayer = p.PdfTextLayer;
                                }

                                if (textLayer is null)
                                {
                                    throw new NullReferenceException("Cannot index search on a null PdfTextLayer.");
                                }

                                return textLayer.Select(w => string.Concat(w.Value));
                            }, ct);
                        }, tokenizationOptions: builder => builder.WithFactory(o => new CalyIndexTokenizer(o)))
                ).Build();
        }

        public async Task BuildPdfDocumentIndex(PdfDocumentViewModel pdfDocument, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            for (int i = 0; i < pdfDocument.PageCount; ++i)
            {
                if (i % 10 == 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (i > 0)
                    {
                        await _index.CommitBatchChangeAsync(token);
                    }
                    _index.BeginBatchChange();
                }

                await _index.AddAsync(pdfDocument.Pages[i], token);

                System.Diagnostics.Debug.Assert(pdfDocument.Pages[i].PdfTextLayer is not null);
                //System.Diagnostics.Debug.Assert(pdfDocument.Pages[i].PdfTextLayer!.Count == _index.Metadata.GetDocumentMetadata(i).DocumentStatistics.TotalTokenCount); // Can't do that with batch
            }

            await _index.CommitBatchChangeAsync(token);
        }

        public async Task<IEnumerable<TextSearchResultViewModel>> Search(PdfDocumentViewModel pdfDocument, string text, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            var results = _index.Search(text);

            return results.Select(r =>
                new TextSearchResultViewModel()
                {
                    PageNumber = r.Key,
                    Nodes = new ObservableCollection<TextSearchResultViewModel>(r.FieldMatches.SelectMany(m => m
                        .Locations
                        .Select(l =>
                            new TextSearchResultViewModel()
                            {
                                PageNumber = r.Key,
                                WordIndex = l.TokenIndex,
                                Score = m.Score,
                                Word = pdfDocument.Pages[r.Key - 1].PdfTextLayer?[l.TokenIndex]
                            }
                        )))
                });
        }

        public void Dispose()
        {
            _index.Dispose();
        }
    }
}
