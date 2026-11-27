using System.Windows;

namespace OOPproject.Gui;

public partial class App : Application
{
    /// <summary>The start of the application is its composition root: here the objects are created and connected.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppServices services = AppServices.Create(e.Args);
        var window = new MainWindow(services);
        window.Show();
    }
}