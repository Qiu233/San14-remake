using System.Numerics;

namespace San14.Data;

public enum OrdinaryTacticViewKind { Army, City, Gate }

/// <summary>Army IDs are persistent Army receiver IDs. City IDs are physical
/// slots1..51; Gate IDs are physical slots1..10, not canonical location IDs.</summary>
public readonly record struct OrdinaryTacticViewIdentity(OrdinaryTacticViewKind Kind, int Id);

/// <summary>Facts supplied by the actual map view owner. Null ViewPosition
/// means known absent; UnresolvedOwner means the fact is unavailable.</summary>
public sealed record OrdinaryTacticViewPose(bool NativeValid, Vector4? ViewPosition,
    string? UnresolvedOwner = null)
{
    public Vector4 Resolve()
    {
        if (UnresolvedOwner is not null) throw new InvalidDataException(UnresolvedOwner);
        //16ED70 initializes this vector before validity/view-pointer checks.
        return NativeValid && ViewPosition is Vector4 value ? value : new(0f, 0f, 0f, 1f);
    }
}

public sealed record OrdinaryTacticLinkedActorPublication(int SlotIndex,
    OrdinaryTacticActorPublication Actor, OrdinaryTacticViewIdentity ViewIdentity);
public readonly record struct OrdinaryTacticBeamPlacement(int SlotIndex, Matrix4x4 Transform);

/// <summary>The primary actor has no beam handle. The10 possible handles
/// belong to the linked actors in original successful-append order. Each
/// actor's two result vectors remain inside its actor; victims are not beams.</summary>
public sealed record OrdinaryTacticBeamPublication(OrdinaryTacticActorPublication PrimaryActor,
    OrdinaryTacticViewIdentity PrimaryView,
    IReadOnlyList<OrdinaryTacticLinkedActorPublication> LinkedActors)
{
    internal static OrdinaryTacticBeamPublication Read(IReadOnlyList<OrdinaryTacticActorPublication> actors)
    {
        if (actors.Count is < 1 or > 11)
            throw new InvalidDataException("tactic.presentation.linked_actor_count");
        var linked = new List<OrdinaryTacticLinkedActorPublication>(actors.Count - 1);
        for (int index = 1; index < actors.Count; index++)
        {
            var view = Identity(actors[index]);
            //178990 only admits Base5/6 as the header, never a linked slot.
            if (view.Kind != OrdinaryTacticViewKind.Army)
                throw new InvalidDataException("tactic.presentation.linked_actor_kind");
            linked.Add(new(index - 1, actors[index], view));
        }
        return new(actors[0], Identity(actors[0]), linked.AsReadOnly());
    }

    /// <summary>172910's one-time placement. The callback reads actual view
    /// vectors; it must not derive a position from a Hex. A zero origin skips
    /// the linked reads just as the original caller does.</summary>
    public AreaAdministrationQuery<IReadOnlyList<OrdinaryTacticBeamPlacement>> CreatePlacements(
        Func<OrdinaryTacticViewIdentity, OrdinaryTacticViewPose> readView)
    {
        ArgumentNullException.ThrowIfNull(readView);
        try
        {
            Vector4 origin = readView(PrimaryView).Resolve();
            if (!OrdinaryTacticBeamMath.HasOrigin(origin))
                return AreaAdministrationQuery<IReadOnlyList<OrdinaryTacticBeamPlacement>>.Known([]);
            var placements = new List<OrdinaryTacticBeamPlacement>(LinkedActors.Count);
            foreach (var linked in LinkedActors)
            {
                Vector4 endpoint = readView(linked.ViewIdentity).Resolve();
                if (OrdinaryTacticBeamMath.TryCreate(origin, endpoint, out var transform))
                    placements.Add(new(linked.SlotIndex, transform));
            }
            return AreaAdministrationQuery<IReadOnlyList<OrdinaryTacticBeamPlacement>>.Known(placements.AsReadOnly());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<OrdinaryTacticBeamPlacement>>.Unknown(error.Message); }
    }

    private static OrdinaryTacticViewIdentity Identity(OrdinaryTacticActorPublication value)
    {
        if (value.Actor.BaseCasterLocationId is int location)
        {
            if (location is < 1 or > 61 || value.Actor.Actor.Value != 0)
                throw new InvalidDataException("tactic.presentation.Base_actor_identity");
            return location <= 51 ? new(OrdinaryTacticViewKind.City, location)
                : new(OrdinaryTacticViewKind.Gate, location - 51);
        }
        if (value.Actor.Actor.Value < 1)
            throw new InvalidDataException("tactic.presentation.Army_actor_identity");
        return new(OrdinaryTacticViewKind.Army, value.Actor.Actor.Value);
    }
}
