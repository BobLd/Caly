using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Caly.Core;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Desktop;

class Program
{
    private static readonly Mutex mutex = new Mutex(true, "Caly application");

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Make sure the current directory is where the app is located, not where a file is opened
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        if (mutex.WaitOne(TimeSpan.Zero, true)) // Make sure a single instance of the app is running
        {
            try
            {
                // TODO - should the below be in App.axaml.cs?
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                if (ex is AggregateException a)
                {
                    ex = a.Flatten();
                }

                File.WriteAllText($"error_avalonia_main_{Guid.NewGuid()}.txt", ex.ToString());
                throw;
            }
            finally
            {
                mutex.ReleaseMutex();
                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            }
        }
        else
        {
            try
            {
                // App instance already running
                if (args.Length == 0)
                {
                    return;
                }

                // TODO - Still bring app to front even if there's no file to open
                SendPathToRunningInstance(args[0]);
            }
            catch (Exception ex)
            {
                if (ex is AggregateException a)
                {
                    File.WriteAllText($"error_avalonia_main_send_file_{Guid.NewGuid()}.txt", a.Flatten().ToString());
                }
                else
                {
                    File.WriteAllText($"error_avalonia_main_send_file_{Guid.NewGuid()}.txt", ex.ToString());
                }
                throw;
            }
        }
    }

    private static void ShowExceptionSafely(Exception? ex)
    {
        try
        {
            if (ex is null) return;

            var dialogService = App.Current?.Services?.GetRequiredService<IDialogService>();
            dialogService?.ShowErrorWindow(new ExceptionViewModel(ex));
        }
        catch
        {
            // No op
        }
    }

    private static void SendPathToRunningInstance(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        FilePipeStream.SendPath(path);
    }

    private static async void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // https://docs.avaloniaui.net/docs/getting-started/unhandled-exceptions
        // https://learn.microsoft.com/en-gb/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-7.0

        var exception = e.Exception.Flatten();
        ShowExceptionSafely(exception);
        await File.WriteAllTextAsync($"error_avalonia_task_scheduler_{Guid.NewGuid()}.txt", exception.ToString());
    }

    private static async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // https://github.com/AvaloniaUI/Avalonia/issues/5387

        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        if (exception is AggregateException aEx)
        {
            exception = aEx.Flatten();
        }
        ShowExceptionSafely(exception);
        await File.WriteAllTextAsync($"error_avalonia_unhandled_{Guid.NewGuid()}.txt", exception.ToString());
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        try
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .UseSkia()
                // https://github.com/AvaloniaUI/Avalonia/discussions/12597
                .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
                .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software }, WmClass = "theia Pdf Reader" })
                .With(new AvaloniaNativePlatformOptions { RenderingMode = new[] { AvaloniaNativeRenderingMode.Software } })
                .LogToTrace();
        }
        catch (Exception e)
        {
            File.WriteAllText($"error_avalonia_{Guid.NewGuid()}.txt", e.ToString());
            throw;
        }
    }
}
