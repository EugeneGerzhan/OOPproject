using System;
using System.Collections.Generic;

namespace OOPproject.SOLID
{
    /// <summary>
    /// Compatibility adapter: the old interpreter API on top of <see cref="Cli"/>.
    /// Every registered command becomes a tiny strategy. New code should plug real strategies in with
    /// <see cref="Cli.UseStrategy"/> instead.
    /// </summary>
    public class CommandInterpreter : Cli
    {
        public CommandInterpreter(IPresenter ui) : base(ui)
        {
        }

        /// <summary>Registers a command object under a name (a command registered earlier under it is replaced).</summary>
        public void RegisterCommand(string name, ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var strategy = new SingleCommandStrategy();
            strategy.RegisterCommand(name, command);
            UseStrategy(strategy);
        }

        /// <summary>Registers a method as a command.</summary>
        public void RegisterCommand(string name, Action<string[], Dictionary<string, string>> action)
        {
            RegisterCommand(name, new DelegateCommand(action));
        }

        /// <summary>Runs a line typed by the user (the same as <see cref="Cli.ExecCommand"/>).</summary>
        public void Execute(string input) => ExecCommand(input);

        private sealed class SingleCommandStrategy : CommandStrategyBase
        {
        }
    }
}