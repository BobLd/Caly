using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    public class CalyTemplatedControl : TemplatedControl
    {
        /// <summary>
        /// Defines the <see cref="Exception"/> property.
        /// </summary>
        public static readonly StyledProperty<ExceptionViewModel?> ExceptionProperty = AvaloniaProperty.Register<CalyTemplatedControl, ExceptionViewModel?>(nameof(ExceptionProperty), defaultBindingMode: BindingMode.TwoWay);

        public ExceptionViewModel? Exception
        {
            get => GetValue(ExceptionProperty);
            set => SetValue(ExceptionProperty, value);
        }
    }
}
