using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private IReadOnlyList<StrategicCommandCandidate> ReadStrategicExecutorsCurrent(GameState state,
        SourceWorldContext current, int forceId, int districtId)
    {
        var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("executor_Person100");
        var result = new List<int>();
        foreach (int? entry in people)
        {
            int person = StrategyNeed(entry,"executor_Person_pointer");
            if (districtId is < 1 or > 51 || StrategyNeed(current.ReadPersonDistrictSlot(person),"executor_E0") != districtId ||
                StrategyNeed(current.ReadPersonStatus(person),"executor_E6") is < 1 or > 4) continue;
            if (StrategyNeed(OfficerHealthSourceAdapter.ReadByte(current,person),"executor_health161") >= 2 ||
                (StrategyNeed(current.ReadArmyReturnPersonFlags196(person),"executor_flags15E") & 1) != 0 ||
                PersonCurrentBusySource.Read(current,person,StrategyMissing)) continue;
            var force = current.ReadPersonForce(person) ?? throw StrategyMissing("executor_Force_pointer");
            int owner = StrategyNeed(force.PhysicalSlot,"executor_Force_ordinal");
            if (owner is >= 46 and <= 50) continue;
            int identity = StrategyNeed(current.ReadPersonIdentity(person),"executor_identity");
            int home = StrategyNeed(current.ReadPersonFirstLocation(person),"executor_E2");
            if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(identity) && home is >= 1 and <= 61 && owner is not (>= 46 and <= 50)) continue;
            result.Add(person);
        }
        var abilities = SearchAbilities(current);
        int Intelligence(int person) => StrategyNeed(abilities.ResolveDimension(state,
            StrategyNeed(current.ReadPersonIdentity(person),"executor_sort_identity"),2,
            (force,effect) => _baseCapacityInputs.ReadPanelModifier(state,force,effect)),"executor_sort_Intelligence");
        //796550 supplies recommendation and journey keys after a target is selected.
        //The initial list has neither target-dependent key, so6B1D20 reaches Intelligence/identity.
        StrategySort(result,(left,right) => Intelligence(left) != Intelligence(right)
            ? Intelligence(left) > Intelligence(right) : current.ReadPersonIdentity(left) < current.ReadPersonIdentity(right));
        return result.Select(person => StrategicPersonCard(state,current,person)).ToArray();
    }

    private IReadOnlyList<StrategicCommandCandidate> ReadStrategicTargetsCurrent(GameState state,
        SourceWorldContext current, int forceId, int kind)
    {
        if (kind == 10) return ReadStrategicForceTargetsCurrent(state,current,forceId,null);
        if (kind is 12 or 24) return ReadStrategicPersonTargetsCurrent(state,current,forceId,kind)
            .Select(person => StrategicPersonCard(state,current,person)).ToArray();
        return new StrategicTerritoryTargets(this,state,current,forceId).Read(kind);
    }

    internal bool StrategicForcesBorderCurrent(GameState state,SourceWorldContext current,int first,int second)
        => new StrategicTerritoryTargets(this,state,current,first).Borders(first,second);

    private IReadOnlyList<StrategicCommandCandidate> ReadStrategicForceTargetsCurrent(GameState state,
        SourceWorldContext current, int forceId, int? excluded)
        => new StrategicTerritoryTargets(this,state,current,forceId).ReadForces(excluded);

    private static StrategicCommandCandidate StrategicPersonCard(GameState state, SourceWorldContext current, int physical)
    {
        int identity = StrategyNeed(current.ReadPersonIdentity(physical),"card_Person_identity");
        var person = state.Officers.GetValueOrDefault(identity) ?? throw StrategyMissing("card_Person_owner");
        int home = StrategyNeed(current.ReadPersonFirstLocation(physical),"card_Person_E2");
        var location = BaseLookup.Find(home,current);
        int? hex = StrategyNeed(current.ReadBaseNativeValidity(location),"card_Person_Base_validity")
            ? current.ReadBaseCurrentHexIndex(location) : null;
        return new(identity,person.Name,person.ForceId,home,hex) { PhysicalSlot=physical,Enabled=true };
    }

    internal BaseReference ReadStrategicEmperorCityCurrent(GameState state, SourceWorldContext current)
    {
        int emperor = StrategyNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state),"Emperor1654");
        var reference = current.ReadPersonLookupReference(emperor <= 6000 ? emperor : 0) ?? throw StrategyMissing("Emperor_pointer");
        int physical = StrategyNeed(reference.PhysicalSlot,"Emperor_physical");
        int city = 0;
        if (StrategyNeed(current.ReadPersonNativeValidity(physical),"Emperor_validity"))
        {
            var position = PersonPositionRepair.DecodeSecondLocation(StrategyNeed(current.ReadPersonSecondLocation(physical),"Emperor_E4"));
            if (position.Kind == 5) city = position.Index < 52 ? position.Index : 0;
        }
        return current.ReadCityTableReference(city) ?? throw StrategyMissing("Emperor_City_pointer");
    }

    private static void StrategySort<T>(List<T> list, Func<T,T,bool> before)
    {
        for (int index=1;index<list.Count;index++)
        {
            var selected=list[index]; int previous=index-1;
            while (previous>=0 && before(selected,list[previous])) { list[previous+1]=list[previous];previous--; }
            list[previous+1]=selected;
        }
    }

    private sealed class StrategicTerritoryTargets(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, int own)
    {
        private readonly Dictionary<int,int[]> neighbors=[];
        private readonly Dictionary<int,int> cityCounts=[];
        private PersonFreedomHomeSource? neighborhood;
        private BaseReference City(int index) => current.ReadCityTableReference(index < 52 ? index : 0)
            ?? throw StrategyMissing("target_City_pointer");
        private int CityOwner(BaseReference city) => StrategyNeed(current.ReadBaseForceSlot(city),"target_City_Force");
        private int AreaOwner(int area) => StrategyNeed(current.ReadAreaCurrentForceSlot(new(area)),"target_Area_Force");
        private int AssignedCity(int area) => StrategyNeed(current.ReadAreaCitySlot(new(area)),"target_Area35");
        private int DefinitionCity(int area) => StrategyNeed(current.ReadAreaValidityCode(new(area)),"target_Area34");
        private bool ValidForce(int id) => StrategyNeed(current.ReadForceNativeValidity(current.ReadForceTableReference(id < 52 ? id : 0)
            ?? throw StrategyMissing("target_Force_pointer")),"target_Force_validity");
        private byte ForceByte(int force,int offset)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force,out var image)==true)
                return StrategyNeed(image[offset],"target_reset_Force_byte");
            var fields=state.NativeArmySimulation?.Forces.GetValueOrDefault(force) ?? throw StrategyMissing("target_Force_owner");
            return StrategyNeed(offset==0x12 ? fields.Flags12 : fields.Flags194,"target_Force_byte");
        }
        private byte Relation(int force,int other,bool treaty)
        {
            int offset=treaty?0xEA:0x13;
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force,out var image)==true)
                return StrategyNeed(image[offset+other],"target_reset_Force_relation");
            byte[]? values=force==0 ? (treaty ? state.NativeForceUnregisterCurrent?.PlaceholderRelationsEA : state.NativeForceUnregisterCurrent?.PlaceholderRelations13)
                : treaty ? state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.RelationsEA
                : state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Relations13;
            return values is {Length:52} ? values[other] : throw StrategyMissing("target_Force_relation");
        }
        private bool Unprotected(int target) => (Relation(target,own,true)&127)==0 &&
            ((ForceByte(own,0x12)&2)==0 || (ForceByte(target,0x12)&2)==0);
        private int CityCount(int force)
        {
            if (!cityCounts.TryGetValue(force,out int count))
            {
                count=StrategyNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current,new(force)),"target_Force_City_count");
                cityCounts.Add(force,count);
            }
            return count;
        }
        private int[] Neighbors(int city)
        {
            if (neighbors.TryGetValue(city,out var cached)) return cached;
            neighborhood ??= new(package._scenario);
            var list=current.CreateTemporaryHexList() ?? throw StrategyMissing("target_neighbor_list");
            var result=new List<int>();
            try
            {
                neighborhood.FillCities22F430(current,City(city),list);
                var node=current.ReadRegistryHead(new(list.Identity)) ?? throw StrategyMissing("target_neighbor_head");
                while (node.Identity is not null)
                {
                    var item=current.ReadRegistryNodePayload(node) ?? throw StrategyMissing("target_neighbor_payload");
                    if (item.Kind!=RegistryKind.City) throw StrategyMissing("target_neighbor_kind");
                    result.Add(StrategyNeed(item.PhysicalSlot,"target_neighbor_City"));
                    node=current.ReadRegistryNodeNext(node) ?? throw StrategyMissing("target_neighbor_next");
                }
            }
            finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            return neighbors[city]=result.ToArray();
        }
        internal bool Borders(int first,int second)
        {
            if (first==0 || second==0) return false;
            for(int city=0;city<52;city++) if(CityOwner(City(city))==first)
                foreach(int adjacent in Neighbors(city)) if(CityOwner(City(adjacent))==second) return true;
            return false;
        }
        internal IReadOnlyList<StrategicCommandCandidate> ReadForces(int? excluded)
        {
            var result=new List<int>();
            var protection=new DiplomacyCommandSource(state,current);
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw StrategyMissing("target_Force_C0"))
            {
                int force=StrategyNeed(entry?.PhysicalSlot,"target_Force_C0_pointer");
                if(force is >=46 and <=50 && ForceByte(force,0x194)!=0) continue;
                if(force==own || force==excluded || protection.HasTreaty(force,own)) continue;
                result.Add(force);
            }
            int reference=excluded is >=1 and <=51 ? excluded.Value : own;
            StrategySort(result,(left,right) =>
            {
                bool a=Borders(reference,left),b=Borders(reference,right); if(a!=b) return a;
                int ar=Relation(reference,left,false),br=Relation(reference,right,false);if(ar!=br)return ar<br;
                int ac=CityCount(left),bc=CityCount(right);return ac!=bc ? ac>bc : left<right;
            });
            return result.Select(id => new StrategicCommandCandidate(id,
                state.Forces.GetValueOrDefault(id)?.Name ?? throw StrategyMissing("target_Force_name"),id,null,null)
                {PhysicalSlot=id,Enabled=true}).ToArray();
        }
        internal IReadOnlyList<StrategicCommandCandidate> Read(int kind)
        {
            if(kind==11) return ReadCities();
            if(kind is not(13 or 14 or 20))throw StrategyMissing("territory_kind");
            var result=new List<int>();
            if(kind==20)
            {
                //6DBED0 enumerates managerE0 Object receivers, not manager60 Armies.
                var areas=new HashSet<int>();
                foreach(var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw StrategyMissing("disturb_Object_E0"))
                {
                    int physical=StrategyNeed(entry?.PhysicalSlot,"disturb_Object_pointer");
                    var obj=current.ReadPhysicalObject(physical) ?? throw StrategyMissing("disturb_Object_record");
                    var definition=current.ReadObjectDefinitionReference(obj.RawField12<31 ? obj.RawField12 : 0)
                        ?? throw StrategyMissing("disturb_Object_definition");
                    if(StrategyNeed(current.ReadObjectDefinitionRaw2C(definition),"disturb_Object_category")!=2)continue;
                    int owner=StrategyNeed(current.ReadObjectForceSlot13(new(physical)),"disturb_Object_Force");
                    if(!StrategyNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state,own,owner),"disturb_relation2CA650"))continue;
                    int raw=StrategyNeed(current.ReadObjectHexIndex(new(physical)),"disturb_Object_Hex");
                    var hex=current.ReadHexTableReference(raw<48400?raw:0) ?? throw StrategyMissing("disturb_Hex_pointer");
                    int area=StrategyNeed(current.ReadHexAreaSlot(hex),"disturb_Hex_Area");
                    var pointer=current.ReadAreaTableReference(area<501?area:0) ?? throw StrategyMissing("disturb_Area_pointer");
                    areas.Add(StrategyNeed(pointer.PhysicalSlot,"disturb_Area_physical"));
                }
                foreach(int area in areas.Order())if(CanDisturb(area))result.Add(area);
            }
            else
            {
                HashSet<int>? enemyNeighbors=null;
                if(kind==13)
                {
                    enemyNeighbors=[];
                    for(int city=0;city<52;city++)if(CityOwner(City(city))==own)
                        foreach(int adjacent in Neighbors(city))if(CityOwner(City(adjacent))!=own)enemyNeighbors.Add(adjacent);
                }
                foreach(var area in current.ReadRegisteredAreaReferences() ?? throw StrategyMissing("target_Area_B0"))
                {
                    int physical=StrategyNeed(area?.PhysicalSlot,"target_Area_pointer");
                    if(kind==13 && PoisonTutorialRestricted() && physical!=44)continue;
                    if(kind==14 ? CanFire(physical) : CanPoison(physical,enemyNeighbors!))result.Add(physical);
                }
                StrategySort(result,(left,right)=>BeforeArea(left,right,kind));
            }
            return result.Select(area=>new StrategicCommandCandidate(area,
                state.Areas.GetValueOrDefault(area)?.Name ?? throw StrategyMissing("target_Area_name"),AreaOwner(area),AssignedCity(area),
                StrategyNeed(current.ReadAreaHexIndex(new(area)),"target_Area_Hex")){PhysicalSlot=area,Enabled=true}).ToArray();
        }
        private bool CanFire(int area)
        {
            int city=AssignedCity(area);
            if(CityOwner(City(city))!=own && !Neighbors(city).Any(id=>CityOwner(City(id))==own))return false;
            int force=AreaOwner(area);
            return ValidForce(force) && force is not(>=46 and <=50) && force!=own && Unprotected(force);
        }
        private bool CanDisturb(int area)
        {
            int force=AreaOwner(area); if(force==own)return true;
            int city=AssignedCity(area);
            if(!Neighbors(city).Any(id=>CityOwner(City(id))==own))return false;
            if(!ValidForce(force))return true;
            int governor=StrategyNeed(current.ReadArmyReturnBaseGovernor2E(City(city)),"disturb_City_governor");
            var person=current.ReadPersonLookupReference(governor<=6000 ? governor:0) ?? throw StrategyMissing("disturb_governor_pointer");
            int physical=StrategyNeed(person.PhysicalSlot,"disturb_governor_physical");
            //232950 only tests the actual governor's E2 range and City virtualB0.
            if(StrategyNeed(current.ReadPersonFirstLocation(physical),"disturb_governor_E2") is >=1 and <=61 && CityOwner(City(city)) is >=46 and <=50)return false;
            return Unprotected(force);
        }
        private bool PoisonTutorialRestricted()
        {
            var app=package.ReadApplicationGameplay() ?? throw StrategyMissing("poison_Application");
            if(!StrategyNeed(app.TutorialServicePresent,"poison_tutorial_service"))return false;
            if(!StrategyNeed(app.TutorialActive,"poison_tutorial_active"))return false;
            if(!StrategyNeed(app.SpecialModeServicePresent,"poison_special_service"))return false;
            throw StrategyMissing("poison_special_service_byteB_bit10");
        }
        private bool CanPoison(int area,HashSet<int> enemyNeighbors)
        {
            int city=DefinitionCity(area);bool accessible=CityOwner(City(city))==own;
            for(int index=0;!accessible && index<8;index++)
            {
                int raw=StrategyNeed(current.ReadAreaNeighborSlot(new(area),index),"poison_neighbor56");
                var next=current.ReadAreaTableReference(raw<501 ? raw:0) ?? throw StrategyMissing("poison_neighbor_pointer");
                if(!StrategyNeed(current.ReadAreaNativeValidity(next),"poison_neighbor_validity"))continue;
                int mask=package._scenario.Snapshot.Areas[area].RegionalNeighborMask;
                if((mask&(1<<index))!=0 && StrategyNeed(current.ReadAreaCurrentForceSlot(next),"poison_neighbor_Force")==own && enemyNeighbors.Contains(city))accessible=true;
            }
            if(!accessible)return false;
            int force=AreaOwner(area);if(!ValidForce(force))return true;
            var assigned=City(AssignedCity(area));
            int hex=StrategyNeed(current.ReadAreaHexIndex(new(area)),"poison_Area3A");
            if(StrategyNeed(current.ReadBaseNativeValidity(assigned),"poison_City_validity"))
            {
                int link=StrategyNeed(current.ReadBaseObjectLink(assigned),"poison_City4E");
                var obj=current.ReadObjectTableReference(link<3001?link:0) ?? throw StrategyMissing("poison_City_Object_pointer");
                if(StrategyNeed(current.ReadObjectNativeValidity(obj),"poison_City_Object_validity") &&
                    StrategyNeed(current.ReadObjectHexIndex(obj),"poison_City_Object_Hex")==hex)return false;
            }
            var at=current.ReadHexTableReference(hex<48400?hex:0) ?? throw StrategyMissing("poison_Hex_pointer");
            if(StrategyNeed(current.ReadHexSurfaceCode(at),"poison_Hex_surface")==17)return false;
            return force!=own && Unprotected(force);
        }
        private bool BeforeArea(int left,int right,int kind)
        {
            int a=AreaOwner(left),b=AreaOwner(right);
            if(kind==13 && (a is >=1 and <=51)!=(b is >=1 and <=51))return a is not(>=1 and <=51);
            int ar=Relation(own,a<52?a:0,false),br=Relation(own,b<52?b:0,false);if(ar!=br)return ar<br;
            int ac=CityCount(a),bc=CityCount(b);
            return ac!=bc ? kind==13 ? ac<bc : ac>bc : left<right;
        }
        private IReadOnlyList<StrategicCommandCandidate> ReadCities()
        {
            var result=new List<int>();var emperor=package.ReadStrategicEmperorCityCurrent(state,current);
            var diplomacy=new DiplomacyCommandSource(state,current);
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw StrategyMissing("independence_City90"))
            {
                int city=StrategyNeed(entry?.PhysicalSlot,"independence_City_pointer");var pointer=City(city);
                int force=CityOwner(pointer);
                if(!ValidForce(force) || force is >=46 and <=50 || force==own || !Unprotected(force))continue;
                int raw=StrategyNeed(current.ReadArmyReturnBaseGovernor2E(pointer),"independence_governor");
                var governor=current.ReadPersonLookupReference(raw<=6000?raw:0) ?? throw StrategyMissing("independence_governor_pointer");
                int physical=StrategyNeed(governor.PhysicalSlot,"independence_governor_physical");
                if(!StrategyNeed(current.ReadPersonNativeValidity(physical),"independence_governor_validity") || StrategyNeed(current.ReadPersonStatus(physical),"independence_governor_status")==1)continue;
                var capital=diplomacy.Capital(force);
                int capitalLink=StrategyNeed(current.ReadBaseObjectLink(capital),"independence_capital4E");
                int link=StrategyNeed(current.ReadBaseObjectLink(pointer),"independence_City4E");
                if(capitalLink==link)continue;
                if(capitalLink is >=52 and <=61)
                {
                    var home=CurrentGateMapQueries.FindCity(capital,current);
                    if(StrategyNeed(current.ReadBaseNativeValidity(home),"independence_capital_City_validity") &&
                        StrategyNeed(current.ReadBaseObjectLink(home),"independence_capital_City4E")==link)continue;
                }
                if(StrategyNeed(current.ReadBaseNativeValidity(emperor),"independence_Emperor_City_validity") && emperor.PhysicalSlot==city)continue;
                result.Add(city);
            }
            int GovernorLoyalty(int city)
            {
                int raw=StrategyNeed(current.ReadArmyReturnBaseGovernor2E(City(city)),"independence_sort_governor");
                int physical=StrategyNeed((current.ReadPersonLookupReference(raw<=6000?raw:0) ?? throw StrategyMissing("independence_sort_governor_pointer")).PhysicalSlot,"independence_sort_governor_physical");
                return StrategyNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state,current,physical,package._governorInputs),"independence_sort_loyalty");
            }
            StrategySort(result,(left,right)=>
            {
                int a=GovernorLoyalty(left),b=GovernorLoyalty(right);if(a!=b)return a<b;
                a=Relation(own,CityOwner(City(left)),false);b=Relation(own,CityOwner(City(right)),false);if(a!=b)return a<b;
                a=CityCount(CityOwner(City(left)));b=CityCount(CityOwner(City(right)));return a!=b?a>b:left<right;
            });
            return result.Select(id=>new StrategicCommandCandidate(id,state.Cities.GetValueOrDefault(id)?.Name ?? throw StrategyMissing("independence_City_name"),
                CityOwner(City(id)),id,current.ReadBaseCurrentHexIndex(City(id))){PhysicalSlot=id,Enabled=true}).ToArray();
        }
    }
}
