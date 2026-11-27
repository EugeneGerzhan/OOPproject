using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using Infra.Display;
using Infra.WebApi;
using OOPproject.SOLID;

namespace OOPproject.Gui;

/// <summary>
/// The composition root of the graphical application: the only place that knows the concrete classes.
/// The command line, the strategies and the autosave are the same as in the console program; only the way
/// of showing the text and of asking questions is different (see <see cref="NotifyingDisplayer"/> and
/// <see cref="DialogInputReader"/>).
/// </summary>
public sealed class AppServices
{
    private const string RemoteApiUrl = "https://genshin.jmp.blue/";

    private readonly AutoSaver _autoSaver;

    private AppServices(AppPaths paths, bool offline, SaveManager saves, NotifyingDisplayer displayer,
                        CommandRunner runner, GameStrategy game, CharacterCatalog catalog, ImageCache images,
                        AutoSaver autoSaver)
    {
        Paths = paths;
        Offline = offline;
        Saves = saves;
        Displayer = displayer;
        Runner = runner;
        Game = game;
        Catalog = catalog;
        Images = images;
        _autoSaver = autoSaver;
    }

    public AppPaths Paths { get; }
    public bool Offline { get; }
    public SaveManager Saves { get; }
    public NotifyingDisplayer Displayer { get; }
    public CommandRunner Runner { get; }
    public GameStrategy Game { get; }
    public CharacterCatalog Catalog { get; }
    public ImageCache Images { get; }

    public static AppServices Create(string[] args)
    {
        bool offline = args.Contains("--offline", StringComparer.OrdinalIgnoreCase);

        var paths = AppPaths.ForCurrentUser();
        var saves = new SaveManager(new JsonRepositoryStorage(paths.SavesFile));

        var http = new HttpClient { BaseAddress = new Uri(RemoteApiUrl), Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OOP-Course-Project/1.0");

        IGameDataApi api = offline ? new OfflineGameDataApi() : new GenshinApiClient(http);
        IImageSource imageSource = offline ? new OfflineImageSource() : new HttpImageSource(http);
        string imagesFolder = offline ? Path.Combine(paths.DataDirectory, "images-offline") : paths.ImagesDirectory;
        var images = new ImageCache(imagesFolder, imageSource, TimeSpan.FromDays(30));

        // The window is the console: text goes out through an event, questions come in through dialogs.
        var displayer = new NotifyingDisplayer();
        var presenter = new Presenter(displayer, new DialogInputReader(displayer));
        var logger = new DisplayerLogger(displayer);
        var session = new GameSession();

        var cli = new Cli(presenter);
        var game = new GameStrategy(session, saves, presenter, cli, logger);
        var autoSaver = new AutoSaver(session, saves, () => game.CurrentBattle);

        cli.UseStrategy(new CharactersCliHandler(logger, presenter, saves, session));
        cli.UseStrategy(new RemoteCliHandler(api, new RemoteImporter(new StatGenerator()), session, presenter,
                                             new CharacterSheetBuilder(), images));
        cli.UseStrategy(game);
        cli.UseStrategy(new PersistenceStrategy(autoSaver, saves, presenter, images));
        cli.UseStrategy(new SystemStrategy(cli, presenter));
        cli.AfterCommand = () => autoSaver.SaveIfChanged();

        return new AppServices(paths, offline, saves, displayer, new CommandRunner(cli), game,
                               new CharacterCatalog(api), images, autoSaver);
    }

    /// <summary>Writes the autosave when the window is closed.</summary>
    public void SaveOnExit()
    {
        try
        {
            _autoSaver.SaveIfChanged();
        }
        catch (Exception)
        {
            // The program is closing: there is nobody left to tell, and the last autosave stays on the disk.
        }
    }
}