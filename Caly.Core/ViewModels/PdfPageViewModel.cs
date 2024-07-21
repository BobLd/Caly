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
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1);
        private readonly IPdfService _pdfService;

        private CancellationTokenSource? _cts = new();

        [ObservableProperty]
        private PdfTextLayer? _pdfTextLayer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageRendering))]
        private IRef<SKPicture>? _pdfPicture;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailHeight))]
        private double _width;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ThumbnailHeight))]
        private double _height;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsThumbnailRendering))]
        private Bitmap? _thumbnail;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private bool _isPagePrepared;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageVisible))]
        private Rect? _visibleArea;

        [ObservableProperty]
        private bool _selectionChangedFlag;

        [ObservableProperty]
        private ITextSelectionHandler _textSelectionHandler;

        public bool IsPageVisible => VisibleArea.HasValue;

        public int ThumbnailWidth { get; } = 100;

        public int ThumbnailHeight => (int)(Height / Width * ThumbnailWidth);

        public bool IsPageRendering => PdfPicture is null || PdfPicture.Item is null; // TODO - refactor might not be optimal

        public bool IsThumbnailRendering => Thumbnail is null;

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

        public void FlagSelectionChanged()
        {
            Debug.ThrowNotOnUiThread();
            SelectionChangedFlag = !SelectionChangedFlag;
        }

        [RelayCommand]
        private async Task LoadPagePicture()
        {
            await Task.Run(async () =>
            {
                if (_cts is null)
                {
                    return;
                }

                await _mutex.WaitAsync();

                try
                {
                    await LoadPagePictureLocal();
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
                    _mutex.Release();
                }
            });
        }

        private async Task LoadPagePictureLocal()
        {
            if (PdfPicture?.Item is not null)
            {
                return;
            }

            _cts.Token.ThrowIfCancellationRequested();

            PdfPicture = await _pdfService.GetRenderPageAsync(PageNumber, _cts.Token);

            if (PdfPicture is not null)
            {
                float? w = PdfPicture?.Item.CullRect.Width;
                float? h = PdfPicture?.Item.CullRect.Height;
                if (w.HasValue && h.HasValue)
                {
                    Width = w.Value;
                    Height = h.Value;
                }

                await LoadInteractiveLayer(_cts.Token);
            }
        }

        [RelayCommand]
        private async Task UnloadPagePicture()
        {
            await Task.Run(async () =>
            {
                if (_cts is null)
                {
                    return;
                }

                await _mutex.WaitAsync();
                try
                {
                    if (_cts.IsCancellationRequested)
                    {
                        return; // already cancelled
                    }

                    await _cts.CancelAsync();
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
                    _mutex.Release();
                }
            });
        }

        private void DisposePictureSafely()
        {
            var tempPicture = PdfPicture;
            PdfPicture = null;
            tempPicture?.Dispose();
            System.Diagnostics.Debug.Assert((tempPicture?.RefCount ?? 0) == 0);
        }

        [RelayCommand]
        private async Task LoadThumbnail()
        {
            await Task.Run(async () =>
            {
                if (_cts is null)
                {
                    return;
                }

                await _mutex.WaitAsync();
                try
                {
                    if (Thumbnail is not null)
                    {
                        return;
                    }

                    if (PdfPicture is null)
                    {
                        await LoadPagePictureLocal();
                        if (PdfPicture is null)
                        {
                            return;
                        }
                    }

                    int tWidth = (int)(ThumbnailWidth / 1.5);
                    int tHeight = (int)(ThumbnailHeight / 1.5);

                    SKMatrix scale = SKMatrix.CreateScale(tWidth / (float)Width, tHeight / (float)Height);

                    using (SKBitmap bitmap = new SKBitmap(tWidth, tHeight))
                    using (SKCanvas canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(SKColors.White);
                        canvas.DrawPicture(PdfPicture!.Item, ref scale);

                        using (SKData d = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100))
                        await using (Stream stream = d.AsStream())
                        {
                            Thumbnail = Bitmap.DecodeToWidth(stream, ThumbnailWidth,
                                BitmapInterpolationMode.LowQuality);
                        }
                    }

                    if (!IsPageVisible)
                    {
                        DisposePictureSafely();
                    }
                }
                catch (OperationCanceledException)
                {
                    //DisposePictureSafely();
                }
                catch (Exception e)
                {
                    //DisposePictureSafely();
                    Exception = new ExceptionViewModel(e);
                }
                finally
                {
                    _mutex.Release();
                }
            });
        }

        [RelayCommand]
        private async Task UnloadThumbnail()
        {
            await Task.Run(async () =>
            {
                if (_cts is null)
                {
                    return;
                }

                await _mutex.WaitAsync();
                try
                {
                    if (_cts.IsCancellationRequested)
                    {
                        return; // already cancelled
                    }

                    var t = Thumbnail;
                    Thumbnail = null;
                    t?.Dispose();
                }
                catch (Exception e)
                {
                    Exception = new ExceptionViewModel(e);
                }
                finally
                {
                    _mutex.Release();
                }
            });
        }

        public async Task LoadInteractiveLayer(CancellationToken cancellationToken)
        {
            try
            {
                PdfTextLayer ??= await _pdfService.GetTextLayerAsync(PageNumber, cancellationToken);
                if (PdfTextLayer is not null)
                {
                    // We ensure the correct selection is set now that we have the text layer
                    TextSelectionHandler.Selection.SelectWordsInRange(this);
                }
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
            await UnloadThumbnail();
            await UnloadPagePicture();
            _cts?.Dispose();
            _cts = null;
            _mutex.Dispose(); // TODO - Check if right place
        }
    }
}
