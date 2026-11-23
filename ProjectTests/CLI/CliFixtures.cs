using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Logging;
using Infra.Data;
using OOPproject.SOLID;
using OOPTextProject.TextSystem;
using ProjectTests.Mocks;

namespace ProjectTests.CliTests
{
    /// <summary>Wires the text CLI to test doubles. Run() executes one command and returns what it printed.</summary>
    public class TextCliFixture
    {
        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public InMemoryStorage Storage { get; } = new InMemoryStorage();
        public TextFactory Factory { get; } = new TextFactory("Doc");
        public CommandInterpreter Interpreter { get; }

        public TextCliFixture()
        {
            var ui = new Presenter(Displayer, Input);
            Interpreter = new CommandInterpreter(ui);
            var handler = new TextCliHandler(Factory, ui, new SaveManager(Storage));
            handler.SetupCommands(Interpreter);
        }

        public IReadOnlyList<string> Run(string command, params string[] answers)
        {
            Displayer.Reset();
            Input.Clear();
            Input.Enqueue(answers);
            Interpreter.Execute(command);
            return Displayer.Lines.ToList();
        }
    }

    /// <summary>Wires the characters CLI to test doubles.</summary>
    public class CharactersCliFixture
    {
        public DisplayerMock Displayer { get; } = new DisplayerMock();
        public InputReaderStub Input { get; } = new InputReaderStub();
        public InMemoryStorage Storage { get; } = new InMemoryStorage();
        public CommandInterpreter Interpreter { get; }

        public CharactersCliFixture()
        {
            var ui = new Presenter(Displayer, Input);
            Interpreter = new CommandInterpreter(ui);
            var handler = new CharactersCliHandler(new DisplayerLogger(Displayer), ui, new SaveManager(Storage));
            handler.SetupCommands(Interpreter);
        }

        public IReadOnlyList<string> Run(string command, params string[] answers)
        {
            Displayer.Reset();
            Input.Clear();
            Input.Enqueue(answers);
            Interpreter.Execute(command);
            return Displayer.Lines.ToList();
        }

        // Shortcuts that go through the real dialogs.
        public void CreateCharacter(string name, string classKey) => Run("create char", name, classKey, "");

        public void CreateItem(string name, int health, int armor, int attack) =>
            Run("create item", name, health.ToString(), armor.ToString(), attack.ToString(), "");

        public void CreateCustomAbility(string name, int hits, int bonus) =>
            Run("create ability", "custom", name, hits.ToString(), bonus.ToString(), "");
    }
}