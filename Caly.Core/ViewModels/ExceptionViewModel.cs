using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Caly.Core.ViewModels
{
    // https://stackoverflow.com/questions/4228483/where-do-i-catch-exceptions-in-mvvm

    public sealed class ExceptionViewModel : ObservableObject
    {
        public Exception Exception { get; }

        public string Message { get; }

        public string StackTrace { get; }

        public ExceptionViewModel(Exception exception)
        {
            Exception = exception;
            Message = exception.Message;
            StackTrace = exception.StackTrace ?? string.Empty;
        }

        public override string ToString()
        {
            return Exception.ToString();
        }
    }
}
