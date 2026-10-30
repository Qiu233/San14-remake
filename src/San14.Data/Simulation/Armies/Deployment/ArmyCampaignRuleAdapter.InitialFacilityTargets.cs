using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static CommandResult? OCReadInitialFacilityTarget(GameState state, SourceWorldContext current,
        int commanderPhysical, DeploymentAction action, out ArmyCommandTarget? result)
    {
        result = null;
        if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } ||
            !FacilityConstructionIntent.IsDefinitionKey(action.StructureKindId))
            return CommandResult.Reject("The selected construction site or definition is invalid.", "deployment.target");
        //The selected resource key carries7EC4B0's actual physical definition
        //identity, not the visible directory lookup ordinal. The later
        //receiver selection reader still performs its actual source lookup.
        int physical = MapObjectSourceDirectory.DefinitionSlot(action.StructureKindId!);
        var reference = new ObjectDefinitionReference(physical);
        if (!OCKnown(current.ReadObjectDefinitionValidity(reference), "initial_facility_definition_validity"))
            return CommandResult.Reject("The selected facility definition is unavailable.", "deployment.target");
        var definition = Read(current, reference);
        if (!IsSupported(physical))
            throw OCMissing("initial_facility_definition_consumer");
        if (definition.Category is not (2 or 3 or 4))
            return CommandResult.Reject("The selected definition is not a construction facility.", "deployment.target");
        string? rejected = null;
        var site = new HexReference(action.Target.Id); //actual physical map input
        if (!FacilityConstructionLegality.CheckCommander(state, current, commanderPhysical,
            site, reference, reason => rejected = reason))
            return CommandResult.Reject("This commander cannot construct at the selected site.",
                rejected ?? "deployment.target");
        result = new(8, new(9, checked((ushort)action.Target.Id)), action.Target.Id);
        return null;
    }

    private static CommandResult? OCValidateInitialFacilitySelection(GameState state,SourceWorldContext current,
        int commanderPhysical,FacilityConstructionIntent? selection,bool validateConstruction)
    {
        if (selection is null) return null;
        if (selection.DefinitionKey is not null && !FacilityConstructionIntent.IsDefinitionKey(selection.DefinitionKey) ||
            !ValidSite(selection.InitialSite) || !ValidSite(selection.ContinuationSite))
            return CommandResult.Reject("The retained construction selection is invalid.", "deployment.construction_selection");
        if(validateConstruction&&selection.DefinitionKey is not null &&
            selection.InitialSite is{Kind:ArmyMarchDestinationKind.Hex,HexId:int first} &&
            selection.ContinuationSite is{Kind:ArmyMarchDestinationKind.Hex,HexId:int last})
        {
            var reference=current.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(selection.DefinitionKey))
                ??throw OCMissing("initial_continuous_definition");
            if(!ContinuousConstructionAllows(state,current,first,last,reference,
                site=>FacilityConstructionLegality.CheckCommander(state,current,commanderPhysical,site,reference)))
                return CommandResult.Reject("The continuous construction route contains an unavailable site.","deployment.construction_selection");
        }
        return null;
        static bool ValidSite(ArmyMarchDestination? site) => site is null ||
            site is { Kind: ArmyMarchDestinationKind.None, HexId: null } ||
            site is { Kind: ArmyMarchDestinationKind.Hex, HexId: >= 0 and < 48400 };
    }

    private static GameState OCBindInitialFacilitySelection(GameState state, int unitId, DeploymentUnitRequest request)
    {
        var action = request.Actions[0];
        var intent = action.ActionKey == "build"
            ? new FacilityConstructionIntent(action.StructureKindId,
                ArmyMarchDestination.AtHex(action.Target.Id),
                request.ConstructionSelection?.ContinuationSite ??
                    (MapObjectSourceDirectory.DefinitionSlot(action.StructureKindId!) is 9 or 10 or 16
                        ? ArmyMarchDestination.AtHex(action.Target.Id) : ArmyMarchDestination.None))
            : request.ConstructionSelection;
        if (intent is null) return state;
        //Only the real newly admitted receiver owns the selected definition
        //and sites. Mission/MilitaryPlan are presentation, not fallback
        //sources. The existing stage15 writer charges the current Home Base
        //when construction actually starts; initial deployment does not pay.
        var arena = state.ReadArmyLifecycle() ?? throw OCMissing("initial_facility_lifecycle");
        var receiver = arena.Receivers.FindUnit(unitId) ?? throw OCMissing("initial_facility_receiver");

        return state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with
            { FacilityConstruction = intent }) });
    }
}
