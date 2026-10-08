using San14.Data;

/// <summary>Actual owned World220 entries and source-created World228 mapping.
/// This construction chooses cleared owned allocation. Declaration default IDs
/// remain declarations; only a reached setter writes the current material Data.
/// The retained Data object's ready and parameter records have separate owners.</summary>
public sealed class OriginalNativeArmyWorldFlagMaterialBank<TMaterialData> where TMaterialData:class
{
    private readonly TMaterialData?[] current220;
    private readonly Dictionary<int,int> rawDrawToSlot;
    private bool alive=true;
    private OriginalNativeArmyWorldFlagMaterialBank(OriginalArmyContainerMaterialGroupSource actualSource)
    {
        Source=actualSource;
        current220=new TMaterialData?[actualSource.Draws.Count];
        rawDrawToSlot=actualSource.Draws.Select((row,index)=>(row.SourceRawDrawIndex,index)).ToDictionary(p=>p.SourceRawDrawIndex,p=>p.index);
    }
    public OriginalArmyContainerMaterialGroupSource Source {get;}
    public string SourceEvidence=>Source.SourceEvidence+
        ";actual-owned-cleared-220-allocation;228-source-pairordinal;DC1120-retained-Data-by-group;declared-defaults-not-assigned";
    public static OriginalNativeArmyWorldFlagMaterialBank<TMaterialData> CreateFreshClearedOwned(
        OriginalArmyContainerMaterialGroupSource actualSource)
    {ArgumentNullException.ThrowIfNull(actualSource);return new(actualSource);}

    /// <summary>DC1120: release the old per-draw Data reference and retain the
    /// new input for every matching228 ordinal. A null input really clears it.
    /// Managed strong references provide these owned retain/release semantics.</summary>
    public void AssignActualGroup(uint actualDeclaredGroupOrdinal,TMaterialData? actualMaterialData)
    {
        RequireCurrent();
        foreach(var row in Source.Draws)
            if(row.DeclaredContainerGroupOrdinal==actualDeclaredGroupOrdinal)
                current220[rawDrawToSlot[row.SourceRawDrawIndex]]=actualMaterialData;
    }

    /// <summary>Actual 1C8850 controller call: manager kind, force variant and
    /// the independent fifth-argument group ordinal keep their own domains.
    /// A failed row-variant gate leaves the bank untouched. The selected Data
    /// was loaded from row variant+1 before this reached call.</summary>
    public bool ApplyActualControllerAssignment(int actualManagerKind,int actualOwnedForceVariant,
        uint actualFifthArgumentGroupOrdinal,LoadedOriginalArmyFlagMaterialVariation actualSelectedRow,
        TMaterialData actualSelectedMaterialData)
    {
        RequireCurrent();ArgumentNullException.ThrowIfNull(actualSelectedRow);ArgumentNullException.ThrowIfNull(actualSelectedMaterialData);
        if(actualSelectedRow.ManagerRow.Category!=Source.Category.Category||
            actualSelectedRow.ManagerRow.ManagerKind!=actualManagerKind||
            actualSelectedRow.OwnedForceVariant!=actualOwnedForceVariant||
            actualSelectedRow.ManagerRow.FaceSlot!=actualFifthArgumentGroupOrdinal)
            throw new InvalidDataException("Army material controller selected another manager, source row or World group.");
        if(!actualSelectedRow.ControllerGateNonzero)return false;
        AssignActualGroup(actualFifthArgumentGroupOrdinal,actualSelectedMaterialData);return true;
    }
    public TMaterialData? ReadActualRawDraw(int actualRawDrawOrdinal)
    {
        RequireCurrent();
        return rawDrawToSlot.TryGetValue(actualRawDrawOrdinal,out int slot)?current220[slot]:
            throw new ArgumentOutOfRangeException(nameof(actualRawDrawOrdinal));
    }
    public void RequireCurrent()
    {if(!alive)throw new ObjectDisposedException(nameof(OriginalNativeArmyWorldFlagMaterialBank<TMaterialData>));}
    public void Retire()
    {alive=false;Array.Clear(current220);}
}
