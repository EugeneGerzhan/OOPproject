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

    /// <summary>The data a renderer needs to show the state of a battle.</summary>
    public record BattleBoard(int Round, string CurrentName, IReadOnlyList<BoardRow> Rows, bool IsOver, string? Winner);

    /// <summary>The result of a finished battle.</summary>
    public record BattleOutcome(string? Winner, IReadOnlyList<string> Participants, int Rounds, int Actions);

    /// <summary>
    /// A turn-based battle of two or more characters. The characters act in the given order,
    /// one action per turn. A round ends when everybody who is alive has acted; defense lasts until
    /// the next round. The battle ends when at most one character is left standing.
    /// Rule violations are reported with <see cref="InvalidOperationException"/>; the turn is not spent.
    /// </summary>
    public class Battle
    {
        private readonly List<Character> _participants;
        private readonly IGameLogger _logger;
        private int _turn;

        /// <exception cref="ArgumentException">Fewer than two participants, a repeated one or a defeated one.</exception>
        public Battle(IEnumerable<Character> participants, IGameLogger logger)
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
            Round = 1;
            foreach (var participant in _participants)
            {
                participant.ResetDefense();
            }
        }

        public IReadOnlyList<Character> Participants => _participants.AsReadOnly();

        /// <summary>The character whose turn it is.</summary>
        public Character Current => _participants[_turn];

        public int Round { get; private set; }

        /// <summary>How many actions have been made.</summary>
        public int Actions { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>The last character standing; null while the battle goes on or when nobody is left.</summary>
        public Character? Winner { get; private set; }

        /// <summary>The result of the battle; null while it goes on.</summary>
        public BattleOutcome? Outcome => IsOver
            ? new BattleOutcome(Winner?.Name, _participants.Select(p => p.Name).ToList(), Round, Actions)
            : null;

        // ---------- actions of the current character ----------

        public void Attack(Character target)
        {
            EnsureRunning();
            RequireOpponent(target);

            Current.Attack(target, _logger);
            EndTurn();
        }

        public void Defend()
        {
            EnsureRunning();

            Current.PrepareDefense(_logger);
            EndTurn();
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

            target.Heal(amount, _logger);
            EndTurn();
        }

        /// <param name="target">The opponent.</param>
        /// <param name="ability">An ability the character knows; its unique ability when null.</param>
        public void UseAbility(Character target, IAbility? ability = null)
        {
            EnsureRunning();
            RequireOpponent(target);

            if (ability == null)
            {
                Current.UseSpecialAbility(target, _logger);
            }
            else
            {
                if (!Current.HasAbility(ability))
                {
                    throw new InvalidOperationException($"{Current.Name} does not know '{ability.Name}'.");
                }
                Current.UseAbility(ability, target, _logger);
            }
            EndTurn();
        }

        /// <summary>The state of the battle as a renderable (the presentation layer sets the renderer).</summary>
        public IRenderable<BattleBoard> CreateView()
        {
            var rows = _participants
                .Select(p => new BoardRow(p.Name, p.Health, p.MaxHealth, p.IsAlive, p.IsDefending,
                    !IsOver && ReferenceEquals(p, Current)))
                .ToList();

            return new Renderable<BattleBoard>(
                new BattleBoard(Round, IsOver ? string.Empty : Current.Name, rows, IsOver, Winner?.Name));
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
                _turn++;
                if (_turn == _participants.Count)
                {
                    _turn = 0;
                    Round++;
                    foreach (var participant in _participants)
                    {
                        participant.ResetDefense();
                    }
                    _logger.Log($"--- Round {Round} ---");
                }
            }
            while (!_participants[_turn].IsAlive);
        }
    }
}