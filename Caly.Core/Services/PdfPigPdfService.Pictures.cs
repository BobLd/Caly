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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _pictureTokens = new();

        private readonly ConcurrentDictionary<int, PdfPageViewModel> _pictures = new();

        private async Task ProcessPictureRequest(RenderRequest renderRequest)
        {
            if (renderRequest.Token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] Cancelled {renderRequest.Page.PageNumber}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] Start process {renderRequest.Page.PageNumber}");

            try
            {
                if (_pictureTokens.TryRemove(renderRequest.Page.PageNumber, out _))
                {
                    if (renderRequest.Page.PdfPicture is not null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] No need process {renderRequest.Page.PageNumber}");
                        return;
                    }

                    var picture = await GetRenderPageAsync(renderRequest.Page.PageNumber, renderRequest.Token);

                    renderRequest.Page.PdfPicture = picture;

                    if (renderRequest.Page.PdfPicture?.Item is not null)
                    {
                        renderRequest.Page.Width = renderRequest.Page.PdfPicture.Item.CullRect.Width;
                        renderRequest.Page.Height = renderRequest.Page.PdfPicture.Item.CullRect.Height;
                    }
                }
                else
                {
                    if (renderRequest.Page.PdfPicture is not null)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] Cancelled process {renderRequest.Page.PageNumber}");
                if (renderRequest.Page.PdfPicture is not null)
                {
                }
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] End process {renderRequest.Page.PageNumber}");
        }

        public void ClearAllPagePictures()
        {
            System.Diagnostics.Debug.Assert(_pictures.Count == 0);

            /*
            foreach (var p in _pictures.Keys.ToArray())
            {
                if (_pictures.TryRemove(p, out var vm))
                {
                    var picture = vm.PdfPicture;
                    vm.PdfPicture = null;
                    picture?.Dispose();
                }
            }
            */
        }

        public void AskPagePicture(PdfPageViewModel page, CancellationToken token)
        {
            System.Diagnostics.Debug.WriteLine($"[RENDER] AskPagePicture {page.PageNumber}");
            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (_pictureTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingRenderRequests.Add(new RenderRequest(page, RenderRequestTypes.Picture, pageCts.Token), pageCts.Token);
            }
            else
            {

            }
        }

        public void AskRemovePagePicture(PdfPageViewModel page)
        {
            System.Diagnostics.Debug.WriteLine($"[RENDER] AskRemovePagePicture {page.PageNumber}");

            var picture = page.PdfPicture;

            page.PdfPicture = null;
            if (_pictureTokens.TryRemove(page.PageNumber, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_pictures.TryRemove(page.PageNumber, out var vm))
            {
                // Should always be null
                System.Diagnostics.Debug.Assert(vm.PdfPicture is null);
            }

            picture?.Dispose();

            System.Diagnostics.Debug.Assert((picture?.RefCount ?? 0) == 0);
        }

        private async Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            SKPicture? pic;
            try
            {
                if (cancellationToken.IsCancellationRequested || isDiposed())
                {
                    return null;
                }

                await _semaphore.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                pic = _document!.GetPage<SKPicture>(pageNumber);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (_semaphore.CurrentCount == 0 && !isDiposed())
                {
                    _semaphore.Release();
                }
            }

            return pic is null ? null : RefCountable.Create(pic);
        }
    }
}
