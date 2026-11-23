using Infra.Data;
using OOPTextProject.TextSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace OOPproject.SOLID
{
    /// <summary>Binds terminal commands of the Text system to TextFactory operations.</summary>
    public class TextCliHandler : CommandStrategyBase
    {
        private readonly TextFactory _textFactory;
        private readonly IPresenter _ui;
        private readonly SaveManager _saves;
        // Caretaker of the Memento pattern: the snapshots that make `undo` possible.
        private readonly UndoHistory _history = new UndoHistory();

        public TextCliHandler(TextFactory textFactory, IPresenter ui, SaveManager saves)
        {
            _textFactory = textFactory;
            _ui = ui;
            _saves = saves;
            RegisterCommands();
        }

        private void RegisterCommands()
        {
            Register("pwd", HandlePwd);
            Register("print", HandlePrint);
            Register("add", Undoable(HandleAdd));
            Register("rm", Undoable(HandleRm));
            Register("up", HandleUp);
            Register("cd", HandleCd);
            Register("cp", Undoable(HandleCp));
            Register("save", HandleSave);
            Register("load", Undoable(HandleLoad));
            Register("saves", HandleSaves);
            Register("undo", HandleUndo);
            Register("help", HandleHelp);
        }

        /// <summary>
        /// Command + Memento: makes a command undoable. Only the commands that change the document are wrapped;
        /// moving around (cd, up) and printing are not steps of the history.
        /// </summary>
        private ICommand Undoable(Action<string[], Dictionary<string, string>> action) =>
            new UndoableCommand(new DelegateCommand(action), _textFactory, _history);

        // pwd
        private void HandlePwd(string[] args, Dictionary<string, string> opts)
        {
            _ui.Show($"Current Document Path: {_textFactory.GetCurrentPath()}");
        }

        // print [--whole] [--id] [--format plain|html|markdown]
        private void HandlePrint(string[] args, Dictionary<string, string> opts)
        {
            if (!FormatOption.TryResolve(opts, _ui, out var formats))
            {
                return;
            }

            bool whole = opts.ContainsKey("--whole");
            bool showIds = opts.ContainsKey("--id");

            _ui.Show(whole
                ? _textFactory.GetWholeView(showIds, formats)
                : _textFactory.GetCurrentView(showIds, formats));
        }

        // add <container|leaf> <type> ["text"]   or shortcut   add <heading|paragraph> ["text"]
        private void HandleAdd(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: add <container|leaf> <type> [\"text\"]   (types: heading, paragraph)";

            if (args.Length == 0)
            {
                _ui.Show(usage);
                return;
            }

            string category = string.Empty;
            string type;
            int textStart;

            string first = args[0].ToLowerInvariant();
            if (first == "container" || first == "leaf")
            {
                if (args.Length < 2)
                {
                    _ui.Show(usage);
                    return;
                }
                category = first;
                type = args[1].ToLowerInvariant();
                textStart = 2;
            }
            else
            {
                type = first; // shortcut: add heading "title"
                textStart = 1;
            }

            string inlineText = string.Join(" ", args.Skip(textStart));

            if (type == "heading" && category != "leaf")
            {
                string title = ReadValue("Enter heading title: ", inlineText);
                var heading = _textFactory.AddHeading(title);
                _ui.Show($"Heading '{heading.Name}' added (id {heading.Id}). Current path: {_textFactory.GetCurrentPath()}");
            }
            else if (type == "paragraph" && category != "container")
            {
                string content = ReadValue("Enter paragraph content: ", inlineText);
                var paragraph = _textFactory.AddParagraph(content);
                _ui.Show($"Paragraph added (id {paragraph.Id}).");
            }
            else
            {
                _ui.Show($"Unknown element: '{string.Join(" ", args.Take(textStart))}'. {usage}");
            }
        }

        // rm [<elem_name>] | rm --id <element_id>
        private void HandleRm(string[] args, Dictionary<string, string> opts)
        {
            IText? target;
            string error;
            bool found;

            if (opts.TryGetValue("--id", out var idText))
            {
                if (!int.TryParse(idText, out int id))
                {
                    _ui.Show("Usage: rm [<elem_name>] | rm --id <element_id>");
                    return;
                }
                found = _textFactory.TryFindById(id, out target, out error);
            }
            else if (args.Length > 0)
            {
                found = _textFactory.TryFindChild(string.Join(" ", args), out target, out error);
            }
            else
            {
                target = _textFactory.Current; // no name -> remove the current element
                error = string.Empty;
                found = true;
            }

            if (!found || target == null)
            {
                _ui.Show(error);
                return;
            }

            if (target is Container container && container.IsRoot())
            {
                _ui.Show("Root cannot be removed.");
                return;
            }

            if (!_ui.Confirm($"Are you sure you want to remove {Describe(target)}? (y/n): "))
            {
                _ui.Show("Removal cancelled.");
                return;
            }

            if (_textFactory.TryRemove(target, out error))
            {
                _ui.Show($"Element removed. Current path: {_textFactory.GetCurrentPath()}");
            }
            else
            {
                _ui.Show(error);
            }
        }

        // up
        private void HandleUp(string[] args, Dictionary<string, string> opts)
        {
            if (_textFactory.Up())
            {
                _ui.Show($"Moved up to parent element. Current path: {_textFactory.GetCurrentPath()}");
            }
            else
            {
                _ui.Show("You are already in root.");
            }
        }

        // cd <path> | cd --id <container_id>
        private void HandleCd(string[] args, Dictionary<string, string> opts)
        {
            string error;
            bool success;

            if (opts.TryGetValue("--id", out var idText))
            {
                if (!int.TryParse(idText, out int id))
                {
                    _ui.Show("Usage: cd <path> | cd --id <container_id>");
                    return;
                }
                success = _textFactory.TryCdById(id, out error);
            }
            else
            {
                string path = string.Join(" ", args);
                if (string.IsNullOrWhiteSpace(path))
                {
                    _ui.Show("Usage: cd <path> | cd --id <container_id>");
                    return;
                }
                success = _textFactory.TryCd(path, out error);
            }

            _ui.Show(success ? $"Current path: {_textFactory.GetCurrentPath()}" : error);
        }

        // save <name>
        private void HandleSave(string[] args, Dictionary<string, string> opts)
        {
            string name = string.Join(" ", args).Trim();
            if (name.Length == 0)
            {
                _ui.Show("Usage: save <name>");
                return;
            }

            _saves.SaveText(TextMapper.ToSave(name, _textFactory.Document));
            _ui.Show($"Document saved as '{name}' ({_saves.Location}).");
        }

        // load <name>
        private void HandleLoad(string[] args, Dictionary<string, string> opts)
        {
            string name = string.Join(" ", args).Trim();
            if (name.Length == 0)
            {
                _ui.Show("Usage: load <name>");
                return;
            }

            if (!_saves.TryLoadText(name, out var save))
            {
                _ui.Show($"Save '{name}' not found. Use 'saves' to list available saves.");
                return;
            }

            var root = TextMapper.ToRoot(save); // validates the save before anything is replaced

            if (!_ui.Confirm("The current document will be replaced. Continue? (y/n): "))
            {
                _ui.Show("Loading cancelled.");
                return;
            }

            _textFactory.Load(root);
            _ui.Show($"Document '{save.SaveName}' loaded. Current path: {_textFactory.GetCurrentPath()}");
        }

        // saves
        private void HandleSaves(string[] args, Dictionary<string, string> opts)
        {
            var list = _saves.ListTextSaves();
            if (list.Count == 0)
            {
                _ui.Show($"No saved documents ({_saves.Location}).");
                return;
            }

            _ui.Show("=== SAVED DOCUMENTS ===");
            foreach (var info in list)
            {
                _ui.Show($"  {info.Name} ({info.LastModified:yyyy-MM-dd HH:mm})");
            }
        }
        // cp <elem_name> [--as <new name>]  |  cp --id <element_id> [--as <new name>]
        private void HandleCp(string[] args, Dictionary<string, string> opts)
        {
            const string usage = "Usage: cp <elem_name> [--as <new name>] | cp --id <element_id> [--as <new name>]";

            IText? source;
            string error;
            bool found;

            if (opts.TryGetValue("--id", out var idText))
            {
                if (!int.TryParse(idText, out int id))
                {
                    _ui.Show(usage);
                    return;
                }
                found = _textFactory.TryFindById(id, out source, out error);
            }
            else if (args.Length > 0)
            {
                found = _textFactory.TryFindChild(string.Join(" ", args), out source, out error);
            }
            else
            {
                _ui.Show(usage);
                return;
            }

            if (!found || source == null)
            {
                _ui.Show(error);
                return;
            }

            opts.TryGetValue("--as", out var newName);
            var copy = _textFactory.AddCopy(source, string.IsNullOrWhiteSpace(newName) ? null : newName);

            _ui.Show($"Copied {Describe(source)} as '{copy.Name}' (id {copy.Id}, {CountElements(copy)} element(s)).");
        }

        private static int CountElements(IText element) =>
    element is Container container ? container.SelfAndDescendants().Count() : 1;

        // undo
        private void HandleUndo(string[] args, Dictionary<string, string> opts)
        {
            if (!_history.TryPop(out var memento))
            {
                _ui.Show("Nothing to undo.");
                return;
            }

            _textFactory.Restore(memento);
            _ui.Show($"Undone. Current path: {_textFactory.GetCurrentPath()} ({_history.Count} more step(s) can be undone).");
        }

        private void HandleHelp(string[] args, Dictionary<string, string> opts)
        {
            _ui.Show("Available commands:");
            _ui.Show("  pwd                               - show current path");
            _ui.Show("  print [--whole] [--id]            - print current element (or whole document), --id shows element ids");
            _ui.Show("  add <container|leaf> <type> [\"text\"] - add element (heading, paragraph); e.g. add container heading \"Title\"");
            _ui.Show("  rm [<elem_name>] | rm --id <id>   - remove child by name/id, or the current element if no name given");
            _ui.Show("  up                                - go to parent element");
            _ui.Show("  cd <path> | cd --id <id>          - go to a container by path (/a/b, ../x) or id");
            _ui.Show("  print ... [--format plain|html|markdown] - choose the output format");
            _ui.Show("  cp <elem_name> [--as <name>] | cp --id <id> - copy an element (with its content) into the current one");
            _ui.Show("  save <name>                       - save the document to the JSON file");
            _ui.Show("  load <name>                       - load a saved document");
            _ui.Show("  saves                             - list saved documents");
            _ui.Show("  undo                              - undo the last change of the document (add, rm, cp, load)");
            _ui.Show("  exit                              - quit");
        }

        // ---------- helpers ----------

        private string ReadValue(string prompt, string inlineValue)
        {
            return string.IsNullOrWhiteSpace(inlineValue) ? _ui.Prompt(prompt) : inlineValue;
        }

        private static string Describe(IText element)
        {
            string name = element.Name.Length > 30 ? element.Name.Substring(0, 30) + "..." : element.Name;
            return $"{element.GetType().Name} '{name}' (id {element.Id})";
        }
    }
}