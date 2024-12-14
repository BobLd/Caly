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
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _pictureTokens = new();

        private readonly ConcurrentDictionary<int, PdfPageViewModel> _pictures = new();

        private async Task ProcessPictureRequest(RenderRequest renderRequest)
        {
            if (IsDisposed())
            {
                return;
            }

            if (renderRequest.Token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] Cancelled {renderRequest.Page.PageNumber}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RENDER] [PICTURE] Start process {renderRequest.Page.PageNumber}");

            try
            {
                if (_pictureTokens.TryRemove(renderRequest.Page.PageNumber, out var cts))
                {
                    cts.Dispose();

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

            if (IsDisposed())
            {
                return;
            }

            var pageCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            if (_pictureTokens.TryAdd(page.PageNumber, pageCts))
            {
                _pendingHighPriorityRequests.Add(new RenderRequest(page, RenderRequestTypes.Picture, pageCts.Token), pageCts.Token);
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

        private async Task<IRef<SKPicture>?> GetRenderPageAsync(int pageNumber, CancellationToken token)
        {
            Debug.ThrowOnUiThread();
            bool hasLock = false;

            SKPicture? pic;
            try
            {
                token.ThrowIfCancellationRequested();

                if (IsDisposed())
                {
                    return null;
                }

                await _semaphore.WaitAsync(token);
                hasLock = true;

                if (IsDisposed())
                {
                    return null;
                }

                token.ThrowIfCancellationRequested();

                pic = _document!.GetPage<SKPicture>(pageNumber);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                pic = GetErrorPicture(pageNumber, e, token);
            }
            finally
            {
                if (hasLock && !IsDisposed())
                {
                    _semaphore.Release();
                }
#if DEBUG
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GetRenderPageAsync NO LOCK {pageNumber}");
                }
#endif
            }

            return pic is null ? null : RefCountable.Create(pic);
        }

        private SKPicture? GetErrorPicture(int pageNumber, Exception ex, CancellationToken cancellationToken)
        {
            // Try get page size
            PdfPageInformation info;

            try
            {
                info = _document!.GetPage<PdfPageInformation>(pageNumber);
            }
            catch (Exception e)
            {
                // TODO
                info = new PdfPageInformation()
                {
                    Width = 100,
                    Height = 100,
                    PageNumber = pageNumber
                };
            }

            float width = (float)info.Width;
            float height = (float)info.Height;

            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(SKRect.Create(width, height)))
            {
//#if DEBUG
                float size = 9;
                using (var drawTypeface = SKTypeface.CreateDefault())
                using (var skFont = drawTypeface.ToFont(size))
                using (var fontPaint = new SKPaint(skFont))
                {
                    fontPaint.Color = SKColors.Red;
                    fontPaint.IsAntialias = true;

                    float lineY = size + 1;
                    foreach (var textLine in ex.ToString().Split('\n'))
                    {
                        canvas.DrawShapedText(textLine, new SKPoint(0, lineY), fontPaint);
                        lineY += size;
                    }
                }
//#endif

                canvas.Flush();

                return recorder.EndRecording();
            }

        }
    }
}
