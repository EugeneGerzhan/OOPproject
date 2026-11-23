namespace GameSystem.Core.Models
{
    /// <summary>Holder of the current game state, shared by all command handlers (load replaces the state).</summary>
    public class GameSession
    {
        public GameState State { get; set; } = new GameState();
    }
}