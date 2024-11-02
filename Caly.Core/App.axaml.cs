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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Caly.Core.Services;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Core.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core
{
    public partial class App : Application
    {
        private readonly FilePipeStream _pipeServer = new();
        private readonly CancellationTokenSource _listeningToFilesCts = new();
        private Task? _listeningToFiles;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private IPdfDocumentsService _pdfDocumentsService;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public static new App? Current => Application.Current as App;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);

            // Initialise dependencies
            var services = new ServiceCollection();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                services.AddSingleton(_ => (Visual)desktop.MainWindow);
                desktop.Startup += Desktop_Startup;
                desktop.Exit += Desktop_Exit;
#if DEBUG
                desktop.MainWindow.RendererDiagnostics.DebugOverlays = Avalonia.Rendering.RendererDebugOverlays.RenderTimeGraph;
#endif
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
                services.AddSingleton(_ => (Visual)singleViewPlatform.MainView);
            }
#if DEBUG
            else if (ApplicationLifetime is null && Avalonia.Controls.Design.IsDesignMode)
            {
                var mainView = new MainView { DataContext = new MainViewModel() };
                services.AddSingleton(_ => (Visual)mainView);
            }
#endif
            services.AddSingleton<IFilesService, FilesService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IPdfDocumentsService, PdfDocumentsService>();
            
            services.AddScoped<IPdfService, PdfPigPdfService>();
            services.AddScoped<ITextSearchService, LiftiTextSearchService>();
            services.AddScoped<PdfDocumentViewModel>();

            Services = services.BuildServiceProvider();

            // We need to make sure IPdfDocumentsService singleton is initiated in UI thread
#pragma warning disable CS8601 // Possible null reference assignment.
            _pdfDocumentsService = Services.GetRequiredService<IPdfDocumentsService>();
#pragma warning restore CS8601 // Possible null reference assignment.

            // TODO - Check https://github.com/AvaloniaUI/Avalonia/commit/0e014f9cb627d99fb4e1afa389b4c073c836e9b6

            base.OnFrameworkInitializationCompleted();
        }

        private async void Desktop_Startup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            _listeningToFiles = Task.Run(ListenToIncomingFiles); // Start listening

            if (e.Args.Length == 0)
            {
                return;
            }

            await Task.Run(() => OpenDoc(e.Args[0], CancellationToken.None));
        }

        private void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            _listeningToFilesCts.Cancel();
            GC.KeepAlive(_listeningToFiles);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Startup -= Desktop_Startup;
                desktop.Exit -= Desktop_Exit;
            }
        }

        private async Task ListenToIncomingFiles()
        {
            Debug.ThrowOnUiThread();

            try
            {
                await Parallel.ForEachAsync(_pipeServer.ReceivePathAsync(_listeningToFilesCts.Token),
                    _listeningToFilesCts.Token,
                    async (path, ct) => await OpenDoc(path, ct));
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            catch (Exception ex)
            {
                // Critical error...
                ShowExceptionWindowSafely(ex);
                Debug.WriteExceptionToFile(ex);
                throw;
            }
            finally
            {
                await _pipeServer.DisposeAsync();
            }
        }

        private async Task OpenDoc(string? path, CancellationToken token)
        {
            Debug.ThrowOnUiThread();

            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    var dialogService = Services?.GetRequiredService<IDialogService>();
                    if (dialogService is not null)
                    {
                        dialogService.ShowNotification("Cannot open file",
                            "The file does not exist or the path is invalid.", NotificationType.Error);
                    }

                    return;
                }

                await _pdfDocumentsService.OpenLoadDocument(path, token);
            }
            catch (Exception ex)
            {
                ShowExceptionNotificationSafely(ex);
                Debug.WriteExceptionToFile(ex);
            }
        }

        private void ShowExceptionNotificationSafely(Exception? ex)
        {
            try
            {
                if (ex is null) return;

                var dialogService = Services?.GetRequiredService<IDialogService>();
                if (dialogService is not null)
                {
                    dialogService.ShowNotification("Error", ex.Message, NotificationType.Error);
                }
            }
            catch
            {
                // No op
            }
        }

        private void ShowExceptionWindowSafely(Exception? ex)
        {
            try
            {
                if (ex is null) return;

                var dialogService = Services?.GetRequiredService<IDialogService>();
                dialogService?.ShowExceptionWindow(ex);
            }
            catch
            {
                // No op
            }
        }
    }
}
