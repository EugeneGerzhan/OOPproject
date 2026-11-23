using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Core.Abilities
{
    public class RageAbility : IAbility
    {
        public string Name => "Rage";

        public void Execute(Character source, Character target, IGameLogger logger)
        {
            logger.Log($"{source.Name} enters a Rage! Attack increased, armor temporarily lowered.");
            source.ModifyAttackPower(10);
            source.ModifyArmor(-2);
            source.Attack(target, logger);
        }
    }
}