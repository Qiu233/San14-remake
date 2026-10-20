namespace San14.Core;

public static class ArmyLifecycleSaveValidation
{
    public static void Validate(GameState state)
    {
        if (state.ArmyLifecycle is not { } lifecycle) return;
        WorldIndexesSaveValidation.Validate(lifecycle.Indexes);
        ArmyGroupsSaveValidation.Validate(lifecycle.ArmyGroups);
        ArmyCautionsSaveValidation.Validate(lifecycle.ArmyCautions);
        var units = new HashSet<int>();
        var receivers = new HashSet<ArmyReceiverId>();
        var knownReceivers = lifecycle.Receivers.Entries.Select(row => row.ReceiverId).ToHashSet();
        foreach (var entry in lifecycle.Receivers.Entries)
        {
            if (entry.FacilityConstruction is { } construction &&
                (construction.DefinitionKey is not null && !FacilityConstructionIntent.IsDefinitionKey(construction.DefinitionKey) ||
                 !ValidConstructionSite(construction.InitialSite) || !ValidConstructionSite(construction.ContinuationSite)))
                throw new InvalidDataException("Invalid current Army facility construction selection.");
            if (entry.SupplyInterdiction is { } intent &&
                (intent.IsActive ? intent.TargetReceiverId is not { } target || !knownReceivers.Contains(target)
                    : intent.TargetReceiverId is not null))
                throw new InvalidDataException("Invalid Army supply interdiction receiver objective.");
            ArmySavedCommandKnowledge.Validate(entry.SavedCommand, (long)state.MapWidth * state.MapHeight, knownReceivers);
            if (entry.FalseReportDestination is { } destination &&
                !(destination is { Kind: ArmyMarchDestinationKind.None, HexId: null } ||
                  destination is { Kind: ArmyMarchDestinationKind.Hex, HexId: int hex } && hex >= 0 && hex < (long)state.MapWidth * state.MapHeight))
                throw new InvalidDataException("Invalid Army false-report destination.");
            var projectedImage = entry.ActiveUnitId is int activeId ? state.Units.GetValueOrDefault(activeId) : entry.PendingImage;
            if (entry.SavedCommand is { } saved && projectedImage is { } image && image.NativeCommandControl140 != saved.Control)
                throw new InvalidDataException("Army command-control projection contradicts its receiver owner.");
            if (entry.ReceiverId.Value <= 0 || !receivers.Add(entry.ReceiverId) || !Enum.IsDefined(entry.Phase) || entry.StoredResources is null ||
                entry.ActiveUnitId is < 0 || entry.ActiveUnitId is not null && entry.PendingImage is not null ||
                (entry.Phase == ArmyReceiverPhase.Active) != entry.ActiveUnitId.HasValue ||
                (entry.Phase == ArmyReceiverPhase.Pending) != (entry.PendingImage is not null))
                throw new InvalidDataException("Army receiver ownership is invalid.");
            if (entry.ActiveUnitId is int id && (!units.Add(id) || !state.Units.ContainsKey(id)))
                throw new InvalidDataException("Army receiver does not identify an active unit.");
            if (entry.PendingImage is { } pending)
            {
                if (state.Units.ContainsKey(pending.Id) || !units.Add(pending.Id))
                    throw new InvalidDataException("Pending army image aliases an active or another pending unit.");
                ValidatePendingImage(state, pending);
            }
        }
        foreach (var army in state.Units.Values)
            if (army.NativeArmyOrigin is not null && !units.Contains(army.Id))
                throw new InvalidDataException("Source-created unit has no campaign receiver owner.");
    }
    private static bool ValidConstructionSite(ArmyMarchDestination? site) => site is null ||
        site is { Kind: ArmyMarchDestinationKind.None, HexId: null } ||
        site is { Kind: ArmyMarchDestinationKind.Hex, HexId: >= 0 and < 48400 };
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
            !ArmyEquippedTactics.Valid(state, image) ||
            image.Behavior is { } behavior && !Enum.IsDefined(behavior.AfterAction) ||
            image.Mission is { } mission && (!Enum.IsDefined(mission.Kind) || !Enum.IsDefined(mission.Arrival) || mission.CargoGold<0))
            throw new InvalidDataException("Native pending Army image has invalid quantities, locations or typed references.");
        //Validation-only view: reuse existing source/module validators on this
        //one stored image. It is never published or treated as an active world.
        var view=state with { Units=new() { [image.Id]=image } };
        NativeSubdayStateValidation.Validate(view);
        PlayableMilitaryStateValidation.Validate(view);
        PlayableTacticStateValidation.Validate(view);
    }
}
