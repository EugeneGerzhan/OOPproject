using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameSystem.Core.Models;
using GameSystem.Engine;

namespace GameSystem.ConsoleApp
{
    class Program
    {
        static void Main()
        {
            var logger = new ConsoleLogger();
            var game = new Game(logger);

            // Initialize characters
            var barbarian = new HalfOrcBarbarian("Growler");
            var monk = new Monk("Shi Fuu");

            game.AddCharacter(barbarian);
            game.AddCharacter(monk);

            // Equip items
            var axe = new Item("Greataxe of Destruction", attackBonus: 5);
            var wraps = new Item("Iron Handwraps", armorBonus: 3);

            barbarian.Equip(axe, logger);
            monk.Equip(wraps, logger);

            // Round 1
            game.StartRound();
            game.ExecuteDefend(monk);
            game.ExecuteAttack(barbarian, monk);

            // Round 2
            game.StartRound();
            game.ExecuteAbility(monk, barbarian);

            // Round 3
            game.StartRound();
            game.ExecuteAbility(barbarian, monk);

            // Round 4
            game.StartRound();
            game.ExecuteHeal(monk, 15);
        }
    }
}