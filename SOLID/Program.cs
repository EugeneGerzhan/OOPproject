using System;
using System.IO;
using System.Net.Http;
using GameSystem.Core.Logging;
using GameSystem.Core.Models;
using GameSystem.Core.Remote;
using Infra.Data;
using Infra.Display;
using Infra.WebApi;
using OOPproject.SOLID;
using OOPTextProject.TextSystem;

class Program
{
    private const string LegacySavesFile = "saves.json";
    private const string RemoteApiUrl = "https://genshin.jmp.blue/";

    static void Main(string[] args)
    {
        // ----- composition root: the only place that knows the concrete classes -----
        IDisplayer displayer = new ConsoleDisplayer();
        IInputReader input = new ConsoleInputReader();
        IPresenter presenter = new Presenter(displayer, input);

        // ----- where the data live: the data folder of the user, not the folder of the program -----
        var paths = AppPaths.ForCurrentUser();
        try
        {
            if (paths.MigrateLegacySaves(LegacySavesFile))
            {
                presenter.Show($"The old saves file was copied to {paths.SavesFile}.");
            }
        }
        catch (IOException ex)
        {
            presenter.Show($"The old saves file could not be copied: {ex.Message}");
        }

        var saves = new SaveManager(new JsonRepositoryStorage(paths.SavesFile));

        // ----- mode selection -----
        var startArgs = new CliArguments(args);
        string mode;

        if (startArgs.Options.ContainsKey("--text"))
        {
            mode = "text";
        }
        else if (startArgs.Options.ContainsKey("--chars"))
        {
            mode = "chars";
        }
        else
        {
            presenter.Show("Select mode:");
            presenter.Show("1. Text System (--text)");
            presenter.Show("2. Characters System and text game (--chars)");
            string choice = presenter.Prompt("Enter choice (1 or 2) [1]: ");
            mode = choice == "2" ? "chars" : "text";
        }

        // ----- the command line: a facade parametrized with strategies -----
        var cli = new Cli(presenter);
        AutoSaver? autoSaver = null;

        if (mode == "chars")
        {
            bool offline = startArgs.Options.ContainsKey("--offline");
            HttpClient http = CreateHttpClient();

            IGameDataApi api = offline ? new OfflineGameDataApi() : new GenshinApiClient(http);
            IImageSource imageSource = offline ? new OfflineImageSource() : new HttpImageSource(http);
            // the offline mode has its own folder: its built-in one-pixel pictures must never be taken for real portraits
            string imagesFolder = offline ? Path.Combine(paths.DataDirectory, "images-offline") : paths.ImagesDirectory;
            var images = new ImageCache(imagesFolder, imageSource, TimeSpan.FromDays(30));

            var session = new GameSession();
            var logger = new DisplayerLogger(displayer);

            var game = new GameStrategy(session, saves, presenter, cli, logger);
            autoSaver = new AutoSaver(session, saves, () => game.CurrentBattle);

            cli.UseStrategy(new CharactersCliHandler(logger, presenter, saves, session));
            cli.UseStrategy(new RemoteCliHandler(api, new RemoteImporter(new StatGenerator()), session, presenter, new CharacterSheetBuilder(), images));
            cli.UseStrategy(game);
            cli.UseStrategy(new PersistenceStrategy(autoSaver, saves, presenter, images));
            cli.AfterCommand = () => autoSaver.SaveIfChanged();

            presenter.Show("=== Characters CLI Mode Activated ===");
            presenter.Show(offline
                ? "Remote database: offline sample data"
                : $"Remote database: {RemoteApiUrl}");
            presenter.Show($"Data folder: {paths.DataDirectory}");

            OfferToContinue(saves, game, presenter);
        }
        else
        {
            cli.UseStrategy(new TextCliHandler(new TextFactory("Main Document"), presenter, saves));
            presenter.Show("=== Text System CLI Mode Activated ===");
        }

        cli.UseStrategy(new SystemStrategy(cli, presenter));

        presenter.Show("Type 'help' for commands, 'commands' for all of them, 'exit' to quit.");

        // ----- main loop -----
        while (true)
        {
            displayer.Write("> ");
            string? line = input.ReadLine();
            if (line == null || line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            cli.ExecCommand(line);
        }

        // The game is saved when the program ends, even if the last command changed nothing visible.
        try
        {
            autoSaver?.SaveIfChanged();
        }
        catch (Exception ex)
        {
            presenter.Show($"[Warning]: the game could not be saved: {ex.Message}");
        }
    }

    /// <summary>At the start: offers to continue from the last save (an autosave counts too).</summary>
    private static void OfferToContinue(SaveManager saves, GameStrategy game, IPresenter presenter)
    {
        try
        {
            string? last = saves.LastCharacterSaveName;
            if (last != null && presenter.Confirm($"The last save is '{last}'. Continue from it? (y/n): "))
            {
                game.ContinueFromLastSave(askConfirmation: false);
            }
        }
        catch (Exception ex)
        {
            presenter.Show($"The last save could not be loaded: {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(RemoteApiUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OOP-Course-Project/1.0");
        return client;
    }
}