using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>Explicit source-created saved owners for logistics gain and native
/// Base44 bytes. Queries never reopen the executable or recopy scenario morale.</summary>
public sealed class OriginalArmyMoraleParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public int LogisticsGain1A4BE8C { get; }
    public int NegativeAdjustmentScale1A4B010 { get; }
    private OriginalArmyMoraleParameterSeed(string language,SourceProvenance source,int gain,int adjustmentScale)
        =>(Language,Source,LogisticsGain1A4BE8C,NegativeAdjustmentScale1A4B010)=(language,source,gain,adjustmentScale);
    public static OriginalArmyMoraleParameterSeed LoadPinnedSeed(OriginalGameRepository repository,string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string expected=language switch{"SC"=>OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC"=>OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,_=>throw new InvalidDataException("Army morale source locale is unsupported.")};
        string name=$"SAN14CE_{language}.exe",path=Path.GetFullPath(Path.Combine(repository.SourceDirectory,name));
        byte[] exe=File.ReadAllBytes(path);if(Convert.ToHexString(SHA256.HashData(exe))!=expected)
            throw new InvalidDataException("Army morale image seed source differs.");
        using var pe=new PEReader(new MemoryStream(exe,false));
        if(pe.PEHeaders.PEHeader is not{Magic:PEMagic.PE32Plus,ImageBase:0x140000000UL}||
            pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Army morale image seed requires the pinned AMD64 PE32+ ABI.");
        int rva=language=="SC"?0x1A4BE8C:0x1A4AE5C;
        var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".data"&&rva>=s.VirtualAddress&&(long)rva+4<=(long)s.VirtualAddress+s.SizeOfRawData);
        int gain=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(section.PointerToRawData+rva-section.VirtualAddress,4));
        int scaleRva=language=="SC"?0x1A4B010:0x1A49FE0;
        var scaleSection=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".data"&&scaleRva>=s.VirtualAddress&&(long)scaleRva+4<=(long)s.VirtualAddress+s.SizeOfRawData);
        int scale=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(scaleSection.PointerToRawData+scaleRva-scaleSection.VirtualAddress,4));
        if(scale!=1)throw new InvalidDataException("Army morale adjustment scale differs from the inspected source.");
        return new(language,new(name,exe.Length,expected,expected){ResourceUri=GameResourceId.File(name).ToString(),PhysicalPath=path},gain,scale);
    }
    public GameState InstallFresh(GameState state,OriginalEventInitializationContext freshDefinitions,string revision)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(freshDefinitions);
        string adjustmentKey=OriginalTacticRemakeParameterSeed.Key(0x1A4B010);
        if(state.NativeArmySimulation?.Parameters is not { } parameters||state.NativeClock is not { UpdateOrdinal:0,UpdatesIntoPeriod:0 } clock||
            state.Units.Count!=0||parameters.Int32Values.Keys.Any(key=>key.StartsWith("native-base-morale44:",StringComparison.Ordinal))||
            parameters.Int32Values.ContainsKey(adjustmentKey)||parameters.Float32Values.ContainsKey(adjustmentKey)||
            parameters.Language!=Language||parameters.ExecutableSha256!=Source.Sha256||string.IsNullOrWhiteSpace(revision)||
            freshDefinitions.StaticSource is not { } definitionSource||string.IsNullOrWhiteSpace(definitionSource.ResourceUri)||
            state.ContentHashes.GetValueOrDefault(definitionSource.ResourceUri)!=definitionSource.Sha256||
            state.NativeArmySimulation.World.SamplerEpoch is { } epoch&&
                (epoch.NativeClockRevision!=clock.Revision||epoch.SourceScenarioUri!=definitionSource.ResourceUri||epoch.SourceScenarioSha256!=definitionSource.Sha256))
            throw new InvalidDataException("Fresh Army morale owners require the same source scenario/clock and matching initial parameter allocation.");
        var values=new Dictionary<string,int>(parameters.Int32Values);string gainKey=OriginalTacticRemakeParameterSeed.Key(0x1A4BE8C);
        values.TryAdd(gainKey,LogisticsGain1A4BE8C);
        values.Add(adjustmentKey,NegativeAdjustmentScale1A4B010);
        for(int slot=0;slot<52;slot++)Copy(freshDefinitions.ReadCityTableReference(slot));
        for(int slot=0;slot<11;slot++)Copy(freshDefinitions.ReadGateTableReference(slot));
        void Copy(OriginalBaseReference? reference)
        {
            if(reference?.PhysicalSlot is null)return;
            if(freshDefinitions.ReadBaseMorale44(reference) is byte value)values.TryAdd(OriginalArmyMoraleSource.BaseMorale44Key(reference),value);
        }
        return state with{NativeArmySimulation=state.NativeArmySimulation with{
            Parameters=parameters with{Revision=revision,Int32Values=values}}};
    }
}
