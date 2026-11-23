using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameSystem.Core.Abilities;

namespace GameSystem.Core.Models
{
    public class HalfOrcBarbarian : Character
    {
        public HalfOrcBarbarian(string name)
            : base(name, maxHealth: 120, armor: 5, attackPower: 20, new RageAbility()) { }
    }

    public class Monk : Character
    {
        public Monk(string name)
            : base(name, maxHealth: 90, armor: 8, attackPower: 15, new FlurryOfBlowsAbility()) { }
    }
}