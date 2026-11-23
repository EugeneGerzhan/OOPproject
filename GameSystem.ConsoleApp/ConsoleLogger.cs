using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using GameSystem.Core.Interfaces;

namespace GameSystem.ConsoleApp
{
    public class ConsoleLogger : IGameLogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}