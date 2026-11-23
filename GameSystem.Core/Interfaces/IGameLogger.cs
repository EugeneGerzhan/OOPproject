using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameSystem.Core.Interfaces
{
    /// <summary>
    /// Contract for handling game output logs.
    /// </summary>
    public interface IGameLogger
    {
        void Log(string message);
    }
}
