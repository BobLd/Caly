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
using Caly.Core;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
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

                Debug.WriteExceptionToFile(ex);
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
                    ex = a.Flatten();
                }

                Debug.WriteExceptionToFile(ex);
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
            dialogService?.ShowExceptionWindow(ex);
        }
        catch
        {
            Debug.WriteExceptionToFile(ex);
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

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // https://docs.avaloniaui.net/docs/getting-started/unhandled-exceptions
        // https://learn.microsoft.com/en-gb/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-7.0

        var exception = e.Exception.Flatten();
        ShowExceptionSafely(exception);
        Debug.WriteExceptionToFile(exception);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
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
        Debug.WriteExceptionToFile(exception);
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
                .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software }, WmClass = "Caly Pdf Reader" })
                .With(new AvaloniaNativePlatformOptions { RenderingMode = new[] { AvaloniaNativeRenderingMode.Software } })
                .LogToTrace();
        }
        catch (Exception e)
        {
            Debug.WriteExceptionToFile(e);
            throw;
        }
    }
}
