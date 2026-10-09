using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>Experience and ordered ability-change notifications belonging to
/// one officer. Missing channels remain unknown. Source layout and resource
/// identity belong to the importer, not to this business state.</summary>
public sealed class OfficerGrowthState
{
    private readonly ulong?[]? _experience;
    private readonly OfficerAbilityGrowthNotice[]? _notifications;

    [JsonConstructor]
    public OfficerGrowthState(ulong?[]? experience, OfficerAbilityGrowthNotice[]? notifications)
    {
        _experience = experience?.ToArray();
        if (notifications is not null)
        {
            if (notifications.Any(notice => notice is null) ||
                notifications.Select(notice => notice.AbilityIndex).Distinct().Count() != notifications.Length)
                throw new InvalidDataException("Ability growth notifications require unique ability identities.");
            _notifications = notifications.ToArray();
        }
    }

    // Persistence and external consumers receive detached arrays.
    public ulong?[]? Experience => _experience?.ToArray();
    public OfficerAbilityGrowthNotice[]? Notifications => _notifications?.ToArray();
    public ulong? ExperienceFor(int abilityIndex) => _experience is not null &&
        abilityIndex >= 0 && abilityIndex < _experience.Length ? _experience[abilityIndex] : null;

    /// <summary>Publishes one resolved experience update. Existing notices keep
    /// their first base value, opaque channels and position in the ordered list.</summary>
    public OfficerGrowthState Advance(int abilityIndex, ulong experience,
        int beforeAbility, int afterAbility, int reportContext)
    {
        if (_experience is null || abilityIndex < 0 || abilityIndex >= _experience.Length)
            throw new InvalidOperationException("Ability growth requires the selected experience channel.");
        ulong?[] nextExperience = _experience.ToArray();
        nextExperience[abilityIndex] = experience;
        OfficerAbilityGrowthNotice[]? notifications = _notifications;
        if (afterAbility != beforeAbility)
        {
            if (notifications is null)
                throw new InvalidOperationException("A changed ability requires notification knowledge.");
            int index = Array.FindIndex(notifications, notice => notice.AbilityIndex == abilityIndex);
            notifications = notifications.ToArray();
            if (index >= 0)
                notifications[index] = notifications[index].WithLatest(afterAbility, reportContext);
            else
                notifications = [.. notifications, new(abilityIndex, afterAbility, beforeAbility, reportContext, 0, 1)];
        }
        return new(nextExperience, notifications);
    }
}

/// <summary>One ability change. The two uninterpreted channels are preserved
/// without assigning an unproved business meaning.</summary>
public sealed record OfficerAbilityGrowthNotice
{
    [JsonConstructor]
    public OfficerAbilityGrowthNotice(int abilityIndex, int afterAbility, int beforeAbility,
        int reportContext, int uninterpretedCode, int uninterpretedState)
    {
        if (abilityIndex < 0 || afterAbility < 0 || beforeAbility < 0)
            throw new InvalidDataException("Ability growth identities and base values cannot be negative.");
        AbilityIndex = abilityIndex;
        AfterAbility = afterAbility;
        BeforeAbility = beforeAbility;
        ReportContext = reportContext;
        UninterpretedCode = uninterpretedCode;
        UninterpretedState = uninterpretedState;
    }
    public int AbilityIndex { get; }
    public int AfterAbility { get; }
    public int BeforeAbility { get; }
    public int ReportContext { get; }
    public int UninterpretedCode { get; }
    public int UninterpretedState { get; }

    public OfficerAbilityGrowthNotice WithLatest(int afterAbility, int reportContext) =>
        new(AbilityIndex, afterAbility, BeforeAbility, reportContext, UninterpretedCode, UninterpretedState);
}

public sealed partial record OfficerState
{
    public OfficerGrowthState? Growth { get; init; }
}
