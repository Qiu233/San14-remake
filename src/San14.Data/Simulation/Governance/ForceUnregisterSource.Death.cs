using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record ForceUnregisterDeathResult(GameState State, int NativeReturn);

public sealed partial class ForceUnregisterSource
{
    //295EE0 calls293BD0(Force,null,0,0), while its Person/relationship
    //temporaries remain live. This is not the old empty-retirement profile.
    internal ForceUnregisterDeathResult ApplyDeathCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, ForceReference victim, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw Missing("death_current_pool");
        int id = Need(current.ReadForcePhysicalSlot(victim), "death_Force60");
        bool selected = package.ReadApplicationInformationRestriction(state)
            ?? throw Missing("death_selected_user_service");
        if (selected && package.TalentForceIsPlayerCurrent(state, current, victim))
            throw Missing("death_selected_user_defeated_Force44C_title448_owner");
        //293BD0's notification precedes27A6B0, including the terminal path.
        if (!selected)
        {
            effects.Add(new(null, null) { ForceCoalitionVisual =
                new(ForceCoalitionVisualOperation.NarrationEnter, null) { SceneKind = 24 } });
            bool returning = package.ReadApplicationReturnToTitlePending() ?? throw Missing("death_return_to_title");
            if (!returning) effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
            var text = package.FormatNormalTurnReport(state, current, 0x3201, new(Force0: id));
            state = text.State;
            effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        }
        var terminal = ApplyDeathTerminalCurrent(state, current, pool, id, date, effects);
        state = terminal.State;
        if (terminal.NativeReturn != 0)
        {
            //32BD90 ->32BDD0: selected-user mode returns without these calls.
            //The attached normal campaign has a real application state, so
            //preserve its source writes as ordered host work, never fake ACK.
            if (!selected)
            {
                long cycle = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("death_exit_cycle");
                effects.Add(new(null, null) { ApplicationExit = new(ApplicationExitOperation.ReturnToTitle) });
                effects.Add(new(null, null) { MapMusicIntent = new(cycle, 1, false, 1000, 1000) });
                effects.Add(new(null, null) { ApplicationExit = new(ApplicationExitOperation.ResetAudioSpecialMode) });
            }
            return new(state, terminal.NativeReturn);
        }
        var retired = ApplyCore(state, id, 0, null, current, pool,
            captureDate: date, captureEffects: effects, death: true);
        return new(retired.State ?? throw Missing(retired.UnresolvedOwner ?? "death_293BD0"), 0);
    }

    private GameState RecallDeathSpecialPersonnelCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int victim, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
        => package.RecallForceDeathSpecialPersonnelCurrent(state, current, pool, victim, date, effects);

    private GameState ClearDeathTargetAssociationsCurrent(GameState state, SourceWorldContext current,
        int victim, List<CampaignEffectRequest> effects)
    {
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Force) ?? throw Missing("death_association_C0");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("death_association_head");
        var target = current.ReadForceTableReference(victim) ?? throw Missing("death_association_target");
        while (node.Identity.HasValue)
        {
            var force = current.ReadRegistryNodePayload(node) ?? throw Missing("death_association_payload");
            if (force is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw Missing("death_association_Force");
            //This ordinary death caller does not construct CEventPlaybackState.
            state = package.CancelForceAssociationTargetCurrent(state, current, new(physical), target,
                0x344D, writeHistory: true, eventPlaybackPresent: false, effects);
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("death_association_next");
        }
        return state;
    }

    private GameState WriteDeathForceChronicleCurrent(GameState state, SourceWorldContext current, int victim)
    {
        if ((state.CampaignChronicle?.RecordingSuppression ?? throw Missing("death_chronicle_control")) != 0) return state;
        var force = current.ReadForceTableReference(victim) ?? throw Missing("death_chronicle_Force");
        int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("death_chronicle_Day450")).Day450;
        string name = package.HistoryWriter.CustomFirstName(state, current, force, FullName);
        string label = package.HistoryWriter.FallbackName(state, current, force, FullName);
        int color = Need(current.ReadForceReportColor(force), "death_chronicle_color");
        var command = new CampaignChronicleCommand(3, 1, day, name, color, 1,
            ImmutableArray.Create(label, string.Empty, string.Empty),
            ImmutableArray.Create(48400, 48400, 48400, 48400));
        return new CampaignChronicleWriter(package.HistoryWriter).Write(state, current, command,
            package.ReadApplicationGameplay()?.UserSelectionServicePresent);
        static string FullName(OfficerState person, bool full)
        {
            string first = person.CurrentSurname ?? throw Missing("death_chronicle_surname");
            string last = person.CurrentGivenName ?? throw Missing("death_chronicle_given_name");
            return full ? first + last : first;
        }
    }

    private static int[] DeathDistrictOrder(SourceWorldContext current, HexListHandle list)
    {
        var result = new List<int>();
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("death_District_head");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node) ?? throw Missing("death_District_payload");
            if (value is not { Kind: RegistryKind.District, PhysicalSlot: int physical }) throw Missing("death_District_pointer");
            result.Add(physical);
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("death_District_next");
        }
        return result.ToArray();
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //293BD0's City/Province loop is between the Army iterator and278090.
    //It uses each actual eligible Province and independently allocated list;
    //it does not borrow a previously captured City's winning-Force condition.
    internal GameState RecallForceDeathSpecialPersonnelCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int victim, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        for (int city = 0; city < 52; city++)
        {
            var pointer = current.ReadCityTableReference(city) ?? throw Missing("City_pointer");
            if (K(current.ReadBaseForceSlot(pointer), "City_Force") != victim) continue;
            int raw = K(_growthQueries.ReadCityState(state, city), "CityCE");
            var province = current.ReadStaticTableReference(0xDC80, raw < 23 ? raw : 0) ?? throw Missing("Province_pointer");
            int physical = K(province.PhysicalSlot, "Province_identity");
            var qualified = PersonReturnPathSource.ReadQualifiedAdvantageForProvinceCurrent(state, current, new(victim), physical);
            if (qualified.UnresolvedOwner is { } unresolved) throw Missing(unresolved);
            if (qualified.Value is not int advantage) continue;
            for (int index = 0; index < 2; index++)
            {
                if (K(_growthQueries.ReadAdvantageEffect(state, advantage, index), "Advantage_effect") != 1) continue;
                string key = PersonGrowthCurrentQueries.MagnitudeKey(advantage, index);
                var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("Advantage_parameter_owner");
                if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int special) || (uint)special > 255)
                    throw Missing("Advantage_magnitude");
                var force = current.ReadForceTableReference(special < 52 ? special : 0) ?? throw Missing("special_Force_pointer");
                if (!K(current.ReadForceNativeValidity(force), "special_Force_validity") ||
                    K(current.ReadConditionForceDependence(new(K(force.PhysicalSlot, "special_Force_identity"))), "special_Force194") != 0) continue;
                int family = K(current.ReadForcePhysicalSlot(force), "special_Force60");
                var list = current.CreateTemporaryHexList() ?? throw Missing("Person_temporary");
                try
                {
                    if (family is >= 46 and <= 50)
                        for (int lookup = 4101; lookup <= 4200; lookup++)
                        {
                            //2B0290 filters the lookup suffix before testing the
                            //actual pointed-to Person identity; preserve aliases.
                            if ((lookup - 4101) % 20 < 10) continue;
                            int person = K(current.ReadPersonLookupReference(lookup)?.PhysicalSlot, "Person_lookup");
                            if (!K(current.ReadPersonNativeValidity(person), "Person_validity") ||
                                K(current.ReadPersonStatus(person), "PersonE6") is < 1 or > 4) continue;
                            ushort identity = K(current.ReadPersonIdentity(person), "Person10");
                            int actualFamily = identity is >= 4001 and <= 4030 ? 46 + (identity - 4001) / 6 :
                                identity is >= 4101 and <= 4200 ? 46 + (identity - 4101) / 20 : 0;
                            if (actualFamily == family) current.AppendPersonReturnTemporary(list, person);
                        }
                    //TC294979..4986 explicitly passes0,1,0.
                    state = ApplySpecialPersonnelRecallCurrent(state, current, pool, list, false, true, false, date, effects);
                }
                finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            }
        }
        return state;
        static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("force-death.special-personnel." + field);
    }
}
