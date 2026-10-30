namespace San14.Data;

/// <summary>The immutable writes made by the loaded scenario's one-time
/// formation/trait/tactic normalization. Each operation still owns its current
/// fields and lists; this object contains no current world or cursor.</summary>
internal sealed class InitialPersonSkillNormalization
{
    private readonly record struct FormationWrite(int Receiver, uint? Value);
    private readonly record struct TraitWrite(int Receiver, int Index, ushort? Value);
    private readonly record struct MaskWrite(int Receiver, ushort? Value);
    private readonly record struct TacticWrite(int Receiver, int Index, byte? Value);
    private readonly FormationWrite[] formations;
    private readonly TraitWrite[] traits, extras;
    private readonly MaskWrite[] masks;
    private readonly TacticWrite[] tactics;
    internal PersonSkillInitialValues Values { get; }

    private InitialPersonSkillNormalization(FormationWrite[] formations, TraitWrite[] traits,
        MaskWrite[] masks, TraitWrite[] extras, TacticWrite[] tactics, PersonSkillInitialValues values)
    { this.formations=formations;this.traits=traits;this.masks=masks;this.extras=extras;this.tactics=tactics;Values=values; }

    internal static InitialPersonSkillNormalization Capture(LoadedScenario source, SourceWorldContext normalized)
    {
        var formations=new List<FormationWrite>();var traits=new List<TraitWrite>();
        var masks=new List<MaskWrite>();var extras=new List<TraitWrite>();var tactics=new List<TacticWrite>();
        foreach (var person in source.Snapshot.Characters)
        {
            if (person.SkillSource is not { } original) continue;
            int physical=person.Slot;
            uint? formation=normalized.ReadPersonFormationMask164(physical);
            if (formation!=original.RawFormationMask164) formations.Add(new(physical,formation));
            ushort? mask=normalized.ReadPersonTraitMask1BC(physical);
            if (mask!=0) masks.Add(new(physical,mask));
            for (int index=0;index<9;index++)
            {
                ushort? trait=normalized.ReadPersonTraitSlot150(physical,index);
                if (trait!=original.RawPersonalitySlots150[index]) traits.Add(new(physical,index,trait));
                ushort? extra=normalized.ReadPersonTraitExtra1BE(physical,index);
                if (extra!=0) extras.Add(new(physical,index,extra));
            }
            for (int index=0;index<10;index++)
            {
                byte? tactic=normalized.ReadPersonTacticSlot168(physical,index);
                if (tactic!=original.RawTacticSlots168[index]) tactics.Add(new(physical,index,tactic));
            }
        }
        return new(formations.ToArray(),traits.ToArray(),masks.ToArray(),extras.ToArray(),tactics.ToArray(),
            PersonSkillInitialValues.Capture(normalized,source));
    }

    internal void Apply(SourceWorldContext current)
    {
        foreach (var write in formations) current.SetPersonFormationMask164(write.Receiver,write.Value);
        foreach (var write in traits) current.SetPersonTraitSlot150(write.Receiver,write.Index,write.Value);
        foreach (var write in masks) current.SetPersonTraitMask1BC(write.Receiver,write.Value);
        foreach (var write in extras) current.SetPersonTraitExtra1BE(write.Receiver,write.Index,write.Value);
        foreach (var write in tactics) current.SetPersonTacticSlot168(write.Receiver,write.Index,write.Value);
    }
}
