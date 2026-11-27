using System.Windows;

namespace OOPproject.Gui;

public partial class MainWindow : Window
{
    private readonly AppServices _services;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;

        Title = services.Offline ? "Character Battler (offline sample data)" : "Character Battler";

        CatalogTab.Attach(services);
        GameTab.Attach(services);

        // The views do not know each other: the catalog only says that a character should be imported.
        CatalogTab.ImportRequested += async id =>
        {
            Tabs.SelectedItem = GameItem;
            await GameTab.SubmitAsync($"remote import char {id}");
        };

        Loaded += async (_, _) =>
        {
            _ = CatalogTab.RefreshAsync();
            await GameTab.OfferToContinueAsync();
        };

        Closing += (_, _) => _services.SaveOnExit();
    }
}