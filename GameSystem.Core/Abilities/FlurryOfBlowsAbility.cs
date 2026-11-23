using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Core.Abilities
{
    public class FlurryOfBlowsAbility : IAbility
    {
        public string Name => "Flurry of Blows";

        public void Execute(Character source, Character target, IGameLogger logger)
        {
            logger.Log($"{source.Name} executes Flurry of Blows!");
            source.Attack(target, logger);
            source.Attack(target, logger);
        }
    }
}
