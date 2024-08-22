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

namespace Caly.Desktop
{
    internal class Program
    {
        private const string _appName = "Caly Pdf Reader";

        private static readonly Mutex mutex = new Mutex(true, _appName);

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Make sure the current directory is where the app is located, not where a file is opened
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            bool isMainInstance = false;

            try
            {
                // Make sure a single instance of the app is running
                isMainInstance = mutex.WaitOne(TimeSpan.Zero, true);

                if (isMainInstance)
                {
                    SendToMainInstance(args);
                }
                else // App instance already running
                {
                    SendToRunningInstance(args);
                }
            }
            finally
            {
                if (isMainInstance)
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static int SendToMainInstance(string[] args)
        {
            try
            {
                // TODO - should the below be in App.axaml.cs?
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            }
        }

        private static void SendToRunningInstance(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    // TODO - Still bring app to front even if there's no file to open
                    return;
                }

                string path = args[0];

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                FilePipeStream.SendPath(path);
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
                    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
                    .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software], WmClass = _appName })
                    .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] })
                    .LogToTrace();
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                throw;
            }
        }
    }
}
