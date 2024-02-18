using System.Threading.Tasks;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Core.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using Caly.Core.Services;

namespace Caly.Core;

public partial class App : Application
{
    private readonly FilePipeStream _pipeServer = new();
    private readonly CancellationTokenSource listeningToFilesCts = new();
    private Task? listeningToFiles;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private IPdfDocumentsService _pdfDocumentsService;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public new static App? Current => Application.Current as App;

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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            services.AddSingleton(_ => desktop.MainWindow);
            services.AddSingleton<IFilesService, FilesService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IPdfDocumentsService, PdfDocumentsService>();

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
        }

        services.AddTransient<IPdfService, PdfPigPdfService>();

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
        listeningToFiles = Task.Run(ListenToIncomingFiles); // Start listening

        if (e.Args.Length == 0)
        {
            return;
        }

        await Task.Run(() => OpenDoc(e.Args[0], CancellationToken.None));
    }

    private void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        listeningToFilesCts.Cancel();
        GC.KeepAlive(listeningToFiles);

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
            await Parallel.ForEachAsync(_pipeServer.ReceivePathAsync(listeningToFilesCts.Token),
                listeningToFilesCts.Token,
                async (path, ct) => await OpenDoc(path, ct));
        }
        catch (OperationCanceledException)
        {
            // No op
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
                // TODO - notify and log
                return;
            }

            await _pdfDocumentsService.OpenLoadDocument(path, token);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync($"error_avalonia_OpenDoc_{Guid.NewGuid()}.txt", ex.ToString());
        }
    }
}
