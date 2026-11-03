namespace San14.Data;

public sealed record CollectionOfficerCell(int ColumnId, string? Text, CollectionSortValue SortValue)
{
    public string? UnresolvedOwner { get; init; }
    public int SortGroup { get; init; }
}

public sealed record CollectionOfficerRow(int OfficerId,
    IReadOnlyDictionary<int, CollectionOfficerCell> Cells)
{
    public int GroupIndex { get; init; }
}

public sealed record CollectionOfficerTab(int Id, string Caption,
    IReadOnlyList<CollectionColumn> Columns)
{
    public IReadOnlyDictionary<int, bool> InitialSortDirections { get; init; } = new Dictionary<int, bool>();
}

public sealed record CollectionOfficerUnresolvedCandidate(int OfficerId, string UnresolvedOwner);

/// <summary>Application-owned admission facts, independent of a campaign. A
/// missing identity is unknown; it does not mean locked or owned.</summary>
public sealed record CollectionOfficerAdmission(
    IReadOnlyDictionary<int, bool> AllowedOfficerIds)
{
    /// <summary>Actual ownership and availability are independent. A missing
    /// dictionary entry remains unknown even when a content file is present.</summary>
    public IReadOnlyDictionary<int, CollectionOfficerContentAvailability> Content { get; init; } =
        new Dictionary<int, CollectionOfficerContentAvailability>();
    /// <summary>Actual subscribed-app facts used by the independent definition loader.</summary>
    public IReadOnlyDictionary<int, bool?> SubscribedDlcAppIds { get; init; } = new Dictionary<int, bool?>();
    public IReadOnlyDictionary<int, bool?> InstalledDlcAppIds { get; init; } = new Dictionary<int, bool?>();

    public static CollectionOfficerAdmission FromProfile(TransferServiceState profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var allowed = new Dictionary<int, bool>();
        for (int id = 2001; id <= 2088; id++)
        {
            if (!OrdinaryPortraitEligibility.NeedsApplicationSetting(id)) continue;
            int slot = id is >= 2068 and <= 2077 ? id - 2023 : id is >= 2083 and <= 2088 ? id - 2028 : id - 2001;
            if (profile.ReadBonusOfficerUnlocked(slot) is bool unlocked) allowed.Add(id, unlocked);
        }
        return new(allowed);
    }
}

public sealed record CollectionOfficerContentAvailability(bool? Owned, bool? Available);
public sealed record CollectionOfficerSpecialEntry(int Slot, int FilenameKey, int AppId,
    int? OfficerId, SourceProvenance? Source, string? UnresolvedOwner);

public sealed record CollectionOfficerAbility(int Id, string Caption, int Value, int NodeIndex);
public sealed record CollectionOfficerTrait(int Slot, int DefinitionId, string Name,
    byte Style, string? Description, int NodeIndex);
public sealed record CollectionOfficerTactic(int Slot, int TacticId, string Name,
    IReadOnlyList<string> Effects, int NodeIndex)
{
    public TacticBalloonContent? Balloon { get; init; }
}

public sealed record CollectionOfficerFormation(int FormationId, string Name, bool Available,
    int NodeIndex);
public sealed record CollectionOfficerRelatedPortrait(int NodeIndex, OfficerPortraitRoute? Portrait)
{
    public string? UnresolvedOwner { get; init; }
}
public sealed record CollectionOfficerDetailAction(int Index, string Caption, bool Enabled)
{
    public string? UnresolvedOwner { get; init; }
}
public sealed record CollectionOfficerExpansion(int OfficerId, int VariantIndex, int VariantCount,
    CollectionImageRoute? Image, string? UnresolvedOwner = null)
{
    public bool HasAlternate => VariantCount > 1;
}

/// <summary>Controls in the dedicated Collection layouts, kept at the UI
/// resource-publication boundary. Switching pages never reloads person data.</summary>
public sealed record CollectionOfficerDetailPage(int Id, string LayoutName,
    IReadOnlyDictionary<int, string> DetailFields)
{
    public string Caption { get; init; } = "";
    public IReadOnlyList<int> HiddenNodes { get; init; } = [];
    public int? PortraitNodeIndex { get; init; }
    public int? NameplateNodeIndex { get; init; }
    public int? AbilityChartNodeIndex { get; init; }
    public IReadOnlyList<int> CaptionNodes { get; init; } = [];
    public IReadOnlyList<int> ParagraphNodes { get; init; } = [];
    public IReadOnlyList<CollectionOfficerAbility> Abilities { get; init; } = [];
    public IReadOnlyList<CollectionOfficerTrait> Traits { get; init; } = [];
    public IReadOnlyList<CollectionOfficerTactic> Tactics { get; init; } = [];
    public IReadOnlyList<CollectionOfficerFormation> Formations { get; init; } = [];
    public IReadOnlyList<CollectionOfficerRelatedPortrait> RelatedPortraits { get; init; } = [];
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = [];
}

public sealed record CollectionOfficerDetail(int OfficerId, string Name,
    OfficerPortraitRoute? Portrait, IReadOnlyList<CollectionOfficerDetailPage> Pages)
{
    public DeploymentNarrationPortraitRoute? StandingPortrait { get; init; }
    public string CourtesyName { get; init; } = "";
    public IReadOnlyList<CollectionOfficerDetailAction> Actions { get; init; } = [];
    public UniqueTacticVoiceSpeaker? VoiceSpeaker { get; init; }
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = [];
}
