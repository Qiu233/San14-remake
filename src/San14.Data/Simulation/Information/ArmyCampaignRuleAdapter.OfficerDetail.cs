using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The three person pages share this single current capture. A
    /// missing optional value is published beside the remaining known values.</summary>
    public AreaAdministrationQuery<OfficerDetailPublication> QueryOfficerDetail(
        GameState state, int playerForceId, int officerId)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle() ?? throw OfficerDetailMissing("army_lifecycle"));
            ForceUnregisterInputs.BindCurrent(state, current);
            OwnedArmyReturnServiceInputs.BindCurrent(state, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            PersonArmyCaptureInputs.BindCurrent(state, current);
            _personRelationInputs.BindCurrent(state, current);
            _moraleWaveInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            //These are current world values, not the opening scenario's year
            //and mode retained by the immutable definition capture.
            current.EventConditions.CurrentYear = state.NativeMapSeason?.ThirdDate.Year;
            current.EventConditions.ScenarioMode = state.CampaignEventWorld?.ScenarioMode;
            int physical = current.ResolveOfficerSource(officerId) ?? throw OfficerDetailMissing("person_receiver");
            if (current.ReadPersonIdentity(physical) != officerId ||
                !DetailNeed(current.ReadPersonNativeValidity(physical), "person_validity") ||
                !state.Officers.TryGetValue(officerId, out var person)) throw OfficerDetailMissing("person_owner");
            var reader = new OfficerDetailsCurrentReader(this, state, current, playerForceId, person, physical);
            return AreaAdministrationQuery<OfficerDetailPublication>.Known(reader.Read());
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<OfficerDetailPublication>.Unknown(error.Message); }
    }

    private static T DetailNeed<T>(T? value, string field) where T : struct => value ?? throw OfficerDetailMissing(field);
    private static InvalidDataException OfficerDetailMissing(string field) => new("officer-detail." + field);

    private sealed partial class OfficerDetailsCurrentReader(ArmyCampaignRuleAdapter owner,
        GameState state, SourceWorldContext current, int playerForceId, OfficerState person, int physical)
    {
        private readonly HashSet<string> issues = new(StringComparer.Ordinal);
        private readonly CityDomesticCandidate candidate = new(checked((ushort)person.Id), physical);
        private CityDomesticPersonCellSource? cells;
        private CityDomesticPersonCellSource Cells => cells ??= new(state, current, owner._definitions,
            owner._governorInputs, owner._scenario, owner._repository, owner.Seed.Language,
            owner.ReadApplicationGameplay()?.UserSelectionServicePresent);
        private string Data(int section, int id) => owner.ReadHistoryDataText(section, id);
        private byte Status => DetailNeed(current.ReadPersonStatus(physical), "person_status");
        private ushort Flags => DetailNeed(current.ReadArmyReturnPersonFlags196(physical), "person_flags");
        private ForceReference Force => current.ReadPersonForce(physical) ?? throw OfficerDetailMissing("person_force");
        private int ForceId => DetailNeed(Force.PhysicalSlot, "force_receiver");
        private DistrictReference District
        {
            get
            {
                int raw = DetailNeed(current.ReadPersonDistrictSlot(physical), "person_district");
                return current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw OfficerDetailMissing("district_lookup");
            }
        }
        private bool DistrictValid => DetailNeed(current.ReadDistrictNativeValidity(District), "district_validity");
        private bool OrdinaryDisplay => ForceId is not (>=46 and <=50) && !Cells.Special(candidate) && (Flags & 0x4000) == 0;
        private AreaAdministrationQuery<T> Q<T>(Func<T> read)
        {
            try { return AreaAdministrationQuery<T>.Known(read()); }
            catch (InvalidDataException error) { issues.Add(error.Message); return AreaAdministrationQuery<T>.Unknown(error.Message); }
        }

        internal OfficerDetailPublication Read()
        {
            var cards = new AreaAdministrationOfficerCardSource(state, current, owner._definitions,
                owner._scenario, owner.Seed.Language, owner._baseCapacityInputs, owner._repository);
            var card = cards.ReadCity(0, physical, true, 0);
            var overview = ReadOverview();
            var military = ReadMilitary();
            var relations = owner.ReadOfficerDetailRelationsCurrent(state, current, physical);
            var traits = ReadTraits(cards);
            var standing = Q(() => ReadNarrationPortrait(state, current, owner._definitions, person.Id,
                person.PortraitAppearanceIndex ?? throw OfficerDetailMissing("portrait_appearance")));
            var priority = ReadPriority();
            var miniMap = Q<DetailMiniMapValues?>(ReadMiniMap);
            //Only this saved semantic bit is needed. Other unknown behavior
            //channels do not make a known special-person bit disappear.
            var pageCount = Q(() => DetailNeed(current.ReadPersonBehavior(physical)?.UsesSpecialPersonRules,
                "page_count_special_person") ? 2 : 3);
            foreach (var ability in card.Abilities)
            { if (ability.Base.UnresolvedInput is {} b) issues.Add(b); if (ability.Effective.UnresolvedInput is {} e) issues.Add(e); }
            if (card.ForceFlag.UnresolvedInput is {} flag) issues.Add(flag);
            foreach (string issue in relations.UnresolvedOwners) issues.Add(issue);
            foreach (string issue in military.UnresolvedOwners) issues.Add(issue);
            return new(person.Id, physical, person.Name, card.Portrait, overview, military,
                relations, card.Abilities.ToImmutableArray(), traits, issues.ToImmutableArray())
                { StandingPortrait = standing, ForceFlag = card.ForceFlag, Priority = priority, MiniMap = miniMap, PageCount = pageCount };
        }

        private OfficerDetailOverview ReadOverview()
        {
            var loyalty = Q<int?>(() => Cells.Loyalty(candidate));
            var merit = Q<int?>(ReadMerit);
            return new(Q<int?>(() => Cells.Age(candidate, true)),
                Q<string?>(() => Cells.Sex(candidate) switch {0 => Data(1,0x495), 1 => Data(1,0x496), _ => ""}),
                Q<string?>(() => Cells.Health(candidate) is int code ? Data(0,0x123 + code) : ""),
                Q<OfficerDetailEntity?>(ReadForce), Q<OfficerDetailEntity?>(ReadDistrict),
                Q<string?>(() => (Flags & 0x4000) != 0 ? Data(1,0x859) : Status < 10 ? Data(0,0xf9 + Status) : ""),
                Q<OfficerDetailEntity?>(() => ReadPlace(false)), Q<OfficerDetailEntity?>(() => ReadPlace(true)),
                Q<string?>(() => Cells.Office(candidate)),
                Q<int?>(() => DistrictValid ? OfficerCommandCapacity.Resolve(state,current,physical).Capacity
                    ?? throw OfficerDetailMissing("command_capacity") : 0),
                Q<int?>(() => !DistrictValid || Status == 1 ? null : ForceId == 51 || ForceId is >=46 and <=50
                    ? 0 : ArmyReturnCaptivesSource.ReadGuard(state,current,physical)),
                loyalty, Q<int?>(() => Cells.ServiceYears(candidate,true)), Q<int?>(ReadItemCount),
                Q<string?>(ReadMeritRank), merit, Q<string?>(ReadTaskName), Q<string?>(ReadTaskTarget),
                Q<string?>(ReadTaskDuration), Q<string?>(() => Data(1,0xc7)), Q<string?>(() => Data(1,0xc8)))
            {
                HealthCode = Q<int?>(() => Cells.Health(candidate)),
                CourtesyName = Q<string?>(() => person.CurrentCourtesyName ?? throw OfficerDetailMissing("courtesy_name")),
                CommandEmphasized = Q<bool?>(() => DetailNeed(current.ReadForceNativeValidity(Force),"command_force_validity") &&
                    ForceId == playerForceId && Cells.CreditBonus(candidate,1) > 0),
                LoyaltyVisible = new(loyalty.Value.HasValue, loyalty.UnresolvedInput),
                MeritVisible = new(merit.Value.HasValue, merit.UnresolvedInput)
            };
        }

        private OfficerDetailEntity? ReadForce()
        {
            if (!DetailNeed(current.ReadForceNativeValidity(Force),"force_validity")) return null;
            var name = Cells.ForceName(candidate);
            return new(OfficerDetailEntityKind.Force,ForceId,name?.Text ?? "");
        }
        private OfficerDetailEntity? ReadDistrict()
        {
            if (!DistrictValid) return null;
            int id=DetailNeed(District.PhysicalSlot,"district_receiver");
            int number=DetailNeed(current.ReadDistrictNumber(id),"district_number");
            return new(OfficerDetailEntityKind.District,id,Data(1,0x494).Replace("%d",number.ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal));
        }
        private OfficerDetailEntity? ReadPlace(bool actual)
        {
            ushort raw = actual ? DetailNeed(current.ReadPersonSecondLocation(physical),"current_location")
                : DetailNeed(current.ReadPersonFirstLocation(physical),"home_location");
            if (!actual && (ForceId == 51 || ForceId is >=46 and <=50 && raw is not (>=1 and <=61))) return null;
            var decoded = PersonPositionRepair.DecodeSecondLocation(raw);
            if (decoded.Kind == 27)
            {
                var army=current.ReadArmyUnitTableReference(decoded.Index < 501 ? decoded.Index : 0) ?? throw OfficerDetailMissing("location_army_lookup");
                if (!DetailNeed(current.ReadArmyUnitNativeValidity(army),"location_army_validity")) return null;
                int commander=DetailNeed(current.ReadArmyUnitCommanderId(army),"location_army_commander");
                return new(OfficerDetailEntityKind.Army,DetailNeed(army.PhysicalSlot,"location_army_receiver"),
                    ReadPersonName(commander) + Data(1,0x350));
            }
            if (decoded.Kind is not (5 or 6)) return null;
            var location=decoded.Kind==5 ? current.ReadCityTableReference(decoded.Index <52 ? decoded.Index:0)
                : current.ReadGateTableReference(decoded.Index <11 ? decoded.Index:0);
            return BaseEntity(location ?? throw OfficerDetailMissing("location_base_lookup"));
        }
        private OfficerDetailEntity BaseEntity(BaseReference location)
        {
            int p=DetailNeed(location.PhysicalSlot,"location_base_receiver");
            return location.Kind==BaseKind.City ? new(OfficerDetailEntityKind.City,p,
                state.Cities.GetValueOrDefault(p)?.Name ?? throw OfficerDetailMissing("city_name"))
                : new(OfficerDetailEntityKind.Gate,p+51,state.Gates?.GetValueOrDefault(p+51)?.Name ?? throw OfficerDetailMissing("gate_name"));
        }
        private string ReadPersonName(int raw)
        {
            var reference=current.ReadPersonLookupReference(raw is >=0 and <6001 ? raw:0) ?? throw OfficerDetailMissing("name_person_lookup");
            int p=DetailNeed(reference.PhysicalSlot,"name_person_receiver");
            if (!DetailNeed(current.ReadPersonNativeValidity(p),"name_person_validity")) return "";
            int id=DetailNeed(current.ReadPersonIdentity(p),"name_person_identity");
            return state.Officers.GetValueOrDefault(id)?.Name ?? throw OfficerDetailMissing("name_person_owner");
        }
        private int? ReadItemCount()
        {
            if (ForceId is >=46 and <=50 || Cells.Special(candidate)) return null;
            var receiver=current.ReadPersonLookupReference(person.Id) ?? throw OfficerDetailMissing("item_person_lookup");
            int count=0;
            for(int i=0;i<202;i++)
            {
                var item=current.ReadRegistryTableReference(RegistryKind.Item,i) ?? throw OfficerDetailMissing("item_lookup");
                if (!DetailNeed(current.ReadRegistryObjectNativeValidity(item),"item_validity")) continue;
                int p=DetailNeed(item.PhysicalSlot,"item_receiver");
                int raw=DetailNeed(current.ReadCaptureItemOwner4A(p),"item_holder");
                var holder=current.ReadPersonLookupReference(raw<6001?raw:0) ?? throw OfficerDetailMissing("item_holder_lookup");
                if (holder==receiver) count++;
            }
            return count;
        }
        private bool CreditVisible()
        {
            bool restricted=owner.ReadApplicationInformationRestriction(state) ?? throw OfficerDetailMissing("application_information_restriction");
            if(restricted)return false;
            if(state.CampaignEventWorld?.ScenarioMode==3) throw OfficerDetailMissing("credit_CCC_controller");
            return Status!=1 && !Cells.Special(candidate) && (Flags&0x4000)==0;
        }
        private string? ReadMeritRank()
        {
            if(!CreditVisible())return null;
            int stage=DetailNeed(OfficerMeritSourceAdapter.EncodeStage(person.MeritState),"merit_stage");
            return stage is >=1 and <=6 ? owner._scenario.Snapshot.CreditRankParameters?.Ranks[stage].Name
                ?? throw OfficerDetailMissing("merit_rank_name") : null;
        }
        private int? ReadMerit() => CreditVisible() && DetailNeed(OfficerMeritSourceAdapter.EncodeStage(person.MeritState),"merit_stage")!=0
            ? DetailNeed(OfficerMeritSourceAdapter.EncodeMerit(person.MeritState),"merit_value") : null;

        private ImmutableArray<OfficerDetailTrait> ReadTraits(AreaAdministrationOfficerCardSource cards)
        {
            var result=ImmutableArray.CreateBuilder<OfficerDetailTrait>();
            HexListHandle? temporary=null;
            try
            {
                temporary=current.CreateTemporaryHexList() ?? throw OfficerDetailMissing("traits_list");
                //2307D0 scans all nine source slots and appends only those
                //native-valid personalities whose list nodes allocated.
                //907EA0 then displays the first five successful entries.
                var admitted=new List<(int Raw,PersonalityReference Trait)>();
                for(int sourceSlot=0;sourceSlot<9;sourceSlot++)
                {
                    int raw=DetailNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,physical,sourceSlot),"trait_slot."+sourceSlot);
                    var trait=current.ReadPersonalityTableReference(raw is >=0 and <=355?raw:0) ?? throw OfficerDetailMissing("trait_lookup");
                    if(DetailNeed(current.ReadSkillPersonalityValidity(trait),"trait_validity") &&
                        current.AppendSkillTemporaryPersonality(temporary,trait))admitted.Add((raw,trait));
                }
                foreach(var (raw,trait) in admitted.Take(5))
                {
                    int slot=result.Count;
                    try
                    {
                        int p=DetailNeed(trait.PhysicalSlot,"trait_receiver");
                        byte style=DetailNeed(current.ReadPersonalityByte(trait,0xbe),"trait_style");
                        var description=cards.ReadTraitTooltip(raw);
                        if(description.UnresolvedInput is {} missing)issues.Add(missing);
                        result.Add(new(slot,raw,p,current.ReadPersonalityName(trait),style,description,null));
                    }
                    catch(InvalidDataException error){issues.Add(error.Message);result.Add(new(slot,raw,null,null,null,
                        AreaAdministrationQuery<string?>.Unknown(error.Message),error.Message));}
                }
            }
            catch(InvalidDataException error)
            {
                issues.Add(error.Message);
                if(result.Count<5)result.Add(new(result.Count,null,null,null,null,
                    AreaAdministrationQuery<string?>.Unknown(error.Message),error.Message));
            }
            finally
            {
                if(temporary is not null){current.ClearTemporaryHexList(temporary);current.ReleaseTemporaryHexList(temporary);}
            }
            return result.ToImmutable();
        }
    }
}
