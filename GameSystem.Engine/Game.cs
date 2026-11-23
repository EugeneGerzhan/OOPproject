using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Engine
{
    /// <summary>
    /// Controls game state, round execution, and character interactions.
    /// </summary>
    public class Game
    {
        private readonly List<Character> _participants;
        private readonly IGameLogger _logger;

        public int CurrentRound { get; private set; }
        public IReadOnlyList<Character> Participants => _participants.AsReadOnly();

        public Game(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _participants = new List<Character>();
            CurrentRound = 0;
        }

        public void AddCharacter(Character character)
        {
            _participants.Add(character);
            _logger.Log($"Character registered: {character.Name}");
        }

        public void StartRound()
        {
            CurrentRound++;
            _logger.Log($"\n--- Round {CurrentRound} ---");
            foreach (var character in _participants)
            {
                character.ResetDefense();
            }
        }

        public void ExecuteAttack(Character attacker, Character target)
        {
            _logger.Log($"[Action] {attacker.Name} turn:");
            attacker.Attack(target, _logger);
        }

        public void ExecuteAbility(Character source, Character target)
        {
            _logger.Log($"[Action] {source.Name} turn:");
            source.UseSpecialAbility(target, _logger);
        }

        public void ExecuteDefend(Character character)
        {
            _logger.Log($"[Action] {character.Name} turn:");
            character.PrepareDefense(_logger);
        }

        public void ExecuteHeal(Character character, int amount)
        {
            _logger.Log($"[Action] {character.Name} turn:");
            character.Heal(amount, _logger);
        }
    }
}