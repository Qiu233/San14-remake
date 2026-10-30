using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record PersonalityEditorChoice(int Code, string Caption, int Minimum, int Maximum, int Step, int Category);
public sealed record PersonalityEditorReference(int Id, string Name, string Description);

/// <summary>Captured ordinary editor captions and choices. No current world or
/// application random state is needed to edit a detached definition.</summary>
public sealed class PersonalityEditorCatalog
{
    private readonly LoadedMessages messages;
    private readonly SourceWorldContext referenceDefinitions;
    public string Locale { get; }
    private PersonalityEditorCatalog(GameResourceRepository repository, string locale)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        Locale = locale; messages = repository.LoadMessages(locale, MessageKind.Data);
        referenceDefinitions = new(); referenceDefinitions.InitializePersonalityStorage();
        referenceDefinitions.MaterializeFixedPersonalities(repository.LoadPersonalities($"0010_{locale}/fixdataexce.s14"));
        PersonalityNormalization.ApplyManagerTable(referenceDefinitions);
    }
    public static PersonalityEditorCatalog Load(GameResourceRepository repository, string locale) => new(repository, locale);
    public string Data(int id) => Data(1, id);
    public string Data(int section, int id) =>
        (uint)section < (uint)messages.Messages.Sections.Count &&
        (uint)id < (uint)messages.Messages.Sections[section].Records.Count &&
        messages.Messages.Sections[section].Records[id].Text is string text ? text :
        throw new InvalidDataException($"personality.editor.data.{section}.{id:X}");
    public IReadOnlyList<PersonalityEditorChoice> Conditions => PersonalityEditorOptions.Conditions
        .Select(value => Choice(value, Data(0, 0x16CC + value.Code))).ToArray();
    public IReadOnlyList<PersonalityEditorChoice> ReadOptions(PersonalityEditorDraft draft) =>
        PersonalityEditorOptions.ReadOptions(draft.Value.ConditionKind).Select(code =>
            new PersonalityEditorChoice(code, OptionName(draft.Value.ConditionKind, code), 0, 0, 1, 0)).ToArray();
    public IReadOnlyList<PersonalityEditorChoice> ReadEffects(PersonalityEditorDraft draft, int slot) =>
        PersonalityEditorOptions.ReadEffects(draft.Value.ConditionKind, draft.Value.OptionKind, slot,
            slot == 1 ? draft.Value.Effect2Code : draft.Value.Effect1Code)
        .Select(value => Choice(value, Data(0, 0x164D + value.Code))).ToArray();
    public PersonalityEditorDraft CreateDraft(int id) => new(new(id, "", 0, 0, 0, 0, 0, 0, 0, 0));
    public PersonalityEditorDraft Edit(CreatedPersonality definition) => new(definition);
    public string ConditionName(int code) => Data(0, 0x16CC + code);
    public string OptionName(int condition, int option) => condition is not (9 or 10) && option is 1 or 37
        ? Data(0x81B) : Data(0, 0x16CC + option);
    public string EffectName(int code) => Data(0, 0x164D + code);
    public string ConditionAmount(CreatedPersonality value) => value.ConditionKind switch
    {
        9 or 10 or 21 or 22 or 31 or 32 or 39 or 40 => Data(0x80C)
            .Replace("%d", unchecked((int)value.ConditionValue).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        20 => "",
        _ => Data(0x353)
    };
    public string EffectAmount(int code, int storedAmount) =>
        AreaAdministrationOfficerTraitTooltip.Number(code, storedAmount, Data(0x353));
    /// <summary>The reference button opens a read-only ordinary trait list.
    /// It never selects or copies a definition into the draft.</summary>
    public IReadOnlyList<PersonalityEditorReference> ReadReferences(PersonalityEditorDraft draft, int field,
        int? selectedCode = null)
    {
        if (field is not (1 or 3 or 4)) throw new ArgumentOutOfRangeException(nameof(field));
        int condition = field == 1 ? selectedCode ?? draft.Value.ConditionKind : 0;
        int effect = field == 3 ? selectedCode ?? draft.Value.Effect1Code
            : field == 4 ? selectedCode ?? draft.Value.Effect2Code : 0;
        var rows = new List<PersonalityEditorReference>();
        for (int id = 1; id < 256; id++)
        {
            var reference = new PersonalityReference(id);
            if (referenceDefinitions.ReadSkillPersonalityValidity(reference) != true ||
                referenceDefinitions.ReadPersonalityByte(reference, 0xBE) == 0 ||
                (referenceDefinitions.ReadSkillPersonalityFlags(reference)!.Value & 0x10) == 0 ||
                condition != 0 && referenceDefinitions.ReadPersonalityByte(reference, 0xA4) != condition ||
                effect != 0 && referenceDefinitions.ReadPersonalityByte(reference, 0xB6) != effect &&
                    referenceDefinitions.ReadPersonalityByte(reference, 0xBA) != effect) continue;
            var description = new System.Text.StringBuilder();
            for (int unit = 0; unit < 51; unit++)
            {
                ushort value = referenceDefinitions.ReadPersonalityWord(reference, 0x3C + 2 * unit) ??
                    throw new InvalidDataException("Personality reference description is unavailable.");
                if (value == 0) break;
                description.Append((char)value);
            }
            rows.Add(new(id, referenceDefinitions.ReadPersonalityName(reference), description.ToString()));
        }
        return rows.AsReadOnly();
    }
    private static PersonalityEditorChoice Choice(PersonalityEditorOption value, string text) =>
        new(value.Code, text, value.Minimum, value.Maximum, value.Step, value.Category);
}

public sealed class PersonalityEditorDraft
{
    private readonly CreatedPersonality original;
    public CreatedPersonality Value { get; private set; }
    public bool IsDirty => Value != original;
    public bool ConditionOptionEnabled => Value.ConditionKind != 0 &&
        PersonalityEditorOptions.ReadOptions(Value.ConditionKind).Count >= 2;
    public bool Effect2Enabled => Value.Effect1Code != 0 &&
        PersonalityEditorOptions.ReadEffects(Value.ConditionKind, Value.OptionKind, 2, Value.Effect1Code).Count >= 2;
    internal PersonalityEditorDraft(CreatedPersonality value)
    { value.Validate(); original = value; Value = value; }
    public void SetName(string name)
    {
        if (name is null || name.Length > 2 || name.Contains('\0'))
            throw new InvalidDataException("A custom personality name may contain at most two UTF-16 units.");
        Value = Value with { Name = name };
    }
    public void SetCondition(int code) => SetCondition(code,
        PersonalityEditorOptions.Conditions.FirstOrDefault(value => value.Code == code)?.Minimum ??
        throw new InvalidDataException("Unavailable personality condition."));
    public void SetCondition(int code, int value)
    {
        var range = PersonalityEditorOptions.Conditions.FirstOrDefault(row => row.Code == code) ??
            throw new InvalidDataException("Unavailable personality condition.");
        if (value < range.Minimum || value > range.Maximum)
            throw new InvalidDataException("The personality condition amount is out of range.");
        Value = Value with { ConditionKind = code, ConditionValue = unchecked((uint)value) };
        var options = PersonalityEditorOptions.ReadOptions(code);
        if (code == 0) Value = Value with { OptionKind = 0 };
        else if (!options.Contains(Value.OptionKind)) Value = Value with { OptionKind = options[0] };
        NormalizeEffects();
    }
    public void SetOption(int code)
    {
        if (!PersonalityEditorOptions.ReadOptions(Value.ConditionKind).Contains(code))
            throw new InvalidDataException("Unavailable personality condition option.");
        Value = Value with { OptionKind = code };
        NormalizeEffects();
    }
    /// <summary>Amount is the editor's displayed number. Effect98 is stored
    /// with its opposite sign by the source acceptance handler.</summary>
    public void SetEffect(int slot, int code, int amount)
    {
        var choice = PersonalityEditorOptions.ReadEffects(Value.ConditionKind, Value.OptionKind, slot,
            slot == 1 ? Value.Effect2Code : Value.Effect1Code).FirstOrDefault(row => row.Code == code) ??
            throw new InvalidDataException("Unavailable personality effect.");
        if (amount < choice.Minimum || amount > choice.Maximum || code != 0 && amount == 0)
            throw new InvalidDataException("The personality effect amount is out of range.");
        int stored = code == 98 ? -amount : amount;
        Value = slot == 1 ? Value with { Effect1Code = code, Effect1Value = stored }
            : Value with { Effect2Code = code, Effect2Value = stored };
    }
    public bool CanConfirm
    {
        get
        {
            if (Value.Name.Length == 0 || Value.Effect1Code == 0 ||
                !AmountValid(1, Value.Effect1Code, Value.Effect1Value) ||
                Value.Effect2Code == 0 && Value.Effect2Value != 0 ||
                Value.Effect2Code != 0 && !AmountValid(2, Value.Effect2Code, Value.Effect2Value)) return false;
            if (Value.ConditionKind == 0) return true;
            var condition = PersonalityEditorOptions.Conditions.FirstOrDefault(row => row.Code == Value.ConditionKind);
            int amount = unchecked((int)Value.ConditionValue);
            return condition is not null && amount >= condition.Minimum && amount <= condition.Maximum &&
                PersonalityEditorOptions.ReadOptions(Value.ConditionKind).Contains(Value.OptionKind);
        }
    }
    public CreatedPersonality ToRecord()
    {
        if (!CanConfirm) throw new InvalidDataException("The personality draft is incomplete.");
        Value.Validate(requireComplete: true); return Value;
    }
    private bool AmountValid(int slot, int code, int stored)
    {
        // The source parent confirmation checks field ranges. Choice exclusion
        // belongs to the selector and does not rewrite an imported definition.
        var range = PersonalityEditorOptions.ReadEffectRange(Value.ConditionKind, code);
        int amount = code == 98 ? -stored : stored;
        return range is not null && amount >= range.Minimum && amount <= range.Maximum;
    }
    private void NormalizeEffects()
    {
        // Source mode5 collects allowed effects without the other-slot filter,
        // then visits first/second with an if/else-if in that exact order.
        var choices = PersonalityEditorOptions.ReadEffects(Value.ConditionKind, Value.OptionKind, 1, 0);
        var first = choices.FirstOrDefault(row => row.Code == Value.Effect1Code);
        var second = Value.Effect2Code == Value.Effect1Code ? null :
            choices.FirstOrDefault(row => row.Code == Value.Effect2Code);
        int firstAmount = first is null ? 0 : NormalizeAmount(first, Value.Effect1Value);
        int secondAmount = second is null ? 0 : NormalizeAmount(second, Value.Effect2Value);
        if (first is null)
        {
            Value = Value with { Effect1Code = second?.Code ?? 0, Effect1Value = secondAmount,
                Effect2Code = 0, Effect2Value = 0 };
        }
        else Value = Value with { Effect1Value = firstAmount,
            Effect2Code = second?.Code ?? 0, Effect2Value = secondAmount };
    }
    private static int NormalizeAmount(PersonalityEditorOption range, int stored)
    {
        // The original candidate constructor negates each code98 endpoint
        // independently. Preserve its ordered comparisons even when reversed.
        int minimum = range.Code == 98 ? -range.Minimum : range.Minimum;
        int maximum = range.Code == 98 ? -range.Maximum : range.Maximum;
        return stored < minimum ? minimum : stored > maximum ? maximum : stored;
    }
}
