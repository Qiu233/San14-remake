namespace San14.Data;

/// <summary>Immutable formation, trait, activation and tactic values produced
/// by one loaded scenario's initialization. These are definition inputs, not
/// a mutable current or a world cache. Narrow transactions borrow only the
/// channels they already consumed.</summary>
internal sealed class PersonSkillInitialValues
{
    private readonly uint?[] formations;
    private readonly ushort?[] traits, masks, extras;
    private readonly byte?[] tactics;
    private readonly LoadedScenario source;

    private PersonSkillInitialValues(uint?[] formations, ushort?[] traits, ushort?[] masks,
        ushort?[] extras, byte?[] tactics, LoadedScenario source)
    { this.formations=formations;this.traits=traits;this.masks=masks;this.extras=extras;this.tactics=tactics;this.source=source; }

    internal bool BelongsTo(LoadedScenario loaded) => ReferenceEquals(source,loaded);

    internal uint? Formation(int receiver) => formations[receiver];
    internal ushort? Trait(int receiver,int index) => traits[receiver*9+index];
    internal ushort? Mask(int receiver) => masks[receiver];
    internal ushort? Extra(int receiver,int index) => extras[receiver*9+index];
    internal byte? Tactic(int receiver,int index) => tactics[receiver*10+index];

    internal static PersonSkillInitialValues Capture(SourceWorldContext current,LoadedScenario source)
    {
        int count=source.Snapshot.Characters.Count;
        var formations=new uint?[count];var traits=new ushort?[count*9];
        var masks=new ushort?[count];var extras=new ushort?[count*9];var tactics=new byte?[count*10];
        for(int physical=0;physical<count;physical++)
        {
            formations[physical]=current.ReadPersonFormationMask164(physical);
            masks[physical]=current.ReadPersonTraitMask1BC(physical);
            for(int index=0;index<9;index++)
            { traits[physical*9+index]=current.ReadPersonTraitSlot150(physical,index);extras[physical*9+index]=current.ReadPersonTraitExtra1BE(physical,index); }
            for(int index=0;index<10;index++)tactics[physical*10+index]=current.ReadPersonTacticSlot168(physical,index);
        }
        return new(formations,traits,masks,extras,tactics,source);
    }
}
