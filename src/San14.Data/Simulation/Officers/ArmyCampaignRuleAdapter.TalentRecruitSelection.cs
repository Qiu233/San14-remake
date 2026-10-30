using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TalentRecruitPersonSelection PrepareTalentRecruitTargetSelection(TalentUiDraft draft)
    {
        RequireRecruitDraft(draft);
        var source = draft.Source;
        return new OfficerInformationCurrent(this, source.State, source.ForceId, true, source.Current)
            .RecruitPages(source.TargetOfficerIds, [], 1, true, PrepareCommanderSelectionGuide(), null);
    }

    public TalentRecruitPersonSelection PrepareTalentRecruitExecutorSelection(TalentUiDraft draft)
    {
        RequireRecruitDraft(draft);
        var source = draft.Source;
        var prepared = source.RecruitSelectionRecommendations
            ?? throw new InvalidDataException("talent.recruit.selection.prepared_rows");
        return new OfficerInformationCurrent(this, source.State, source.ForceId, true, source.Current)
            .RecruitPages(source.OrderRecruitExecutors(draft.ExecutorOfficerIds), draft.SelectedExecutorOfficerIds,
                draft.MaximumExecutors, false, PrepareCommanderSelectionGuide(), prepared.ToDictionary(row => row.OfficerId));
    }

    private static void RequireRecruitDraft(TalentUiDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.Recruitment) throw new ArgumentException("This selector requires the actual Recruit draft.", nameof(draft));
    }

    //6DF2A0 visits the actual Person registry and applies1EF770 before its
    //status/loyalty/identity ordering. It does not use the generic UI targets.
    private IReadOnlyList<int> ReadTalentRecruitTargetsCurrent(GameState state, SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw RecruitSelectionMissing("Force_pointer");
        int forcePhysical = RecruitSelectionNeed(force.PhysicalSlot, "Force_physical");
        var rows = new List<(int Id, int Status, int Loyalty)>();
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw RecruitSelectionMissing("Person_registry"))
        {
            int physical = RecruitSelectionNeed(entry, "Person_pointer");
            if (PersonCurrentBusySource.Read(current, physical, RecruitSelectionMissing)) continue;
            int status = RecruitSelectionNeed(current.ReadPersonStatus(physical), "Person_status");
            if (status == 1) continue;
            int rawDistrict = RecruitSelectionNeed(current.ReadPersonDistrictSlot(physical), "Person_District");
            var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                ?? throw RecruitSelectionMissing("District_pointer");
            //22DD20 returns the District pointer ordinal, not its Force field.
            int districtPhysical = RecruitSelectionNeed(district.PhysicalSlot, "District_physical");
            if (districtPhysical is >= 46 and <= 51 || DuelAftermathSpecialPerson(current, physical) ||
                (RecruitSelectionNeed(current.ReadArmyReturnPersonFlags196(physical), "Person_flags") & 0x4000) != 0) continue;
            bool allowed;
            if (status is >= 2 and <= 4)
            {
                var other = current.ReadPersonForce(physical) ?? throw RecruitSelectionMissing("Person_Force");
                bool friendly = !RecruitSelectionNeed(current.ReadForceNativeValidity(force), "Force_validity") ||
                    !RecruitSelectionNeed(current.ReadForceNativeValidity(other), "Person_Force_validity") ? false :
                    RecruitSelectionNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, force, other), "Force_friendly_relation");
                allowed = !friendly;
            }
            else if (status == 5)
            {
                var hex = PersonReturnCommands.ResolvePersonPosition(state, current, physical,
                    state.NativeMapSeason?.ThirdDate ?? throw RecruitSelectionMissing("date"));
                if (hex.PhysicalSlot is null) continue;
                int rawArea = RecruitSelectionNeed(current.ReadHexAreaSlot(hex), "Person_Hex_Area");
                var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                    ?? throw RecruitSelectionMissing("Person_Area_pointer");
                allowed = RecruitSelectionNeed(current.ReadAreaCurrentForceSlot(area), "Person_Area_Force") == forcePhysical;
            }
            else if (status == 6)
            {
                var position = PersonPositionRepair.DecodeSecondLocation(
                    RecruitSelectionNeed(current.ReadPersonSecondLocation(physical), "captive_position"));
                BaseReference? location = position.Kind switch
                {
                    5 => current.ReadCityTableReference(position.Index <= 51 ? position.Index : 0),
                    6 => current.ReadGateTableReference(position.Index <= 10 ? position.Index : 0),
                    _ => null
                };
                allowed = location is not null &&
                    RecruitSelectionNeed(current.ReadBaseForceSlot(location), "captive_Base_Force") == forcePhysical;
            }
            else allowed = false;
            if (!allowed) continue;
            int id = RecruitSelectionNeed(current.ReadPersonIdentity(physical), "Person_identity");
            int loyalty = RecruitSelectionNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical, _governorInputs), "Person_raw_loyalty");
            rows.Add((id, status, loyalty));
        }
        return Array.AsReadOnly(rows.OrderBy(row => row.Status == 5 ? 0 : row.Status == 6 ? 1 : 2)
            .ThenBy(row => row.Loyalty).ThenBy(row => row.Id).Select(row => row.Id).ToArray());
    }

    private static T RecruitSelectionNeed<T>(T? value, string field) where T : struct => value ?? throw RecruitSelectionMissing(field);
    private static InvalidDataException RecruitSelectionMissing(string field) => new("talent.recruit.selection." + field);

    private sealed partial class OfficerInformationCurrent
    {
        internal TalentRecruitPersonSelection RecruitPages(IReadOnlyList<int> ids, IReadOnlyList<int> initial,
            int maximum, bool target, string guide, IReadOnlyDictionary<int, TalentOfficerRecommendation>? prepared)
        {
            commandBodyPalette = true;
            var columns = OfficerInformationSources.Create(Data).SelectMany(group => group.Columns)
                .GroupBy(column => column.Id).ToDictionary(group => group.Key, group => group.First());
            void Add(int id, int caption, int width, int format, bool ascending)
                => columns[id] = new(id, caption, Data(1, caption), width, format, ascending, true, true);
            for (int i = 0; i < 5; i++) Add(13 + i, 0x238 + i, 76, 5, false);
            Add(80, 0x3D7, 132, 5, false);
            Add(75, 0x44D, 132, 4, false);
            Add(102, 0x26C, 76, 5, true);
            Add(103, 0x51B, 76, 4, false); Add(104, 0x51A, 76, 4, false);
            Add(128, 0x81A, 76, 4, false); Add(129, 0x81D, 76, 4, false);
            int[][] sequence = target
                ? [[0,1,3,5,6,13,14,15,16,17,80],[0,61,62,63,64,65],[0,29,28,24,30,31,32,33]]
                : [[0,75,3,5,35,11,12,103,104,128,129,102,18,194]];
            int[] captions = target ? [0x3CD,0x23D,0x451] : [0x3CD];
            var groups = sequence.Select((group,index) => new OfficerInformationGroup(index,captions[index],Data(1,captions[index]),
                group.Select(id => columns[id]).ToImmutableArray())).ToImmutableArray();
            var cache = new Dictionary<(int Person,int Column), PreparedCell>();
            Dictionary<int,int>? recruitCounts = null;
            PreparedCell Read(int id, int column)
            {
                if (cache.TryGetValue((id,column), out var saved)) return saved;
                try
                {
                    var person = ReadPerson(id);
                    bool highlighted = target && InformationStatus(person) is 5 or 6;
                    int BaseAbility()
                    {
                        var values = person.Owner.BaseAbilities ?? throw Missing("recruit.base_abilities");
                        int value = (column - 13) switch {0 => values.Leadership,1 => values.Strength,2 => values.Intelligence,
                            3 => values.Politics,_ => values.Charm};
                        return value is >= 0 and <= 255 ? value : throw Missing("recruit.base_ability_byte");
                    }
                    int Count()
                    {
                        if (recruitCounts is null)
                        {
                            var manager = current.ReadOwnedPersonCommandManager() ?? throw Missing("recruit.command_manager");
                            var talents = manager.Talents ?? throw Missing("recruit.Talent_owner");
                            recruitCounts = talents.Values.Where(task => task.Kind == TalentOrderKind.Recruit)
                                .GroupBy(task => task.TargetOfficerId ?? throw Missing("recruit.task_target"))
                                .ToDictionary(group => group.Key, group => group.Count());
                        }
                        return recruitCounts.GetValueOrDefault(id);
                    }
                    TalentOfficerRecommendation Preview() => prepared?.GetValueOrDefault(id) ?? throw Missing("recruit.prepared_candidate");
                    bool Trait(int requested)
                    {
                        for (int i = 0; i < 9; i++)
                            if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person.PhysicalSlot, i),
                                "recruit.trait_slot") == requested) return true;
                        return false;
                    }
                    saved = column switch
                    {
                        >= 13 and <= 17 => MakeCell(() => column != 13 && BaseAbility() == 0 ? Data(1,0x353) : Number(BaseAbility()),
                            () => new NumberKey(BaseAbility())),
                        80 => MakeCell(() => Number(Count()), () => new NumberKey(Count())),
                        75 => MakeCell(() => Preview().Recommended ? Data(1,0x338) : "", () => new NumberKey(Preview().Recommended ? 1 : 0)),
                        102 => MakeCell(() => Number(Preview().TravelDays),
                            () => new NumberKey(InformationNativeValid(person) ? Preview().TravelDays : 0,
                                !InformationNativeValid(person)), statePalette: () => BasicRowPalette(person,5)),
                        103 or 104 or 128 or 129 => MakeCell(() => Trait(column switch {103 => 0x78,104 => 0x77,128 => 0xCE,_ => 0xC8}) ? Data(1,0x338) : "",
                            () => new NumberKey(Trait(column switch {103 => 0x78,104 => 0x77,128 => 0xCE,_ => 0xC8}) ? 1 : 0)),
                        5 when target => MakeCell(() => (InformationFlags(person) & 0x4000) != 0 ? Data(1,0x859)
                                : InformationStatus(person) < 10 ? Data(0,0xf9 + InformationStatus(person)) : null,
                            () => new NumberKey(Cells.ServiceStatusKey(person.Candidate)),
                            statePalette: () => BasicRowPalette(person,10,highlighted)),
                        _ => Cell(id,column)
                    };
                    //2C8F10 callback code1 supplies the ordinary65/65/66/11
                    //row fallback. It highlights free/captive rows; it is not
                    //a disabled or injury admission predicate.
                    if (highlighted && saved.Display.StatePresets is null && saved.Display.PresetIndex is null)
                        saved = saved with { Display = saved.Display with { StatePresets = landAdvantageTextPresets } };
                }
                catch (InvalidDataException error) { saved = new(new(null,null,error.Message),null,error.Message); }
                return cache[(id,column)] = saved;
            }
            var pages = new OfficerInformationPageSet(Data(1,target ? 0x58D : 0x58F),groups,ids,
                (id,column) => Read(id,column).Display with { SortUnresolvedOwner = Read(id,column).SortUnresolvedOwner },
                (id,column) => Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending) => (Read(left,column).Key ?? throw Missing("recruit.sort_key"))
                    .Before(Read(right,column).Key ?? throw Missing("recruit.sort_key"),ascending))
                { DynamicPaletteEnabled = true };
            pages.SortSelection(initial,selectedFirst:true);
            return new(pages,initial.ToArray(),maximum,target ? 0x58D : 0x58F,guide,target)
            { AlteredHealth = target ? new Dictionary<int,AreaAdministrationQuery<bool>>() : ids.ToDictionary(id => id,MovementAlteredHealth) };
        }
    }
}
