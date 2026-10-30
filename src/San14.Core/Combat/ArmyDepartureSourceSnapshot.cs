namespace San14.Core;

/// <summary>One source receiver's render inputs. No mutable receiver table,
/// campaign copy or future commander/force lookup is retained here.</summary>
public sealed record ArmyDepartureSourceSnapshot(ArmyReceiverId ReceiverId, ushort CommanderId,
    byte Status, float? Mobility, ArmyDepartureLabelSnapshot? Label,
    ArmyDepartureOutlineSnapshot? Outline, ArmyDepartureFlagSnapshot? Flag)
{
    public string? GraphicsUnavailable { get; init; }
    public ushort? CurrentHex { get; init; }
    public byte? Direction { get; init; }
    public bool? ArmyValid { get; init; }
    public byte? Flags { get; init; }
    public byte? Abnormal { get; init; }
    public ArmyDeparturePersonalitySnapshot[] Personalities { get; init; } = [];
    internal ArmyDepartureSourceSnapshot Copy() => this with
    {
        Label = Label is null ? null : Label with { MissingFields = Label.MissingFields.ToArray() },
        Personalities = Personalities.ToArray()
    };
    internal void Validate()
    {
        if (ReceiverId.Value <= 0 || Mobility is float mobility && !float.IsFinite(mobility) ||
            Personalities is null || Personalities.Length > 9 || Personalities.Any(entry => entry is null ||
                entry.Slot is < 0 or > 8 || entry.PersonalityId is < 0 or > ushort.MaxValue) ||
            Label is { } label && (label.UnitId <= 0 || label.MissingFields is null || label.MissingFields.Any(value => value is null)) ||
            Outline is { } outline && (!float.IsFinite(outline.PrimaryMultiplier) || !float.IsFinite(outline.DisabledMultiplier)) ||
            Flag is { } flag && (flag.ForcePhysical is < 0 or > 51 || flag.BannerText is null))
            throw new InvalidDataException("Army departure source snapshot is incomplete or invalid.");
    }
}

/// <summary>Already selected source label inputs. Nullable fields preserve
/// unavailable source knowledge rather than substituting current world rows.</summary>
public sealed record ArmyDepartureLabelSnapshot(int UnitId)
{
    public int? ArmyPhysicalSlot { get; init; }
    public bool? ArmyValid { get; init; }
    public ushort? Commander12 { get; init; }
    public ushort? Ready16 { get; init; }
    public byte? Morale1A { get; init; }
    public int? MoraleCapacity { get; init; }
    public int? EffectiveMorale { get; init; }
    public int? PersonPhysicalSlot { get; init; }
    public bool? PersonValid { get; init; }
    public ushort? AppearanceA6 { get; init; }
    public byte? PersonDistrictE0 { get; init; }
    public int? DistrictPhysicalSlot { get; init; }
    public bool? DistrictValid { get; init; }
    public byte? DistrictNumber11 { get; init; }
    public bool? DistrictPredicate233A90 { get; init; }
    public int? ForcePhysicalSlot { get; init; }
    public byte? SourceForceColor49 { get; init; }
    public bool? IsCurrentPlayerForce { get; init; }
    public byte? Army13C { get; init; }
    public byte? SelectedFormation { get; init; }
    public byte? FormationClass7E { get; init; }
    public int? IconSpriteId { get; init; }
    public int? IconPalette { get; init; }
    public string[] MissingFields { get; init; } = [];
}
public sealed record ArmyDepartureOutlineSnapshot(bool UsesOutline, int PrimaryKind,
    uint PrimaryValue, float PrimaryMultiplier, int DisabledKind, uint DisabledValue, float DisabledMultiplier);
public sealed record ArmyDepartureFlagSnapshot(int ForcePhysical, byte? Color, string BannerText,
    string? CarriedText, string? CarriedMissing)
{
    public bool BannerForceValid { get; init; }
    public bool? CarriedForceValid { get; init; }
}
public sealed record ArmyDeparturePersonalitySnapshot(int Slot, int PersonalityId, bool Valid,
    bool Eligible, bool? Active, byte? Style, string? Missing);
