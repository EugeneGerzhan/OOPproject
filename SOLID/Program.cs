using System;
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
    private const string SaveFileName = "saves.json";
    private const string RemoteApiUrl = "https://genshin.jmp.blue/";

    static void Main(string[] args)
    {
        // ----- composition root: the only place that knows the concrete classes -----
        IDisplayer displayer = new ConsoleDisplayer();
        IInputReader input = new ConsoleInputReader();
        IPresenter presenter = new Presenter(displayer, input);
        var saves = new SaveManager(new JsonRepositoryStorage(SaveFileName));

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

        if (mode == "chars")
        {
            bool offline = startArgs.Options.ContainsKey("--offline");
            IGameDataApi api = offline
                ? new OfflineGameDataApi()
                : new GenshinApiClient(CreateHttpClient());

            var session = new GameSession();
            var logger = new DisplayerLogger(displayer);

            cli.UseStrategy(new CharactersCliHandler(logger, presenter, saves, session));
            cli.UseStrategy(new RemoteCliHandler(api, new RemoteImporter(new StatGenerator()), session, presenter, new CharacterSheetBuilder()));
            cli.UseStrategy(new GameStrategy(session, saves, presenter, cli, logger));

            presenter.Show("=== Characters CLI Mode Activated ===");
            presenter.Show(offline
                ? "Remote database: offline sample data"
                : $"Remote database: {RemoteApiUrl}");
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