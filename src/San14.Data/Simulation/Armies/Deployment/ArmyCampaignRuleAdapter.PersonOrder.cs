using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Orders the caller's complete current rows. Direction and selected
    /// identities belong to its UI owner; this query changes neither world nor selection.</summary>
    public DeploymentPersonOrderQuery QueryDeploymentPersonOrder(GameState state,
        IReadOnlyList<DeploymentPersonSortRow> currentRows, DeploymentPersonSortColumn column, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(currentRows);
        try
        {
            if (!Enum.IsDefined(column)) throw PersonOrderMissing("column");
            var rows = currentRows.ToArray();
            SourceWorldContext? captured = null;
            SourceWorldContext Current() => captured ??= CaptureDeploymentPersonSelectionCurrent(state);
            TacticRemakeCurrentAbilities? abilitySource = null;
            bool capacityBound = false;
            var physicals = new Dictionary<int, int>();
            var scalars = new Dictionary<int, (bool Empty, int Value)>();
            var names = new Dictionary<int, string>();
            var duties = new Dictionary<int, DeploymentPersonResponsibilityKey>();
            var areaNames = new Dictionary<int, string>();
            for (int index = 1; index < rows.Length; index++)
            {
                var row = rows[index]; int at = index;
                while (at > 0 && Before(row, rows[at - 1]))
                { rows[at] = rows[at - 1]; at--; }
                rows[at] = row;
            }
            return new(Array.AsReadOnly(rows.Select(row => row.OfficerId).ToArray()), null);

            bool Before(DeploymentPersonSortRow left, DeploymentPersonSortRow right)
            {
                if (column == DeploymentPersonSortColumn.Selected)
                    return ascending ? !left.Selected && right.Selected : left.Selected && !right.Selected;
                if (left.OfficerId == right.OfficerId) return false;
                if (column == DeploymentPersonSortColumn.Name)
                {
                    int order = CompareNameSortKeys(Name(left.OfficerId), Name(right.OfficerId));
                    return ascending ? order < 0 : order > 0;
                }
                if (column == DeploymentPersonSortColumn.Responsibility)
                {
                    var a = Duty(left.OfficerId); var b = Duty(right.OfficerId);
                    if (a.Weight != b.Weight) return ascending ? a.Weight < b.Weight : a.Weight > b.Weight;
                    return ascending ? DeploymentResponsibilityNameOrder.Before(AreaName(b), AreaName(a)) :
                        DeploymentResponsibilityNameOrder.Before(AreaName(a), AreaName(b));
                }
                var one = Scalar(left.OfficerId); var two = Scalar(right.OfficerId);
                // Source tag2 is known not-applicable and stays last in BOTH directions.
                if (one.Empty != two.Empty) return !one.Empty;
                if (one.Empty) return false;
                return ascending ? one.Value < two.Value : one.Value > two.Value;
            }
            int Physical(int id)
            {
                if (physicals.TryGetValue(id, out int value)) return value;
                int p = _definitions.ResolveOfficerSource(id) ?? throw PersonOrderMissing("person_identity");
                if (Need(Current().ReadPersonIdentity(p), "person_identity") != id || !state.Officers.ContainsKey(id))
                    throw PersonOrderMissing("person_owner");
                return physicals[id] = p;
            }
            string Name(int id)
            {
                if (names.TryGetValue(id, out string? value)) return value;
                int p = Physical(id);
                return names[id] = !Need(Current().ReadPersonNativeValidity(p), "name_person_validity") ? "" :
                    state.Officers[id].CurrentNameSortKey ?? throw PersonOrderMissing("name_key");
            }
            (bool Empty, int Value) Scalar(int id)
            {
                if (scalars.TryGetValue(id, out var value)) return value;
                int p = Physical(id); var current = Current();
                value = column switch
                {
                    DeploymentPersonSortColumn.CommandCapacity => (false, Capacity(p)),
                    >= DeploymentPersonSortColumn.Leadership and <= DeploymentPersonSortColumn.Charisma =>
                        (false, Ability(id, column)),
                    DeploymentPersonSortColumn.Health => (false, Need(OfficerHealthSourceAdapter.ReadByte(current, p), "health")),
                    DeploymentPersonSortColumn.CommandMembership => (false,
                        ReadDeploymentCommandMembership(state, current, p, PersonOrderMissing) ? 0 : 1),
                    DeploymentPersonSortColumn.ServiceStatus => (false, Status(p) * 10 + ((Flags(p) >> 14) & 1)),
                    DeploymentPersonSortColumn.CreditRankStage => Credit(p, id),
                    DeploymentPersonSortColumn.GovernorPriority => (false, Priority(p)),
                    _ => throw PersonOrderMissing("scalar_column")
                };
                return scalars[id] = value;
            }
            int Capacity(int p)
            {
                if (!Need(Current().ReadPersonNativeValidity(p), "capacity_person_validity")) return 0;
                if (!capacityBound)
                {
                    _moraleWaveInputs.BindCurrent(state, Current()); _baseCapacityInputs.BindCurrent(state, Current());
                    capacityBound = true;
                }
                var result = OfficerCommandCapacity.Resolve(state, Current(), p);
                return result.Capacity ?? throw new InvalidDataException(result.UnresolvedInput);
            }
            int Ability(int id, DeploymentPersonSortColumn key)
            {
                abilitySource ??= new(Current(), _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                var a = abilitySource.Resolve(state, id) ?? throw PersonOrderMissing("abilities");
                return key switch
                {
                    DeploymentPersonSortColumn.Leadership => a.Leadership,
                    DeploymentPersonSortColumn.Strength => a.Strength,
                    DeploymentPersonSortColumn.Intelligence => a.Intelligence,
                    DeploymentPersonSortColumn.Politics => a.Politics,
                    _ => a.Charm
                };
            }
            byte Status(int p) => Need(Current().ReadPersonStatus(p), "service_status");
            ushort Flags(int p) => Need(Current().ReadArmyReturnPersonFlags196(p), "person_flags");
            bool Special(int p, int id) => InSpecialBand(id) &&
                Need(Current().ReadPersonFirstLocation(p), "first_location") is >= 1 and <= 61 &&
                Need((Current().ReadPersonForce(p) ?? throw PersonOrderMissing("person_force")).PhysicalSlot,
                    "force_pointer") is not (>= 46 and <= 50);
            (bool Empty, int Value) Credit(int p, int id)
            {
                if (Status(p) == 1 || Special(p, id) || (Flags(p) & 0x4000) != 0) return (true, 0);
                byte stage = Need(OfficerMeritSourceAdapter.EncodeStage(state.Officers[id].MeritState), "credit_stage");
                return (stage == 0, stage);
            }
            int Priority(int p)
            {
                byte priority = Need(_governorInputs.ReadCurrentPriority160(state, p), "governor_priority");
                if ((priority & 1) != 0) return 1;
                return Status(p) is 1 or 2 ? 1 : 0;
            }
            DeploymentPersonResponsibilityKey Duty(int id)
            {
                if (duties.TryGetValue(id, out var value)) return value;
                int p = Physical(id);
                return duties[id] = DeploymentPersonResponsibilitySource.Read(state, Current(), p, () => Special(p, id));
            }
            string AreaName(DeploymentPersonResponsibilityKey key)
            {
                if (key.NamedArea is not int area) return "";
                if (areaNames.TryGetValue(area, out string? value)) return value;
                return areaNames[area] = state.Areas.GetValueOrDefault(area)?.Name ?? throw PersonOrderMissing("area_name");
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static bool ReadDeploymentCommandMembership(GameState state, SourceWorldContext current,
        int physical, Func<string, InvalidDataException> missing)
    {
        T K<T>(T? value, string name) where T : struct => value ?? throw missing(name);
        ushort id = K(current.ReadPersonIdentity(physical), "person_identity");
        var saved = state.CommanderSelection;
        int count = K(saved?.ActiveCount, "commander_active_count");
        if (count == 0) return false;
        if (count is < 0 or > 255 || saved!.Candidates is not { } members || count > members.Length)
            throw missing("commander_members");
        for (int index = 0; index < count; index++)
        {
            int member = K(members[index].OfficerId, "commander_member_identity");
            if (member is < 0 or > ushort.MaxValue) throw missing("commander_member_encoding");
            var pointer = member < 6001 ? current.ReadPersonLookupReference(member) : current.ReadTransferPersonTableReference(0);
            int at = K(pointer?.PhysicalSlot, "commander_member_pointer");
            if (K(current.ReadPersonIdentity(at), "commander_member_person_identity") == id) return true;
        }
        return false;
    }
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw PersonOrderMissing(name);
    private static InvalidDataException PersonOrderMissing(string name) => new("deployment.person-order." + name);
}
