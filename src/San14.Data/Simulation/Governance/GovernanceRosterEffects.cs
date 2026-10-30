using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record GovernanceRosterEffectColumn(int Id, int Width, string Caption,
    bool CanSort, bool InitialDescending);

public sealed record GovernanceRosterEffectRow(int EffectId, int Level, int? Value,
    IReadOnlyDictionary<int, string> Cells)
{
    public string? UnresolvedOwner { get; init; }
}

public sealed record GovernanceRosterEffectsPublication(int ForceId, string Title,
    string BasicTabCaption, IReadOnlyList<GovernanceRosterEffectColumn> Columns,
    IReadOnlyList<GovernanceRosterEffectRow> Rows, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary 6D1590 modal list. The draft is projected through
    /// the appointment writers' pending rules before 26AEA0(flag1) is read.
    /// No appointment, temporary Force, or world parameter bank is published.</summary>
    public GovernanceRosterEffectsPublication ReadGovernanceRosterEffects(GameState state,
        int forceId, GovernanceRoster roster, GameState? tutorialControl = null)
    {
        var data = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id) => (uint)id < (uint)data.Count && data[id].Text is string text
            ? text : throw GovernanceMissing("effects.Data1_" + id.ToString("X", CultureInfo.InvariantCulture));
        var columns = ImmutableArray.Create(
            new GovernanceRosterEffectColumn(0, 300, Message(0x38A), true, false),
            new GovernanceRosterEffectColumn(1, 48, Message(0x250), true, false),
            new GovernanceRosterEffectColumn(2, 860, Message(0x400), false, true));
        var rows = ImmutableArray.CreateBuilder<GovernanceRosterEffectRow>();
        string? unresolved = null;
        try
        {
            if (!state.Forces.TryGetValue(forceId, out var force)) throw GovernanceMissing("effects.force");
            if (roster is null || roster.Groups is null || roster.Groups.Count != 5 ||
                roster.Groups.Any(group => group is null || group.OfficerIds is null || group.OfficerIds.Count > 7) ||
                !roster.Groups.Select(group => group.Group).Order().SequenceEqual(Enumerable.Range(0, 5)))
                throw GovernanceMissing("effects.roster_shape");
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            _baseCapacityInputs.BindCurrent(state, current);
            var reference = current.ReadForceTableReference(forceId) ?? throw GovernanceMissing("effects.force_pointer");
            if (!(current.ReadForceNativeValidity(reference) ?? throw GovernanceMissing("effects.force_validity")))
                throw GovernanceMissing("effects.invalid_force");
            GovernanceRoster active = ReadActiveGovernanceEffectsRoster(state, current, forceId, roster);
            //259210 calls2AA170. The selected lesson is an application owner,
            //independent of the business world's pending appointment fields.
            int lesson = TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(tutorialControl ?? state)
                ?? throw GovernanceMissing("effects.selected_lesson");

            // The manager contains one CForcePolicyData per effect. The first
            // column's 313130 -> 22E0A0 key is the physical effect index, not a
            // locale-dependent name comparison. Preserve that initial order.
            foreach (var definition in state.PolicyEffects.Values.Where(effect => effect.Id is >= 1 and <= 100)
                .OrderBy(effect => effect.Id))
            {
                int level;
                try
                {
                    level = Math.Min(10, BattleRemakeCurrentForcePolicies.ReadGovernanceTargetLevel(
                        state, force, active, definition.Id, current, suppressCreditBonus: lesson != -1)
                        ?? throw GovernanceMissing("effects.level_" + definition.Id));
                }
                catch (InvalidDataException error) { unresolved ??= error.Message; continue; }
                if (level <= 0) continue;
                int? value = BattleRemakeCurrentForcePolicies.MapEffectAtLevel(state, definition.Id, level);
                string? issue = null;
                string description = "";
                try
                {
                    if (value is not int amount) throw GovernanceMissing("effects.value_" + definition.Id);
                    description = FormatGovernanceEffectDescription(definition.Description, amount);
                }
                catch (InvalidDataException error) { issue = error.Message; unresolved ??= issue; }
                rows.Add(new(definition.Id, level, value, new Dictionary<int, string>
                {
                    [0] = definition.Name,
                    [1] = level.ToString(CultureInfo.InvariantCulture),
                    [2] = description,
                }.ToImmutableDictionary()) { UnresolvedOwner = issue });
            }
        }
        catch (InvalidDataException error) { unresolved ??= error.Message; }
        return new(forceId, Message(0x38A), Message(0x3CD), columns, rows.ToImmutable(), unresolved);
    }

    private static GovernanceRoster ReadActiveGovernanceEffectsRoster(GameState state,
        SourceWorldContext current, int forceId, GovernanceRoster target)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw GovernanceMissing("effects.parameter_owner");
        int Word(string key) => parameters.Int32Values.TryGetValue(key, out int value) &&
            value is >= 0 and <= ushort.MaxValue ? value : throw GovernanceMissing("effects.word:" + key);
        int NewPending() => !parameters.Float32Values.ContainsKey(GovernanceRosterParameters.NewAppointmentPending) &&
            parameters.Int32Values.TryGetValue(GovernanceRosterParameters.NewAppointmentPending, out int value)
            ? Math.Clamp(value, 0, ushort.MaxValue) : throw GovernanceMissing("effects.new_appointment_pending");
        (bool Valid, int Identity) Person(int id)
        {
            var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
            int physical = person?.PhysicalSlot ?? throw GovernanceMissing("effects.person_pointer");
            bool valid = current.ReadPersonNativeValidity(physical) ?? throw GovernanceMissing("effects.person_validity");
            return (valid, valid ? current.ReadPersonIdentity(physical) ?? throw GovernanceMissing("effects.person_identity") : 0);
        }

        int oldAdvisor = Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x52));
        var previousAdvisor = Person(oldAdvisor);
        int selectedAdvisor = target.AdvisorId ?? 0;
        // 2EF690 retains the old pending word only when its early-return
        // identity test succeeds. Clearing an appointment has pending zero.
        int advisorPending = (selectedAdvisor == 0 ? oldAdvisor == 0 :
                previousAdvisor.Valid && previousAdvisor.Identity == selectedAdvisor)
            ? Word(BattleRemakeCurrentForcePolicies.ForceFieldKey(forceId, 0x16C))
            : selectedAdvisor == 0 ? 0 : NewPending();
        var groups = ImmutableArray.CreateBuilder<GovernanceDepartmentConfiguration>(5);
        foreach (var group in target.Groups.OrderBy(group => group.Group))
        {
            var active = ImmutableArray.CreateBuilder<int>();
            foreach (int id in group.OfficerIds)
            {
                int? retained = null;
                // 2ED980 uses the LAST matching native-valid old slot. A new
                // appointment does not become active merely by being previewed.
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    int old = Word(BattleRemakeCurrentForcePolicies.DepartmentPersonKey(forceId, group.Group, ordinal));
                    var previous = Person(old);
                    if (previous.Valid && previous.Identity == id)
                        retained = Word(BattleRemakeCurrentForcePolicies.DepartmentFilterKey(forceId, group.Group, ordinal));
                }
                if ((retained ?? NewPending()) == 0) active.Add(id);
            }
            if (selectedAdvisor != 0 && group.OfficerIds.Contains(selectedAdvisor)) selectedAdvisor = 0;
            groups.Add(new(group.Group, active.ToImmutable()));
        }
        return new(selectedAdvisor != 0 && advisorPending == 0 ? selectedAdvisor : null, groups.ToImmutable());
    }

    private static string FormatGovernanceEffectDescription(string format, int value)
    {
        // 313020 passes exactly one integer to the normal wide formatter.
        // Resource descriptions may omit it, and %% is a literal percent.
        var result = new StringBuilder(format.Length + 8);
        bool consumed = false;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { result.Append(format[index]); continue; }
            if (++index == format.Length) throw GovernanceMissing("effects.description_format");
            if (format[index] == '%') { result.Append('%'); continue; }
            if (format[index] != 'd' || consumed) throw GovernanceMissing("effects.description_format");
            result.Append(value.ToString(CultureInfo.InvariantCulture));
            consumed = true;
        }
        return result.ToString();
    }
}
