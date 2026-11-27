using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameSystem.Core.Remote;
using Infra.WebApi;

namespace OOPproject.Gui;

/// <summary>
/// Theme 1: the catalog. A scrollable list of character cards (portrait, name, short information),
/// a search with filters and a panel with the details. The window only shows what <see cref="CharacterCatalog"/> gives.
/// </summary>
public partial class CatalogView : UserControl
{
    private const string Any = "Any";

    private readonly Dictionary<string, CardViewModel> _viewModels = new(StringComparer.OrdinalIgnoreCase);
    private AppServices? _services;
    private bool _filling;

    public CatalogView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user wants a character in the game (the id of the character).</summary>
    public event Action<string>? ImportRequested;

    public void Attach(AppServices services)
    {
        _services = services;
    }

    /// <summary>Loads the cards from the server (the portraits are loaded later, when the cards are shown).</summary>
    public async Task RefreshAsync()
    {
        if (_services == null)
        {
            return;
        }

        RefreshButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        StatusText.Text = "Loading the characters...";

        try
        {
            CatalogRefreshResult result = await _services.Catalog.RefreshAsync();

            _viewModels.Clear();
            FillFilters();
            ApplyFilter();

            StatusText.Text = result.Failed.Count == 0
                ? $"{result.Loaded} character(s) loaded."
                : $"{result.Loaded} character(s) loaded, {result.Failed.Count} failed: {string.Join(", ", result.Failed.Take(5))}";
        }
        catch (RemoteApiException ex)
        {
            ApplyFilter(); // the cards that were loaded before (if any) stay
            StatusText.Text = $"The server is not available: {ex.Message} (start the program with --offline to use the sample data)";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"The catalog could not be loaded: {ex.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    // ---------- events of the controls ----------

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    // TextChanged and SelectionChanged of all the filters
    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void CardsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardsList.SelectedItem is CardViewModel card)
        {
            DetailsPanel.DataContext = card;
            DetailsPanel.Visibility = Visibility.Visible;
            EmptyText.Visibility = Visibility.Collapsed;
        }
        else
        {
            DetailsPanel.DataContext = null;
            DetailsPanel.Visibility = Visibility.Collapsed;
            EmptyText.Visibility = Visibility.Visible;
        }
    }

    private void AddToGame_Click(object sender, RoutedEventArgs e)
    {
        if (DetailsPanel.DataContext is CardViewModel card)
        {
            ImportRequested?.Invoke(card.Id);
        }
    }

    // ---------- helpers ----------

    private void ApplyFilter()
    {
        if (_services == null || _filling)
        {
            return;
        }

        var filter = new CatalogFilter(
            Text: SearchBox.Text,
            Vision: Selected(VisionBox),
            WeaponType: Selected(WeaponBox),
            MinRarity: RarityBox.SelectedIndex switch { 1 => 4, 2 => 5, _ => (int?)null },
            Sort: (CatalogSort)Math.Max(0, SortBox.SelectedIndex));

        List<CardViewModel> cards = _services.Catalog.Search(filter).Select(ViewModelFor).ToList();

        CardsList.ItemsSource = cards;
        CountText.Text = $"{cards.Count} of {_services.Catalog.Cards.Count} shown";
    }

    private CardViewModel ViewModelFor(CharacterCard card)
    {
        if (!_viewModels.TryGetValue(card.Id, out CardViewModel? viewModel))
        {
            viewModel = new CardViewModel(card, _services!.Images);
            _viewModels[card.Id] = viewModel;
        }
        return viewModel;
    }

    private void FillFilters()
    {
        _filling = true; // filling the lists must not start a search for every change
        try
        {
            Fill(VisionBox, _services!.Catalog.Cards.Select(c => c.Vision));
            Fill(WeaponBox, _services.Catalog.Cards.Select(c => c.WeaponType));
        }
        finally
        {
            _filling = false;
        }
    }

    private static void Fill(ComboBox box, IEnumerable<string> values)
    {
        string? previous = box.SelectedItem as string;

        var items = new List<string> { Any };
        items.AddRange(values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));

        box.ItemsSource = items;
        box.SelectedItem = previous != null && items.Contains(previous) ? previous : Any;
    }

    private static string? Selected(ComboBox box) => box.SelectedItem is string s && s != Any ? s : null;
}