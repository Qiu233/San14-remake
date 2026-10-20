using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>Current ruler's doctrine and the force's saved experience level.</summary>
internal sealed record ForceDoctrineView(string? Name, ForceDoctrineProgress Progress)
{
    internal int? Level => Progress.Level;
    internal static ForceDoctrineView Create(GameState world, int forceId)
    {
        ForceState force = world.Forces[forceId];
        OfficerState? ruler = force.RulerId is int rulerId ? world.Officers.GetValueOrDefault(rulerId) : null;
        string? name = ruler?.DoctrineId is int doctrineId ? world.Doctrines.GetValueOrDefault(doctrineId)?.Name : null;
        return new(name, OwnedForce164Inputs.ReadProgress(world, forceId));
    }
}
