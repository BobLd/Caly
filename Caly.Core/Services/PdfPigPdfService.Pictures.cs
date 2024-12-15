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
using System.Threading;
using System.Threading.Tasks;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Caly.Core.Services
{
    internal sealed partial class PdfPigPdfService
    {
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
                throw; // No error picture to generate
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
