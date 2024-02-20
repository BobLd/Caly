using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;

namespace Caly.Core.Views;

public partial class MainWindow : Window
{
    public WindowNotificationManager NotificationManager { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        NotificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomRight,
#if DEBUG
            MaxItems = 50
#else
            MaxItems = 5
#endif
        };

        base.OnLoaded(e);
    }
}
