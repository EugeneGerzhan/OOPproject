using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;
using Infra.Data;

namespace GameSystem.Engine
{
    /// <summary>Converts a battle in progress to its data model and back. The participants are saved as ids of the game state.</summary>
    public static class BattleSaveMapper
    {
        /// <exception cref="InvalidOperationException">A participant is not registered in the game state.</exception>
        public static BattleStateData ToData(Battle battle, GameState state)
        {
            ArgumentNullException.ThrowIfNull(battle);
            ArgumentNullException.ThrowIfNull(state);

            var ids = new List<int>();
            foreach (Character participant in battle.Participants)
            {
                int? id = state.Characters.IdOf(participant);
                if (id == null)
                {
                    throw new InvalidOperationException($"{participant.Name} is not registered in the game state.");
                }
                ids.Add(id.Value);
            }

            BattleProgress progress = battle.GetProgress();
            return new BattleStateData(
                ids,
                progress.Round,
                progress.Queue.ToList(),
                progress.QueueIndex,
                progress.Actions,
                progress.History
                    .Select(t => new TurnData(t.Number, t.Round, t.Actor, t.Action, t.Target, t.Messages.ToList()))
                    .ToList());
        }

        /// <exception cref="InvalidDataException">The data cannot be turned into a battle in progress.</exception>
        public static Battle ToBattle(BattleStateData data, GameState state, IGameLogger logger)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(logger);

            var participants = new List<Character>();
            foreach (int id in data.ParticipantIds)
            {
                if (!state.Characters.TryGet(id, out var character))
                {
                    throw new InvalidDataException($"Character {id} of the saved battle is missing in the save.");
                }
                participants.Add(character);
            }

            var history = data.History
                .Select(t => new TurnRecord(t.Number, t.Round, t.Actor, t.Action, t.Target, t.Messages.ToList()))
                .ToList();
            var progress = new BattleProgress(data.Round, data.Queue.ToList(), data.QueueIndex, data.Actions, history);

            try
            {
                return Battle.Restore(participants, logger, progress);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("The saved battle is not valid: " + ex.Message, ex);
            }
        }
    }
}