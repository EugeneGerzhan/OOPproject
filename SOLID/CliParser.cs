using System;
using System.Collections.Generic;

namespace OOPproject.SOLID
{
    public class CliArguments
    {
        public List<string> Args { get; } = new List<string>();
        public Dictionary<string, string> Options { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CliArguments(string[] rawArgs)
        {
            string currentOption = string.Empty;

            foreach (var arg in rawArgs)
            {
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    currentOption = arg;
                    if (!Options.ContainsKey(currentOption))
                    {
                        Options[currentOption] = string.Empty;
                    }
                }
                else if (!string.IsNullOrEmpty(currentOption))
                {
                    Options[currentOption] = arg;
                    currentOption = string.Empty;
                }
                else
                {
                    Args.Add(arg);
                }
            }
        }
    }
}