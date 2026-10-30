using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2B5ED0's rank1 prefix: an explicit1..100 choice stores directly;
    //otherwise28B610 selects, then2AFBF0 validates the current definition.
    internal (GameState State, bool Valid) PrepareNormalTurnEmpireNameCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force, int kingdomNameChoice)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("current_pool_binding");
        int forceId = K(current.ReadForcePhysicalSlot(force), "Force60");
        var definitions = ReadEmpireNameResourcesCurrent().CustomNames.Definitions;
        if (kingdomNameChoice is >= 1 and <= 100) Store(kingdomNameChoice);
        else SelectNpcName();
        int actual = K(current.ReadForceKingdomNameIndex(forceId), "Force48");
        return (state, definitions[actual <= 100 ? actual : 0].NativeValidity);

        void SelectNpcName()
        {
            //This is the ordinary NPC caller. Player selection is a modal
            //command, and must not consume the NPC random/name preference path.
            if (TalentForceIsPlayerCurrent(state, current, force)) throw Missing("NPC_selection_scope");
            var list = Temporary();
            try
            {
                int imperialForce = ImperialForce();
                int scenario = (state.CampaignEventWorld ?? throw Missing("scenario_owner")).ScenarioId;
                int faction = K(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.FactionOfficerId, "Force11E");
                int selected = scenario == 19 ? faction switch { 5101 => 44, 5105 => 45, _ => 0 } : 0;
                if (selected != 0 && !Used(selected)) { Store(selected); return; }
                foreach (var country in definitions)
                {
                    bool eligible = (country.Field8E & 1) != 0;
                    if (!eligible && K(current.ReadForceNativeValidity(new(imperialForce)), "imperial_Force_validity") &&
                        country.PhysicalSlot == 7 && imperialForce == forceId) eligible = true;
                    if (country.PhysicalSlot == 47 || !eligible || !Owns(country.Field8C) || !Owns(country.Field8D) || Used(country.PhysicalSlot)) continue;
                    Lock(true);
                    try
                    {
                        var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("candidate_node_owner");
                        if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Kingdom, country.PhysicalSlot));
                    }
                    finally { Lock(false); }
                }
                var candidates = new List<int>();
                for (var node = current.ReadHexListHead(list) ?? throw Missing("candidate_head"); node.Identity.HasValue;
                     node = current.ReadHexListNodeNext(node) ?? throw Missing("candidate_next"))
                {
                    var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("candidate_payload");
                    if (payload is not { Kind: RegistryKind.Kingdom, PhysicalSlot: int country }) throw Missing("candidate_kind");
                    candidates.Add(country);
                }
                if (candidates.Count == 0) return;
                selected = 0;
                int ruler = Lookup(K(current.ReadForceRecord(forceId)?.RawRuler10, "ruler10"));
                Prefer(1, () => Lineage(521));
                Prefer(2, () => Lineage(551));
                Prefer(3, () => Lineage(952));
                Prefer(4, () => Lineage(344));
                Prefer(5, () => faction == 32 || Word(ruler, 0xB4) == 32);
                Prefer(11, () => Direct(2010));
                Prefer(7, () => scenario == 63 && K(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654") == 0 && Direct(2007));
                Prefer(36, () => Direct(2008));
                Prefer(32, () => Direct(2068));
                Prefer(46, () => Identity(ruler) is >= 2050 and <= 2055 or >= 2083 and <= 2084);
                Prefer(47, () => Direct(2062) || Identity(ruler) is 2064 or >= 2066 and <= 2067);
                Prefer(48, () => Direct(2085));
                //Matching TC immutable .data:1A4A878=20,1A4A86C=30.
                if (selected == 0 && unchecked((short)K(RulerCaptureReleaseInputs.ReadCurrentWord(state, ruler, 0x17A), "ruler17A")) >= 20 &&
                    candidates.Contains(7) && Sample(100) < 30) selected = 7;
                if (selected == 0) selected = candidates[candidates.Count > 1 ? Sample(candidates.Count) : 0];
                if (selected != 0) Store(selected);

                bool Direct(int ancestor) => faction == ancestor || Word(ruler, 0xB2) == ancestor || Word(ruler, 0xB4) == ancestor;
                bool Lineage(int ancestor)
                {
                    if (Direct(ancestor)) return true;
                    int first = Lookup(Word(ruler, 0xB2));
                    if (Word(first, 0xB2) == ancestor || Word(first, 0xB4) == ancestor) return true;
                    int second = Lookup(Word(ruler, 0xB4));
                    return Word(second, 0xB2) == ancestor || Word(second, 0xB4) == ancestor;
                }
                void Prefer(int country, Func<bool> condition)
                {
                    if (selected == 0 && !Used(country) && condition()) selected = country;
                }
            }
            finally
            {
                Lock(true);
                try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
                finally { Lock(false); }
            }
        }
        bool Owns(byte cityCode)
        {
            if (cityCode > 46) return false;
            var city = current.ReadCityTableReference(cityCode) ?? throw Missing("required_City_pointer");
            return K(current.ReadBaseNativeValidity(city), "required_City_validity") &&
                K(current.ReadBaseForceSlot(city), "required_City_Force") == forceId;
        }
        bool Used(int country) => EmpireNameUsedCurrent(current, country);
        int ImperialForce() => ReadEmpireNameImperialForceCurrent(state, current);
        void Store(int country) => state = StoreEmpireNameCurrent(state, current, forceId, country);
        int Sample(int bound) => TacticEffectSourceSamplerMath.Sample(bound, 1, forceId, 0, 0, 0,
            ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C"));
        int Lookup(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        int Identity(int physical) => K(current.ReadPersonIdentity(physical), "Person10");
        int Word(int person, int field) => OfficeWord(state, person, field);
        HexListHandle Temporary()
        {
            Lock(true);
            try { return current.CreateTemporaryHexList() ?? throw Missing("temporary_owner"); }
            finally { Lock(false); }
        }
        void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.empire-name." + field);
    }

    //27F320's rank1 tail deliberately uses241D30(status9), not2A1720's
    //ordinary death transaction.240DB0 still clears the reached associations.
    internal GameState RemoveNormalTurnRetiredEmperorCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("current_pool_binding");
        int id = K(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
        int person = K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "emperor_pointer");
        if (!K(current.ReadPersonNativeValidity(person), "emperor_validity")) return state;
        current.WritePersonStatus(person, 9);
        int identity = K(current.ReadPersonIdentity(person), "emperor10");
        var officer = state.Officers.GetValueOrDefault(identity) ?? throw Missing("emperor_owner");
        var status = OfficerServiceStatusSourceAdapter.Decode(9);
        state = state with { Officers = new(state.Officers) { [identity] = officer with
            { ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, officer.Role), Presence = OfficerPresence.Dead } } };
        var assignments = new List<PersonAssignmentPrecallResult>();
        var effects = new List<CampaignEffectRequest>();
        PersonDistrictTransfer.Apply(person, 0, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments,
            result => state = ApplyPersonAssociationRemovalReports(state, current, person, result,
                () => (ReadArmyDestructionSampler(state, current) ?? throw Missing("World450")).Day450, effects)));
        state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, person, assignments);
        current.WritePersonFirstLocation(person, 0);
        state = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(state, current, person);
        current.WritePersonSecondLocation(person, 0);
        officer = state.Officers[identity];
        state = state with { Officers = new(state.Officers) { [identity] = officer with
            { DistrictId = null, ForceId = null, LocationId = null,
              LocationState = new(OfficerPosition.Unplaced, OfficerDistrictMembership.Unaffiliated) } } };
        var native = state.NativeArmySimulation ?? throw Missing("World_owner");
        state = state with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values)
                { [CityCaptureTerritoryInputs.Emperor1654Key] = 0,
                  [OwnedArmyLifecycleDetachedInputs.Key("Person", person, "E0")] = 0 } } } };
        state = OfficerLocationSourceAdapter.PublishPosition(state, identity, current);
        var residue = current.ReadOfficerLocationResidue(identity);
        state = state with { ResourceState = CampaignSourceKnowledge.From(state.ResourceState)
            .With(new(identity, residue?.LocationCode, residue?.DistrictCode)) };
        CityCaptureTerritoryInputs.BindCurrentEmperor(state, current);
        return effects.Count == 0 ? state : state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw Missing("effect_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeClock?.UpdateOrdinal ?? throw Missing("effect_revision"), effects) };

        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => new("normal-turn.retired-emperor." + field);
    }
}
