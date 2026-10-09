namespace San14.Core;

/// <summary>Complete pointer-table order and current receiver header. Pending
/// image is retained independently after its active gameplay projection leaves
/// Units; this is not authority that original header cleanup already ran.</summary>
public sealed record NativeArmyLifecycleSlot(int PhysicalSlot, ushort? Commander12, byte? Status10,
    ushort? Priority14, int? ActiveUnitId, UnitState? PendingImage, ArmyStoreKnowledge StoredResources,
    ushort[]? AuxiliaryAC, ushort[]? AuxiliaryF4)
{
    /// <summary>Free receiver reset/current source writes. Active/Pending
    /// images keep their own mutable raw fields; a missing reset is unknown.</summary>
    public NativeArmyOverrideTargetRuntime? OverrideTarget { get; init; }
    //Free receiver current value; active/Pending images own their direction.
    public byte? Direction36 { get; init; }
    public byte? MapUiByte13C { get; init; }
    public byte? CommandControl140 { get; init; }
    public byte? RouteHold56 { get; init; }
    public ushort? RouteGoal1F8 { get; init; }
    /// <summary>Whether this reusable receiver completed its group detachment.
    /// Unknown legacy readiness does not authorize another assignment.</summary>
    public bool? GroupAssignmentReady { get; init; }
    /// <summary>Source presentation link to an independently owned map binding.</summary>
    public int? OccupancyBindingId { get; init; }
}
public sealed partial record NativeArmyLifecycleState(string Revision, string Language, string RuleVersion,
    string SourceScenarioUri, string SourceScenarioSha256, WorldIndexesState Indexes,
    NativeArmyLifecycleSlot[] Slots)
{
    public ArmyGroupsState? ArmyGroups { get; init; }
    public ArmyCautionsState? ArmyCautions { get; init; }
    /// <summary>Detaches the mutable receiver images and lifecycle collections.</summary>
    public NativeArmyLifecycleState Copy() => GameStateCopy.Copy(this)!;
}
public sealed partial record GameState
{
    public NativeArmyLifecycleState? NativeArmyLifecycle { get; init; }
}

public static class NativeArmyLifecycleStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeArmyLifecycle is not { } lifecycle) return;
        if (string.IsNullOrWhiteSpace(lifecycle.Revision) || lifecycle.Language is not ("SC" or "TC") ||
            string.IsNullOrWhiteSpace(lifecycle.RuleVersion) || !Hash(lifecycle.SourceScenarioSha256) ||
            string.IsNullOrWhiteSpace(lifecycle.SourceScenarioUri) ||
            state.ContentHashes.GetValueOrDefault(lifecycle.SourceScenarioUri) != lifecycle.SourceScenarioSha256 ||
            state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != lifecycle.Language ||
            parameters.RuleVersion != lifecycle.RuleVersion || lifecycle.Slots is not { Length: > 0 } ||
            lifecycle.Indexes is null)
            throw new InvalidDataException("Native Army lifecycle has missing or contradictory source ownership.");
        WorldIndexesSaveValidation.Validate(lifecycle.Indexes);
        ArmyGroupsSaveValidation.Validate(lifecycle.ArmyGroups);
        ArmyCautionsSaveValidation.Validate(lifecycle.ArmyCautions);
        NativeArmyDestructorFlagsStateValidation.Validate(lifecycle);
        NativeArmyLifecycleTargetStateValidation.Validate(lifecycle);
        var units = new HashSet<int>();
        for (int i = 0; i < lifecycle.Slots.Length; i++)
        {
            var slot = lifecycle.Slots[i];
            if (slot is null || slot.StoredResources is null || slot.PhysicalSlot != i || slot.ActiveUnitId is < 0 ||
                slot.AuxiliaryAC is { Length: not 36 } || slot.AuxiliaryF4 is { Length: not 36 } ||
                slot.ActiveUnitId is not null && slot.PendingImage is not null)
                throw new InvalidDataException("Native Army pointer-table/current slot image is incomplete or malformed.");
            if (slot.ActiveUnitId is int id)
            {
                if (!units.Add(id) || !state.Units.TryGetValue(id, out var army) || army.NativeArmyOrigin?.NativeSlotIndex != i ||
                    slot.Commander12 != army.OfficerId || slot.Status10 != army.NativeTacticRuntime?.Status10)
                    throw new InvalidDataException("Native Army active slot does not identify its current owned unit/header.");
            }
            if (slot.PendingImage is { } pending &&
                (state.Units.ContainsKey(pending.Id) || !units.Add(pending.Id) || pending.NativeArmyOrigin?.NativeSlotIndex != i ||
                    slot.Commander12 != pending.OfficerId || slot.Status10 != pending.NativeTacticRuntime?.Status10))
                throw new InvalidDataException("Native Army pending image aliases an active projection or a different source slot.");
            if (slot.PendingImage is { } image) ValidatePendingImage(state,image);
        }
        foreach (var army in state.Units.Values)
            if (army.NativeArmyOrigin is not null && !units.Contains(army.Id))
                throw new InvalidDataException("Source-created unit has no actual complete-pool lifecycle owner.");
    }
    private static void ValidatePendingImage(GameState state,UnitState image)
    {
        //A pending receiver may have zero ready soldiers and its commander is
        //already home/dead. Do not impose active Deployed/positive-troop gates.
        //The image still needs valid value domains and saved module ownership.
        long size=(long)state.MapWidth*state.MapHeight;
        if (image.Id<0 || !state.Forces.ContainsKey(image.ForceId) || !state.Officers.ContainsKey(image.OfficerId) ||
            image.HexId<0 || image.HexId>=size || image.DestinationHexId<0 || image.DestinationHexId>=size ||
            image.Troops<0 || image.Supplies<0 ||
            image.SourceLocationId is int source && !state.Cities.ContainsKey(source) && !state.Gates.ContainsKey(source) ||
            image.FormationId is int formation && !state.Formations.ContainsKey(formation) ||
            image.TacticIds is { } tactics && (tactics.Distinct().Count()!=tactics.Length || tactics.Any(t=>!state.Tactics.ContainsKey(t))) ||
            image.Behavior is { } behavior && !Enum.IsDefined(behavior.AfterAction) ||
            image.Mission is { } mission && (!Enum.IsDefined(mission.Kind) || !Enum.IsDefined(mission.Arrival) || mission.CargoGold<0))
            throw new InvalidDataException("Native pending Army image has invalid quantities, locations or typed references.");
        //Validation-only view: reuse existing source/module validators on this
        //one stored image. It is never published or treated as an active world.
        var view=state with { Units=new() { [image.Id]=image } };
        NativeSubdayStateValidation.Validate(view);
        NativeArmyMarchStateValidation.Validate(view);
        PlayableMilitaryStateValidation.Validate(view);
        PlayableTacticStateValidation.Validate(view);
    }
    private static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
