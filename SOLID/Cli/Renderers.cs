using System;
using System.Collections.Generic;
using GameSystem.Engine;
using Infra.Data;
using Infra.Display;

namespace OOPproject.SOLID;

// Console renderers: the way data is turned into text belongs to the presentation layer. A GUI would
// provide its own IRenderer implementations for the same data.

/// <summary>Shows the board of a battle.</summary>
public sealed class BattleBoardRenderer : IRenderer<BattleBoard>
{
    public string Render(BattleBoard board)
    {
        var lines = new List<string>
        {
            board.IsOver ? $"=== BATTLE OVER (round {board.Round}) ===" : $"=== BATTLE (round {board.Round}) ==="
        };

        foreach (BoardRow row in board.Rows)
        {
            lines.Add($"{(row.IsCurrent ? ">" : " ")} {row.Name,-14} HP {row.Health}/{row.MaxHealth}  [{Status(row)}]");
        }

        if (board.IsOver)
        {
            lines.Add(board.Winner != null ? $"Winner: {board.Winner}" : "Draw: nobody is left standing.");
        }
        else
        {
            lines.Add($"Turn: {board.CurrentName}");
            if (board.Order is { Count: > 0 })
            {
                lines.Add($"Order: {string.Join(" > ", board.Order)}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Status(BoardRow row) => !row.IsAlive ? "defeated" : row.IsDefending ? "defending" : "ready";
}

/// <summary>Shows the characters that can be chosen for a battle.</summary>
public sealed class RosterRenderer : IRenderer<RosterView>
{
    public string Render(RosterView view)
    {
        var lines = new List<string> { "=== AVAILABLE CHARACTERS ===" };

        if (view.Rows.Count == 0)
        {
            lines.Add("  (none) - create one with 'create char' or import one with 'remote import char <id>'");
        }
        foreach (RosterRow row in view.Rows)
        {
            string state = row.IsAlive ? string.Empty : " - defeated";
            lines.Add($"  [{row.Id}] {row.Name} ({row.ClassName}) HP {row.Health}/{row.MaxHealth}{state}");
        }

        lines.Add("Start a battle with: game start <name|id> <name|id> [...]");
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>Shows the saved results of the battles.</summary>
public sealed class ResultsRenderer : IRenderer<IReadOnlyList<BattleResultData>>
{
    public string Render(IReadOnlyList<BattleResultData> results)
    {
        var lines = new List<string> { "=== BATTLE RESULTS ===" };

        if (results.Count == 0)
        {
            lines.Add("  (no battles have been finished yet)");
        }
        foreach (BattleResultData result in results)
        {
            string outcome = result.Winner != null ? $"{result.Winner} wins" : "draw";
            string who = string.Join(" vs ", result.Participants);
            lines.Add($"  {result.FinishedAt:yyyy-MM-dd HH:mm}  {outcome} ({who}, {result.Rounds} round(s), {result.Actions} action(s))");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>Shows the history of a battle: every step with the messages it caused.</summary>
public sealed class TurnHistoryRenderer : IRenderer<IReadOnlyList<TurnRecord>>
{
    public string Render(IReadOnlyList<TurnRecord> history)
    {
        var lines = new List<string> { "=== BATTLE HISTORY ===" };

        foreach (TurnRecord turn in history)
        {
            string target = turn.Target != null ? $" -> {turn.Target}" : string.Empty;
            lines.Add($"  #{turn.Number} (round {turn.Round}) {turn.Actor}: {turn.Action}{target}");
            foreach (string message in turn.Messages)
            {
                lines.Add($"      {message}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}