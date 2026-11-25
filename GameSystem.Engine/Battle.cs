using System;
using System.Collections.Generic;
using System.Linq;
using GameSystem.Core.Abilities;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Infra.Display;

namespace GameSystem.Engine
{
    /// <summary>One line of the battle board.</summary>
    public record BoardRow(string Name, int Health, int MaxHealth, bool IsAlive, bool IsDefending, bool IsCurrent);

    /// <summary>The data a renderer needs to show the state of a battle (Order: who acts next, starting with the current one).</summary>
    public record BattleBoard(int Round, string CurrentName, IReadOnlyList<BoardRow> Rows, bool IsOver, string? Winner,
                              IReadOnlyList<string>? Order = null);

    /// <summary>The result of a finished battle.</summary>
    public record BattleOutcome(string? Winner, IReadOnlyList<string> Participants, int Rounds, int Actions);

    /// <summary>One step of the history of a battle: who did what to whom and what was reported.</summary>
    public record TurnRecord(int Number, int Round, string Actor, string Action, string? Target, IReadOnlyList<string> Messages);

    /// <summary>Everything besides the characters that is needed to continue a battle later.</summary>
    public record BattleProgress(int Round, IReadOnlyList<int> Queue, int QueueIndex, int Actions, IReadOnlyList<TurnRecord> History);

    /// <summary>
    /// A turn-based battle of two or more characters.
    /// <para>
    /// A round is a sequence of actions in the order of a queue. The queue is built at the start of every round:
    /// a character with speed S gets max(1, S div 100) actions, the k-th of them at the time k x 100 / S;
    /// the actions are sorted by time and, at equal time, by the position of the character in the party.
    /// With equal speeds this is simply "in turn". The battle ends when at most one character is left standing.
    /// </para>
    /// <para>
    /// Rule violations are reported with <see cref="InvalidOperationException"/> and the turn is not spent.
    /// Every action is written to <see cref="History"/>, and <see cref="GetProgress"/> / <see cref="Restore"/>
    /// let a battle in progress be saved and continued.
    /// </para>
    /// </summary>
    public class Battle
    {
        /// <summary>Passes the messages on and keeps them, so a step of the history knows what was reported.</summary>
        private sealed class RecordingLogger : IGameLogger
        {
            private readonly IGameLogger _inner;

            public RecordingLogger(IGameLogger inner)
            {
                _inner = inner;
            }

            public List<string> Lines { get; } = new List<string>();

            public void Log(string message)
            {
                Lines.Add(message);
                _inner.Log(message);
            }
        }

        private readonly List<Character> _participants;
        private readonly IGameLogger _logger;
        private readonly GameRules _rules;
        private readonly List<TurnRecord> _history = new List<TurnRecord>();
        private List<int> _queue = new List<int>();
        private int _queueIndex;

        /// <exception cref="ArgumentException">Fewer than two participants, a repeated one or a defeated one.</exception>
        public Battle(IEnumerable<Character> participants, IGameLogger logger, GameRules? rules = null)
        {
            ArgumentNullException.ThrowIfNull(participants);
            ArgumentNullException.ThrowIfNull(logger);

            _participants = participants.ToList();
            if (_participants.Count < 2)
            {
                throw new ArgumentException("A battle needs at least two participants.");
            }
            if (_participants.Distinct().Count() != _participants.Count)
            {
                throw new ArgumentException("A character cannot take part in a battle twice.");
            }
            if (_participants.Any(p => !p.IsAlive))
            {
                throw new ArgumentException("Defeated characters cannot take part in a battle.");
            }

            _logger = logger;
            _rules = rules ?? GameRules.Default;
            Round = 1;
            foreach (var participant in _participants)
            {
                participant.ResetDefense();
            }
            _queue = BuildQueue();
        }

        private Battle(List<Character> participants, IGameLogger logger, GameRules rules, BattleProgress progress)
        {
            _participants = participants;
            _logger = logger;
            _rules = rules;
            Round = progress.Round;
            Actions = progress.Actions;
            _queue = progress.Queue.ToList();
            _queueIndex = progress.QueueIndex;
            _history.AddRange(progress.History);
        }

        /// <summary>Continues a battle that was saved with <see cref="GetProgress"/>.</summary>
        /// <exception cref="ArgumentException">The participants or the progress cannot belong to a battle in progress.</exception>
        public static Battle Restore(IEnumerable<Character> participants, IGameLogger logger,
                                     BattleProgress progress, GameRules? rules = null)
        {
            ArgumentNullException.ThrowIfNull(participants);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(progress);

            var list = participants.ToList();
            if (list.Count < 2)
            {
                throw new ArgumentException("A battle needs at least two participants.");
            }
            if (list.Distinct().Count() != list.Count)
            {
                throw new ArgumentException("A character cannot take part in a battle twice.");
            }
            if (progress.Round < 1 || progress.Actions < 0)
            {
                throw new ArgumentException("The saved progress is not valid.");
            }
            if (progress.Queue.Count == 0
                || progress.QueueIndex < 0
                || progress.QueueIndex >= progress.Queue.Count
                || progress.Queue.Any(i => i < 0 || i >= list.Count))
            {
                throw new ArgumentException("The saved turn order is not valid.");
            }
            if (list.Count(p => p.IsAlive) < 2)
            {
                throw new ArgumentException("A battle in progress needs at least two living characters.");
            }

            var battle = new Battle(list, logger, rules ?? GameRules.Default, progress);
            if (!battle.Current.IsAlive)
            {
                throw new ArgumentException("The saved turn belongs to a defeated character.");
            }
            return battle;
        }

        public IReadOnlyList<Character> Participants => _participants.AsReadOnly();

        /// <summary>The character whose turn it is.</summary>
        public Character Current => _participants[_queue[_queueIndex]];

        public int Round { get; private set; }

        /// <summary>How many actions have been made.</summary>
        public int Actions { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>The last character standing; null while the battle goes on or when nobody is left.</summary>
        public Character? Winner { get; private set; }

        /// <summary>The steps made so far, the first one first.</summary>
        public IReadOnlyList<TurnRecord> History => _history.AsReadOnly();

        /// <summary>The names of the living characters in the order they act in the rest of the round (the current one first).</summary>
        public IReadOnlyList<string> UpcomingOrder =>
            _queue.Skip(_queueIndex).Where(i => _participants[i].IsAlive).Select(i => _participants[i].Name).ToList();

        /// <summary>The result of the battle; null while it goes on.</summary>
        public BattleOutcome? Outcome => IsOver
            ? new BattleOutcome(Winner?.Name, _participants.Select(p => p.Name).ToList(), Round, Actions)
            : null;

        /// <summary>A copy of everything needed (besides the characters) to continue the battle later.</summary>
        public BattleProgress GetProgress() =>
            new BattleProgress(Round, _queue.ToList(), _queueIndex, Actions, _history.ToList());

        // ---------- actions of the current character ----------

        public void Attack(Character target)
        {
            EnsureRunning();
            RequireOpponent(target);

            Act("attack", target, log => Current.Attack(target, log));
        }

        public void Defend()
        {
            EnsureRunning();

            Act("defend", null, log => Current.PrepareDefense(log));
        }

        /// <summary>Heals a participant (also the acting character itself).</summary>
        public void Heal(Character target, int amount)
        {
            EnsureRunning();
            RequireParticipant(target);
            if (!target.IsAlive)
            {
                throw new InvalidOperationException($"{target.Name} is defeated and cannot be healed.");
            }
            if (amount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be positive.");
            }

            Act("heal", target, log => target.Heal(amount, log));
        }

        /// <param name="target">The opponent.</param>
        /// <param name="ability">An ability the character knows; its unique ability when null.</param>
        public void UseAbility(Character target, IAbility? ability = null)
        {
            EnsureRunning();
            RequireOpponent(target);

            if (ability != null && !Current.HasAbility(ability))
            {
                throw new InvalidOperationException($"{Current.Name} does not know '{ability.Name}'.");
            }

            string name = ability?.Name ?? Current.UniqueAbility.Name;
            Act($"ability {name}", target, log =>
            {
                if (ability == null)
                {
                    Current.UseSpecialAbility(target, log);
                }
                else
                {
                    Current.UseAbility(ability, target, log);
                }
            });
        }

        /// <summary>The state of the battle as a renderable (the presentation layer sets the renderer).</summary>
        public IRenderable<BattleBoard> CreateView()
        {
            var rows = _participants
                .Select(p => new BoardRow(p.Name, p.Health, p.MaxHealth, p.IsAlive, p.IsDefending,
                    !IsOver && ReferenceEquals(p, Current)))
                .ToList();

            return new Renderable<BattleBoard>(new BattleBoard(
                Round,
                IsOver ? string.Empty : Current.Name,
                rows,
                IsOver,
                Winner?.Name,
                IsOver ? Array.Empty<string>() : UpcomingOrder));
        }

        // ---------- rules ----------

        private void EnsureRunning()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The battle is over.");
            }
        }

        private void RequireParticipant(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!_participants.Contains(target))
            {
                throw new InvalidOperationException($"{target.Name} is not taking part in this battle.");
            }
        }

        private void RequireOpponent(Character target)
        {
            RequireParticipant(target);
            if (ReferenceEquals(target, Current))
            {
                throw new InvalidOperationException($"{Current.Name} cannot target itself with this action.");
            }
            if (!target.IsAlive)
            {
                throw new InvalidOperationException($"{target.Name} is already defeated.");
            }
        }

        /// <summary>Performs the action, writes it to the history and passes the turn on.</summary>
        private void Act(string action, Character? target, Action<IGameLogger> perform)
        {
            Character actor = Current;
            var recorder = new RecordingLogger(_logger);

            perform(recorder);

            _history.Add(new TurnRecord(_history.Count + 1, Round, actor.Name, action, target?.Name, recorder.Lines.ToList()));
            EndTurn();
        }

        private void EndTurn()
        {
            Actions++;

            var alive = _participants.Where(p => p.IsAlive).ToList();
            if (alive.Count <= 1)
            {
                IsOver = true;
                Winner = alive.FirstOrDefault();
                _logger.Log(Winner != null
                    ? $"{Winner.Name} wins the battle!"
                    : "Nobody is left standing: the battle ends in a draw.");
                return;
            }

            do
            {
                _queueIndex++;
                if (_queueIndex >= _queue.Count)
                {
                    Round++;
                    foreach (var participant in _participants)
                    {
                        participant.ResetDefense();
                    }
                    _queue = BuildQueue();
                    _queueIndex = 0;
                    _logger.Log($"--- Round {Round} ---");
                }
            }
            while (!Current.IsAlive);
        }

        /// <summary>The order of the actions of a round (indices of the participants), see the class description.</summary>
        private List<int> BuildQueue()
        {
            var slots = new List<(double Time, int Index)>();

            for (int i = 0; i < _participants.Count; i++)
            {
                Character participant = _participants[i];
                if (!participant.IsAlive)
                {
                    continue;
                }

                int speed = Math.Max(1, participant.Speed);
                int actions = Math.Max(1, speed / _rules.BaseSpeed);
                for (int k = 1; k <= actions; k++)
                {
                    slots.Add((k * (double)_rules.BaseSpeed / speed, i));
                }
            }

            return slots.OrderBy(s => s.Time).ThenBy(s => s.Index).Select(s => s.Index).ToList();
        }
    }
}