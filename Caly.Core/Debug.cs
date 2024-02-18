using System;
using System.Diagnostics;

namespace Caly.Core
{
    internal static class Debug
    {
        [Conditional("DEBUG")]
        public static void ThrowOnUiThread()
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                throw new InvalidOperationException("Call from UI thread");
            }
        }

        [Conditional("DEBUG")]
        public static void ThrowNotOnUiThread()
        {
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                throw new InvalidOperationException("Call from non-UI thread");
            }
        }
    }
}
