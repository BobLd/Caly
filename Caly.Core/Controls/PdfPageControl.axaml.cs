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
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Controls
{
    public class PdfPageControl : CalyTemplatedControl
    {
        private readonly IDisposable _pagePreparedDisposable;

        /// <summary>
        /// Defines the <see cref="IsPageRendering"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageRenderingProperty = AvaloniaProperty.Register<PdfPageControl, bool>(nameof(IsPageRendering));

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty = AvaloniaProperty.Register<PdfPageControl, IRef<SKPicture>?>(nameof(Picture), defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Defines the <see cref="IsPageVisible"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageVisibleProperty = AvaloniaProperty.Register<PdfPageControl, bool>(nameof(IsPageVisible), false);

        /// <summary>
        /// Defines the <see cref="IsPagePrepared"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPagePreparedProperty = AvaloniaProperty.Register<PdfPageControl, bool>(nameof(IsPagePrepared), false, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty = AvaloniaProperty.Register<PdfPageControl, Rect?>(nameof(VisibleArea), null, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="LoadPagePictureCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> LoadPagePictureCommandProperty = AvaloniaProperty.Register<PdfPageControl, ICommand?>(nameof(LoadPagePictureCommand));

        /// <summary>
        /// Defines the <see cref="UnloadPagePictureCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> UnloadPagePictureCommandProperty = AvaloniaProperty.Register<PdfPageControl, ICommand?>(nameof(UnloadPagePictureCommand));

        /// <summary>
        /// Defines the <see cref="TextSelectionHandler"/> property.
        /// </summary>
        public static readonly StyledProperty<ITextSelectionHandler?> TextSelectionHandlerProperty = AvaloniaProperty.Register<PdfPageControl, ITextSelectionHandler?>(nameof(TextSelectionHandler), defaultBindingMode: BindingMode.TwoWay);

        public bool IsPageRendering
        {
            get => GetValue(IsPageRenderingProperty);
            set => SetValue(IsPageRenderingProperty, value);
        }

        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public bool IsPageVisible
        {
            get => GetValue(IsPageVisibleProperty);
            set => SetValue(IsPageVisibleProperty, value);
        }

        public bool IsPagePrepared
        {
            get => GetValue(IsPagePreparedProperty);
            set => SetValue(IsPagePreparedProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        /// <summary>
        /// Gets or sets an <see cref="ICommand"/> to be invoked when the page picture needs to be loaded.
        /// <para>This is when the page becomes 'visible'.</para>
        /// </summary>
        public ICommand? LoadPagePictureCommand
        {
            get => GetValue(LoadPagePictureCommandProperty);
            set => SetValue(LoadPagePictureCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets an <see cref="ICommand"/> to be invoked when the page picture needs to be unloaded.
        /// <para>This is when the page becomes 'invisible'.</para>
        /// </summary>
        public ICommand? UnloadPagePictureCommand
        {
            get => GetValue(UnloadPagePictureCommandProperty);
            set => SetValue(UnloadPagePictureCommandProperty, value);
        }

        public ITextSelectionHandler? TextSelectionHandler
        {
            get => GetValue(TextSelectionHandlerProperty);
            set => SetValue(TextSelectionHandlerProperty, value);
        }

        static PdfPageControl()
        {
            AffectsRender<PdfPageControl>(PictureProperty, IsPageVisibleProperty);
        }

        public PdfPageControl()
        {
#if DEBUG
            if (Design.IsDesignMode)
            {
                // Only if in design mode
                DataContext = new PdfPageViewModel();
            }
#endif

            _pagePreparedDisposable = this.GetObservable(IsPagePreparedProperty)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(SynchronizationContext.Current!) // UI thread - needed as we call the commands
                .Subscribe(
                    onNext: isPrepared =>
                    {
                        try
                        {
                            if (isPrepared)
                            {
                                LoadPagePictureCommand?.Execute(null);
                            }
                            else
                            {
                                // UnloadPagePictureCommand is most often null here, it is invoked when the property
                                // is changed to a new value.
                                UnloadPagePictureCommand?.Execute(null);
                            }
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR in Subscribe {e}");
                            Debug.WriteExceptionToFile(e);
                            SetCurrentValue(ExceptionProperty, new ExceptionViewModel(e));
                        }
                    },
                    onError: e =>
                    {
                        System.Diagnostics.Debug.WriteLine($"ERROR Subscribe {e}");
                        Debug.WriteExceptionToFile(e);
                        SetCurrentValue(ExceptionProperty, new ExceptionViewModel(e));
                    },
                    onCompleted: () =>
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR Subscribe COMPLETE");
                        // Throw - should never happen
                    });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UnloadPagePictureCommandProperty && change.OldValue is ICommand command)
            {
                // The command is being updated, make sure we invoke it before it's gone
                command.Execute(null);
            }
#if DEBUG
            if (change.Property == PictureProperty && change.OldValue is IRef<SKPicture> pic)
            {
                System.Diagnostics.Debug.Assert(pic.RefCount == 0);
            }
#endif
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            _pagePreparedDisposable.Dispose();
            Picture?.Dispose();

            System.Diagnostics.Debug.Assert((Picture?.RefCount ?? 0) == 0);
        }
    }
}
