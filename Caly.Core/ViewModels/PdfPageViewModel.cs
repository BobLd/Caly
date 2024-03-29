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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;

namespace Caly.Core.ViewModels
{
    public sealed partial class PdfPageViewModel : ViewModelBase, IAsyncDisposable
    {
        private readonly AutoResetEvent _mutex = new AutoResetEvent(true);
        private readonly IPdfService _pdfService;

        private CancellationTokenSource? _cts = new();

        [ObservableProperty]
        private PdfTextLayer? _pdfTextLayer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageRendering))]
        private IRef<SKPicture>? _pdfPicture;

        //[ObservableProperty]
        //private IRef<SKBitmap>? _pdfBitmap;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailHeight))]
        private double _width;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailHeight))]
        private double _height;

        [ObservableProperty]
        private Bitmap? _thumbnail;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private bool _isPagePrepared;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageVisible))]
        private Rect? _visibleArea;

        [ObservableProperty]
        private ITextSelectionHandler _textSelectionHandler;

        public bool IsPageVisible => VisibleArea.HasValue;

        public int ThumbnailWidth { get; } = 100;

        public int ThumbnailHeight => (int)(Height / Width * ThumbnailWidth);

        public bool IsPageRendering => PdfPicture is null || PdfPicture.Item is null; // TODO - refactor might not be optimal

#if DEBUG
        /// <summary>
        /// Design mode constructor.
        /// </summary>
        public PdfPageViewModel()
        {
            if (Design.IsDesignMode)
            {
                //_pdfService = DummyPdfPageService.Instance; // TODO
            }
            else
            {
                throw new InvalidOperationException($"{typeof(PdfPageViewModel)} empty constructor should only be called in design mode");
            }
        }
#endif

        public PdfPageViewModel(int pageNumber, IPdfService pdfService, ITextSelectionHandler textSelectionHandler)
        {
            ArgumentNullException.ThrowIfNull(textSelectionHandler, nameof(textSelectionHandler));

            PageNumber = pageNumber;
            _pdfService = pdfService;
            TextSelectionHandler = textSelectionHandler;
        }

        public async Task LoadPageSize(CancellationToken cancellationToken)
        {
            var info = await _pdfService.GetPageInformationAsync(PageNumber, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && info.HasValue)
            {
                Width = info.Value.Width;
                Height = info.Value.Height;
            }
        }

        [RelayCommand]
        public async Task LoadPagePicture()
        {
            await Task.Run(async () =>
            {
                try
                {
                    _mutex.WaitOne();

                    if (_cts is null)
                    {
                        return;
                    }

                    if (PdfPicture?.Item is not null)
                    {
                        return;
                    }

                    _cts.Token.ThrowIfCancellationRequested();

                    PdfPicture = await _pdfService.GetRenderPageAsync(PageNumber, _cts.Token);

                    if (PdfPicture is not null)
                    {
                        var w = PdfPicture?.Item.CullRect.Width;
                        var h = PdfPicture?.Item.CullRect.Height;
                        if (w.HasValue && h.HasValue)
                        {
                            Width = w.Value;
                            Height = h.Value;
                        }

                        //await Task.WhenAll(LoadInteractiveLayer(_cts.Token), Task.Run(LoadBitmap, _cts.Token));
                        await LoadInteractiveLayer(_cts.Token);
                    }
                    //await LoadThumbnailFromPicture(ThumbnailWidth, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    DisposePictureSafely();
                }
                catch (Exception e)
                {
                    DisposePictureSafely();
                    Exception = new ExceptionViewModel(e);
                }
                finally
                {
                    _mutex.Set();
                }
            });
        }

        [RelayCommand]
        public async Task UnloadPagePicture()
        {
            // TODO - Make sure that loading / unloading the picture at the same time is properly handled

            await Task.Run(() =>
            {
                if (_cts is null)
                {
                    return;
                }

                try
                {
                    _mutex.WaitOne();
                    if (_cts.IsCancellationRequested)
                    {
                        return; // already cancelled
                    }

                    _cts.Cancel();
                    DisposePictureSafely();
                }
                catch (Exception e)
                {
                    Exception = new ExceptionViewModel(e);
                }
                finally
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                    _mutex.Set();
                }
            });
        }

        private void DisposePictureSafely()
        {
            var tempPicture = PdfPicture;
            PdfPicture = null;
            tempPicture?.Dispose();
            System.Diagnostics.Debug.Assert((tempPicture?.RefCount ?? 0) == 0);

            //var tempBitmap = PdfBitmap;
            //PdfBitmap = null;
            //tempBitmap?.Dispose();
            //System.Diagnostics.Debug.Assert((tempBitmap?.RefCount ?? 0) == 0);
        }

        /*
        private void LoadBitmap()
        {
            Debug.ThrowOnUiThread();

            if (PdfPicture is null)
            {
                throw new ArgumentNullException(nameof(PdfPicture),
                    "Please call LoadPagePicture() before calling LoadBitmap().");
            }

            var bitmap = new SKBitmap((int)PdfPicture.Item.CullRect.Width, (int)PdfPicture.Item.CullRect.Height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.DrawPicture(PdfPicture.Item);
            }

            PdfBitmap = RefCountable.Create(bitmap);
        }
        */

        private Task LoadThumbnailFromPicture(int width, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || Thumbnail is not null)
            {
                return Task.CompletedTask;
            }

            if (PdfPicture is null)
            {
                throw new ArgumentNullException(nameof(PdfPicture), "Please call LoadPagePicture() before calling LoadThumbnailFromPicture().");
            }

            return Task.Run(() =>
            {
                using (SKImage skImage = SKImage.FromPicture(PdfPicture.Item, new SKSizeI((int)Width, (int)Height)))
                using (SKData d = skImage.Encode(SKEncodedImageFormat.Png, 100))
                using (Stream stream = d.AsStream())
                {
                    Thumbnail = Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.LowQuality);
                }
            }, cancellationToken);
        }

        public async Task LoadInteractiveLayer(CancellationToken cancellationToken)
        {
            try
            {
                PdfTextLayer ??= await _pdfService.GetTextLayerAsync(PageNumber, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            catch (Exception ex)
            {
                Exception = new ExceptionViewModel(ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await UnloadPagePicture();
            _cts?.Dispose();
            _cts = null;
            _mutex.Dispose(); // TODO - Check if right place
        }
    }
}
