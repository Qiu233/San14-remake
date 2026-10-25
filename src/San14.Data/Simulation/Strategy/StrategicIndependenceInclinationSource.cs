using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC27A0E0. The list branches inspect live Force membership in source
    //order; only the reached final branch reads the pure World sampler.
    internal bool ReadStrategicIndependenceInclinationCurrent(GameState state,
        SourceWorldContext current, int person, int rebel, ForceReference newForce)
    {
        var oldForce = current.ReadPersonForce(person) ?? throw StrategyMissing("inclination_old_Force");
        int oldRuler = Person(StrategyNeed(current.ReadForceRecord(Ordinal(oldForce))?.RawRuler10, "inclination_old_ruler"));
        var date = state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("inclination_date");
        var personHex = PersonReturnCommands.ResolvePersonPosition(state, current, person, date);
        var rebelHex = PersonReturnCommands.ResolvePersonPosition(state, current, rebel, date);
        if (Sworn(person, oldRuler) || Spouse(person, oldRuler) || Parent(person, oldRuler, 0xB4) ||
            Parent(person, oldRuler, 0xB6) || Parent(oldRuler, person, 0xB4) || Parent(oldRuler, person, 0xB6)) return false;
        if (Sworn(person, rebel) || Spouse(person, rebel)) return true;
        if (Status(rebel) > Status(person)) return false;
        if (Dislikes(person, oldRuler)) return true;
        int newRuler = Person(StrategyNeed(current.ReadForceRecord(Ordinal(newForce))?.RawRuler10, "inclination_new_ruler"));
        if (Dislikes(person, newRuler)) return Draw();
        foreach (int other in People(oldForce)) if (Sworn(person, other) || Spouse(person, other)) return false;
        foreach (int other in People(newForce)) if (Sworn(person, other) || Spouse(person, other)) return true;
        if (Likes(person, oldRuler)) return false;
        if (Likes(person, newRuler)) return true;
        if (Likes(person, rebel)) return Draw();
        if (Dislikes(person, rebel)) return false;
        int loyalty = StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "inclination_loyalty");
        //TC.data1A4AAC0=50,1A4AACC=80,1A4AAE4=4.
        if (loyalty < Rule("low-loyalty", 50)) return true;
        if (unchecked(StrategyNeed(current.ReadAllegianceDisposition(person), "inclination_disposition16C") /
                Rule("ordinary-disposition-divisor", 4) + loyalty) > Rule("ordinary-threshold", 80)) return false;
        if (personHex.PhysicalSlot is >= 0 and < 48400 && rebelHex.PhysicalSlot is >= 0 and < 48400)
        {
            var firstArea = Area(personHex); var secondArea = Area(rebelHex);
            if (ValidArea(firstArea) && ValidArea(secondArea))
            {
                var firstCity = City(firstArea); var secondCity = City(secondArea);
                if (ValidCity(firstCity) && ValidCity(secondCity) && Province(firstCity) != Province(secondCity) && !Adjacent(firstCity, secondCity))
                    return false;
            }
        }
        return Draw();

        bool Draw()
        {
            //1A4AAD8=5,1A4AAF0=15,1A4AAFC=5, all file-backed TC rules.
            int loyalty = StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, _governorInputs), "inclination_draw_loyalty");
            int disposition = StrategyNeed(current.ReadAllegianceDisposition(person), "inclination_draw_disposition");
            int currentNewRuler = Person(StrategyNeed(current.ReadForceRecord(Ordinal(newForce))?.RawRuler10, "inclination_draw_new_ruler"));
            var actualOldForce = current.ReadPersonForce(person) ?? throw StrategyMissing("inclination_draw_old_Force");
            int actualOldRuler = Person(StrategyNeed(current.ReadForceRecord(Ordinal(actualOldForce))?.RawRuler10, "inclination_draw_old_ruler"));
            int difference = unchecked(Affinity(person, currentNewRuler) - Affinity(person, actualOldRuler));
            int extra = Math.Clamp(difference, 0, Rule("affinity-cap", 15));
            int same = Doctrine(person) == Doctrine(actualOldRuler) ? Rule("same-doctrine", 5) : 0;
            int threshold = Math.Clamp(unchecked(same + disposition / Rule("draw-disposition-divisor", 5) + extra + loyalty), 0, 100);
            var now = state.NativeMapSeason?.ThirdDate ?? throw StrategyMissing("inclination_draw_date");
            var sampler = ReadArmyDestructionSampler(state, current) ?? throw StrategyMissing("inclination_sampler450_45C");
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, unchecked(now.Year + now.Month + now.Day),
                StrategyNeed(current.ReadPersonIdentity(person), "inclination_draw_identity"), 0, 0, sampler);
            return sample >= threshold; //3FCA30(sample,threshold)==0.
        }
        int Rule(string name, int literal) => StrategicParameter(state, "independence-inclination." + name, literal);
        int Person(int raw) => StrategyNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "inclination_Person_lookup");
        int Ordinal(ForceReference force) => StrategyNeed(current.ReadForcePhysicalSlot(force), "inclination_Force_ordinal");
        byte Status(int value) => StrategyNeed(current.ReadPersonStatus(value), "inclination_Person_status");
        bool ValidPerson(int value) => StrategyNeed(current.ReadPersonNativeValidity(value), "inclination_Person_validity");
        bool Sworn(int a, int b) => StrategicIndependenceSwornCurrent(state,current,a,b);
        bool Spouse(int a, int b) => StrategicIndependenceSpouseCurrent(state,current,a,b);
        bool Parent(int a, int b, int field) => ValidPerson(a) && ValidPerson(b) &&
            StrategyNeed(current.ReadPersonIdentity(a), "inclination_parent_first") != StrategyNeed(current.ReadPersonIdentity(b), "inclination_parent_other") &&
            OfficeWord(state,a,field) is var parent && parent != 0 && parent == StrategyNeed(current.ReadPersonIdentity(b), "inclination_parent_target");
        bool Likes(int a,int b) => OfficeRelationList(state,current,a,b,0xC0);
        bool Dislikes(int a,int b) => OfficeRelationList(state,current,a,b,0xD0);
        int Affinity(int a,int b)
        {
            if (!ValidPerson(a) || !ValidPerson(b)) return 0;
            int difference = Math.Abs(StrategyNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(state,a), "inclination_affinity_first") -
                StrategyNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(state,b), "inclination_affinity_other"));
            return Math.Min(75, Math.Min(difference,150-difference));
        }
        int Doctrine(int value)
        {
            int id = StrategyNeed(current.ReadPersonIdentity(value), "inclination_doctrine_identity");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw StrategyMissing("inclination_doctrine_owner");
            return owner.DoctrineId ?? owner.AbilityState?.ObservedDoctrineId ?? throw StrategyMissing("inclination_doctrine14C");
        }
        IEnumerable<int> People(ForceReference force)
        {
            int target = Ordinal(force);
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("inclination_Person_registry"))
            {
                int value = StrategyNeed(entry, "inclination_Person_payload");
                if (target is >= 1 and <= 51 && StrategyNeed(current.ReadPersonOldForceSlot(value), "inclination_Person_Force") == target) yield return value;
            }
        }
        AreaReference Area(HexReference hex)
        {
            int raw = StrategyNeed(current.ReadHexAreaSlot(hex), "inclination_Hex_Area");
            return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw StrategyMissing("inclination_Area_pointer");
        }
        BaseReference City(AreaReference area)
        {
            int raw = StrategyNeed(current.ReadAreaCitySlot(area), "inclination_Area_City");
            return current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw StrategyMissing("inclination_City_pointer");
        }
        bool ValidArea(AreaReference area) => StrategyNeed(current.ReadAreaNativeValidity(area), "inclination_Area_validity");
        bool ValidCity(BaseReference city) => StrategyNeed(current.ReadBaseNativeValidity(city), "inclination_City_validity");
        int Province(BaseReference city) => StrategyNeed(_growthQueries.ReadCityState(state,
            StrategyNeed(city.PhysicalSlot, "inclination_City_slot")), "inclination_City_province");
        bool Adjacent(BaseReference first, BaseReference second)
        {
            var temporary = current.CreateTemporaryHexList() ?? throw StrategyMissing("inclination_neighbor_temporary");
            try
            {
                (_eventCityNeighborhoods ??= new(_scenario)).FillCities22F430(current,first,temporary);
                var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw StrategyMissing("inclination_neighbor_head");
                while (node.Identity.HasValue)
                {
                    var candidate = current.ReadRegistryNodePayload(node) ?? throw StrategyMissing("inclination_neighbor_payload");
                    if (candidate.Kind != RegistryKind.City) throw StrategyMissing("inclination_neighbor_kind");
                    if (candidate.PhysicalSlot == second.PhysicalSlot) return true;
                    node = current.ReadRegistryNodeNext(node) ?? throw StrategyMissing("inclination_neighbor_next");
                }
                return false;
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
    }
}
