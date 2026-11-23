using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameSystem.Core.Interfaces;
using Infra.Display;

namespace GameSystem.Core.Logging
{
    /// <summary>Adapts the game logger abstraction to the infrastructure displayer.</summary>
    public class DisplayerLogger : IGameLogger
    {
        private readonly IDisplayer _displayer;

        public DisplayerLogger(IDisplayer displayer)
        {
            _displayer = displayer;
        }

        public void Log(string message) => _displayer.Display(message);
    }
}
