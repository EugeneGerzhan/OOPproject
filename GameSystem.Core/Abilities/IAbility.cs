using GameSystem.Core.Interfaces;
using GameSystem.Core.Models;

namespace GameSystem.Core.Abilities
{
    /// <summary>
    /// STRATEGY pattern: an ability is an interchangeable algorithm of what happens when a character acts.
    /// <see cref="Character"/> (the context) does not know the details: it only calls <see cref="Execute"/>
    /// on its unique ability or on any ability it has learned. <c>RageAbility</c>, <c>FlurryOfBlowsAbility</c>
    /// and <c>CustomAbility</c> are the concrete strategies. A new ability is a new class, and
    /// <see cref="Character"/> stays untouched.
    /// </summary>
    public interface IAbility
    {
        string Name { get; }

        void Execute(Character source, Character target, IGameLogger logger);

        /// <summary>Short human-readable description of the effect.</summary>
        string Describe() => Name;
    }
}