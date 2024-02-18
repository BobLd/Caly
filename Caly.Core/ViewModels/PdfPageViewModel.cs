using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
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
        private Bitmap? _thumbnail;

        [ObservableProperty]
        private int _pageNumber;

        [ObservableProperty]
        private bool _isPagePrepared;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPageVisible))]
        private Rect? _visibleArea;

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

        public PdfPageViewModel(int pageNumber, IPdfService pdfService)
        {
            PageNumber = pageNumber;
            _pdfService = pdfService;
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
            // TODO - Make sure that loading / unloading the picture at the same time is properly handled

            await Task.Run(async () =>
            {
                if (_cts is null)
                {
                    return;
                }

                if (PdfPicture?.Item is not null)
                {
                    return;
                }

                try
                {
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
                    if (_cts.IsCancellationRequested)
                    {
                        return; // already cancelled
                    }

                    _cts.Cancel(); // await _cts.CancelAsync();
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
                //Thumbnail?.Dispose();

                //using (SKPicture picture = PdfPicture.Snapshot())
                // TODO - Clone Picture?
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
        }
    }
}
