using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameSystem.Core.Remote;
using Infra.Data;

namespace OOPproject.Gui;

/// <summary>
/// What a card of the list binds to. The texts come from <see cref="CardPresentation"/>; the portrait is loaded
/// the first time the card asks for it. The list is virtualized, so only the cards that are on the screen load
/// their pictures, and every picture goes through the <see cref="ImageCache"/> (the server is asked only once).
/// </summary>
public sealed class CardViewModel : INotifyPropertyChanged
{
    private readonly CharacterCard _card;
    private readonly ImageCache _images;
    private readonly CardPresentation _look;

    private ImageSource? _portrait;
    private string _portraitInfo = "Loading the portrait...";
    private bool _requested;

    public CardViewModel(CharacterCard card, ImageCache images)
    {
        _card = card;
        _images = images;
        _look = CardPresentation.From(card);

        var color = (Color)ColorConverter.ConvertFromString(_look.AccentColor);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        AccentBrush = brush;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => _card.Id;
    public string Name => _look.Name;
    public string Title => _look.Title;
    public string Stars => _look.Stars;
    public string Info => _look.Info;
    public string ShortDescription => _look.ShortDescription;
    public string Initial => _look.Initial;
    public string Description => _card.Description;
    public IReadOnlyList<TalentCard> Talents => _card.Talents;
    public IReadOnlyList<TalentCard> Passives => _card.Passives;
    public Brush AccentBrush { get; }

    /// <summary>The portrait; null while it is loading or when there is none (the placeholder is shown then).</summary>
    public ImageSource? Portrait
    {
        get
        {
            if (!_requested)
            {
                _requested = true;
                _ = LoadPortraitAsync();
            }
            return _portrait;
        }
    }

    /// <summary>Where the portrait came from, or why there is none.</summary>
    public string PortraitInfo
    {
        get => _portraitInfo;
        private set
        {
            _portraitInfo = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortraitInfo)));
        }
    }

    private async Task LoadPortraitAsync()
    {
        if (string.IsNullOrWhiteSpace(_card.ImageUrl))
        {
            PortraitInfo = "This character has no link to a picture.";
            return;
        }

        try
        {
            CachedImage image = await _images.GetAsync($"{_card.Id}-portrait", _card.ImageUrl);
            _portrait = LoadBitmap(image.FilePath);

            string origin = image.FromCache ? (image.Stale ? "the cache (expired)" : "the cache") : "the server";
            PortraitInfo = _portrait == null
                ? "No real picture: a placeholder is shown."
                : $"Portrait from {origin}: {image.FilePath}";

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Portrait)));
        }
        catch (Exception ex)
        {
            // A missing or unreadable picture must never break the list: the placeholder stays.
            PortraitInfo = $"No picture: {ex.Message}";
        }
    }

    private static ImageSource? LoadBitmap(string path)
    {
        // The file is read into memory, so it is not locked and the cache can replace it later.
        byte[] data = File.ReadAllBytes(path);

        using (var probe = new MemoryStream(data))
        {
            var frame = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            if (frame.PixelWidth < 16)
            {
                return null; // the built-in picture of the offline mode is one pixel: the placeholder looks better
            }
        }

        using var stream = new MemoryStream(data);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.DecodePixelWidth = 320;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}