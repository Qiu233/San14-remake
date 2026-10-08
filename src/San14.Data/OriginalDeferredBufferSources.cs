using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A producer's complete prepared bytes and selected native DXGI view.
/// Format and extent are source inputs. Empty bytes never request a substitute.</summary>
public sealed record OriginalDeferredPreparedBuffer(uint SourceBindPoint, uint NativeViewFormat,
    ReadOnlyMemory<byte> Bytes, string SourceResourceUri);

public sealed record OriginalDeferredBufferDeclaration(uint SourceBindPoint, string Name,
    G1ShaderConfigurationRecord Configuration, G1ShaderConfigurationParameter Slot,
    DxbcRdefBinding PixelBinding);

public sealed record LoadedOriginalDeferredBufferLayout(LoadedShaderContainer Shader,
    int CombinationIndex, IReadOnlyList<OriginalDeferredBufferDeclaration> Declarations);

public sealed record OriginalDeferredBufferSource(OriginalDeferredBufferDeclaration Declaration,
    uint NativeViewFormat, int TexelBytes, int TexelCount, ReadOnlyMemory<byte> Bytes,
    string SourceResourceUri);

/// <summary>Validated populated producer packets. The list can contain one
/// source group; missing groups remain external resources to the Deferred draw.</summary>
public sealed record LoadedOriginalDeferredBufferSources(LoadedOriginalDeferredBufferLayout Layout,
    IReadOnlyList<OriginalDeferredBufferSource> Buffers, int CellCount, int PointCount,
    int SpotCount, int LocalAmbientCount);

public enum OriginalDeferredBufferSourceAction { RequirePreparedSource, RetainBinding, BindNull }
public sealed record OriginalDeferredBufferSourceDecision(uint SourceBindPoint,
    OriginalDeferredBufferSourceAction Action);

/// <summary>Original PS1 typed-buffer consumers and explicit prepared source
/// uploads. Does not choose lights/probes, generate spatial membership, import
/// an active cache or turn native null resource pointers into zero buffers.</summary>
public static partial class OriginalDeferredBufferSources
{
    public const string PixelSourceSha256 = "B01248B54B5D09A98D850D4627520F78808EA14C1CF090F3C804753E78F1884E";

    private static readonly (uint Register, string Name, string Configuration, uint Hash, uint ReturnType)[] Slots =
    [
        (16,"tCllLightIndices","ClusterLightList",0xAB69006D,4),
        (17,"tCllLightPositions","ClusterLightList",0x6AF9A27C,5),
        (18,"tCllLightAttributes","ClusterLightList",0x5764F4BD,5),
        (19,"tCllLightExAttributes","ClusterLightList",0x56C4C954,4),
        (20,"tCllSptLightAttributes","ClusterLightList",0x689F3D34,5),
        (21,"tCllSptLightExAttributes","ClusterLightList",0x65BD2433,4),
        (23,"tLasData","LocalAmbSpecMaps",0xC356A050,5),
        (24,"tLasIndices","LocalAmbSpecMaps",0xA119EC14,4)
    ];

    public static LoadedOriginalDeferredBufferLayout ReadLayout(LoadedShaderContainer shader,
        int explicitCombination)
    {
        ArgumentNullException.ThrowIfNull(shader);
        var combination = shader.ReadG1ResourceBindings(explicitCombination).Combination;
        var pixels = combination.Stages.Where(s=>s.StageCode==0 && s.Entry is not null).ToArray();
        if(pixels.Length!=1 || Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(
            shader.Source.Bytes,pixels[0].Entry!.ProgramIndex)))!=PixelSourceSha256)
            throw Error("selected source combination does not contain exact original PS1");
        var definitions=shader.ReadResourceDefinitions(pixels[0].Entry!.ProgramIndex);
        var configurations=G1ShaderConfigurationReader.Read(shader.Source.Bytes,shader.Container);
        var rows=new List<OriginalDeferredBufferDeclaration>();
        foreach(var expected in Slots)
        {
            var configuration=configurations.Records.FirstOrDefault(r=>r.Name==expected.Configuration &&
                r.NameHash==(expected.Configuration=="ClusterLightList"?0x10BCD0CAu:0xC1BD0501u));
            var slot=configuration?.Find(expected.Hash);
            var bindings=definitions.Bindings.Where(b=>b.InputType==2 && b.Name.Text==expected.Name).ToArray();
            if(configuration is null || slot is null || slot.RawValue!=expected.Register || bindings.Length!=1 ||
                bindings[0].BindPoint!=slot.RawValue || bindings[0].BindCount!=1 || bindings[0].Dimension!=1 ||
                bindings[0].ReturnType!=expected.ReturnType)
                throw Error("source configuration slot differs from its exact typed-buffer consumer");
            rows.Add(new(expected.Register,expected.Name,configuration,slot,bindings[0]));
        }
        return new(shader,explicitCombination,rows.AsReadOnly());
    }

    /// <summary>Read the complete six buffers after their original producer has
    /// prepared them. Counts are the selected producer's counts, not shader CB
    /// directional-light counts. Allocation tails are copied without filling.</summary>
    public static LoadedOriginalDeferredBufferSources ReadPreparedCluster(LoadedOriginalDeferredBufferLayout layout,
        IReadOnlyList<OriginalDeferredPreparedBuffer> explicitPreparedBuffers,
        int sourceCellCount,int sourcePointCount,int sourceSpotCount)
    {
        ArgumentNullException.ThrowIfNull(layout);ArgumentNullException.ThrowIfNull(explicitPreparedBuffers);
        Counts(sourceCellCount,sourcePointCount,sourceSpotCount);
        var rows=Snapshot(layout,explicitPreparedBuffers,[16,17,18,19,20,21]);
        Need(rows[17],sourcePointCount,4);
        Need(rows[18],checked(sourcePointCount*3),3);Need(rows[19],checked(sourcePointCount*3),1);
        Need(rows[20],checked(sourceSpotCount*5),3);Need(rows[21],checked(sourceSpotCount*3),1);
        var indices=rows[16];IntegerScalar(indices);
        int header=checked(sourceCellCount*4);
        if(indices.TexelCount<header)throw Error("cluster index buffer lacks its four-texel cell headers");
        for(int cell=0;cell<sourceCellCount;cell++)
        {
            uint low=Index(indices,cell*4),high=Index(indices,cell*4+1);
            uint points=Index(indices,cell*4+2),spots=Index(indices,cell*4+3);
            if(low>ushort.MaxValue || high>ushort.MaxValue || points>ushort.MaxValue || spots>ushort.MaxValue)
                throw Error("cluster header differs from original ushort offset/count words");
            uint offset=low|(high<<16);
            ulong total=(ulong)points+spots;
            if(total==0)continue; // The source never loads the unused tail address.
            if(offset<(uint)header || (ulong)offset+total>(ulong)indices.TexelCount)
                throw Error("cluster cell references an absent source index range");
            for(uint i=0;i<points;i++)
                if(Index(indices,checked((int)(offset+i)))>=(uint)sourcePointCount)
                    throw Error("cluster point index is outside the selected source records");
            for(uint i=0;i<spots;i++)
                if(Index(indices,checked((int)(offset+points+i)))>=(uint)sourceSpotCount)
                    throw Error("cluster spot index is outside the selected source records");
        }
        return Packet(layout,rows,sourceCellCount,sourcePointCount,sourceSpotCount,0);
    }

    /// <summary>Three prepared float4 rows per local ambient probe, with the
    /// original two-scalar-texel cell headers (offset,count). The rows contain
    /// the producer's transform results; no inverse or scale is invented.</summary>
    public static LoadedOriginalDeferredBufferSources ReadPreparedLocalAmbient(LoadedOriginalDeferredBufferLayout layout,
        OriginalDeferredPreparedBuffer explicitData,OriginalDeferredPreparedBuffer explicitIndices,
        int sourceCellCount,int sourceProbeCount)
    {
        ArgumentNullException.ThrowIfNull(layout);ArgumentNullException.ThrowIfNull(explicitData);
        ArgumentNullException.ThrowIfNull(explicitIndices);Counts(sourceCellCount,sourceProbeCount);
        var rows=Snapshot(layout,[explicitData,explicitIndices],[23,24]);
        Need(rows[23],checked(sourceProbeCount*3),4);
        var indices=rows[24];IntegerScalar(indices);
        int header=checked(sourceCellCount*2);
        if(indices.TexelCount<header)throw Error("local ambient index buffer lacks its two-texel cell headers");
        for(int cell=0;cell<sourceCellCount;cell++)
        {
            uint offset=Index(indices,cell*2),count=Index(indices,cell*2+1);
            if(count==0)continue;
            if(offset<(uint)header || (ulong)offset+count>(ulong)indices.TexelCount)
                throw Error("local ambient cell references an absent source index range");
            for(uint i=0;i<count;i++)
                if(Index(indices,checked((int)(offset+i)))>=(uint)sourceProbeCount)
                    throw Error("local ambient index is outside the selected source probes");
        }
        return Packet(layout,rows,sourceCellCount,0,0,sourceProbeCount);
    }

    /// <summary>TC D728C0: t16 is supplied before the View gate. With no View,
    /// t17..21 retain their bindings; with a View, absent point/spot producers
    /// bind null to their respective globals. Null is never an upload recipe.</summary>
    public static IReadOnlyList<OriginalDeferredBufferSourceDecision> SelectClusterView(
        bool sourceViewPresent,bool sourcePointProducerPresent,bool sourceSpotProducerPresent)
    {
        var rows=new List<OriginalDeferredBufferSourceDecision>{new(16,OriginalDeferredBufferSourceAction.RequirePreparedSource)};
        foreach(uint slot in new uint[]{17,18,19,20,21})
            rows.Add(new(slot,!sourceViewPresent?OriginalDeferredBufferSourceAction.RetainBinding:
                (slot<20?sourcePointProducerPresent:sourceSpotProducerPresent)?
                OriginalDeferredBufferSourceAction.RequirePreparedSource:OriginalDeferredBufferSourceAction.BindNull));
        return rows.AsReadOnly();
    }

    /// <summary>Only the successful new LocalAmbSpecMaps data construction,
    /// before later setters: data+18/+20 are null (TC 9B7178/9B717C).</summary>
    public static IReadOnlyList<OriginalDeferredBufferSourceDecision> SuccessfulLocalAmbientDataConstruction()
        => Array.AsReadOnly(new[]{new OriginalDeferredBufferSourceDecision(23,OriginalDeferredBufferSourceAction.BindNull),
            new OriginalDeferredBufferSourceDecision(24,OriginalDeferredBufferSourceAction.BindNull)});

    /// <summary>TC D1B520's explicit global reset. This clears all six source
    /// pointers; it does not initialize a populated cluster or any live bytes.</summary>
    public static IReadOnlyList<OriginalDeferredBufferSourceDecision> ResetClusterSources()
        =>Array.AsReadOnly(new uint[]{16,17,18,19,20,21}.Select(register=>
            new OriginalDeferredBufferSourceDecision(register,OriginalDeferredBufferSourceAction.BindNull)).ToArray());

    public static int TexelByteSize(uint explicitNativeViewFormat) => explicitNativeViewFormat switch
    {
        2=>16,6=>12,10=>8,16=>8,34=>4,41=>4,42=>4,54=>2,57=>2,
        _=>throw Error("native typed-buffer view format is outside the explicit finite adapter")
    };

    private static Dictionary<uint,OriginalDeferredBufferSource> Snapshot(LoadedOriginalDeferredBufferLayout layout,
        IReadOnlyList<OriginalDeferredPreparedBuffer> supplied,uint[] registers)
    {
        if(supplied.Count!=registers.Length || supplied.Any(b=>b is null) ||
            supplied.Select(b=>b.SourceBindPoint).Distinct().Count()!=supplied.Count ||
            supplied.Any(b=>!registers.Contains(b.SourceBindPoint)))
            throw Error("prepared producer must supply its exact source register group once");
        var rows=new Dictionary<uint,OriginalDeferredBufferSource>();
        foreach(var input in supplied)
        {
            var declarations=layout.Declarations.Where(d=>d.SourceBindPoint==input.SourceBindPoint).ToArray();
            int texel=TexelByteSize(input.NativeViewFormat);
            if(declarations.Length!=1 || input.Bytes.IsEmpty || input.Bytes.Length%texel!=0 ||
                string.IsNullOrWhiteSpace(input.SourceResourceUri))
                throw Error("prepared source has no complete nonempty typed-buffer extent or provenance");
            var declaration=declarations[0];
            bool integer=input.NativeViewFormat is 42 or 57;
            if(integer!=(declaration.PixelBinding.ReturnType==4))
                throw Error("selected native source view differs from the original uint/float consumer");
            rows.Add(input.SourceBindPoint,new(declaration,input.NativeViewFormat,texel,input.Bytes.Length/texel,
                input.Bytes.ToArray(),input.SourceResourceUri));
        }
        return rows;
    }

    private static void Need(OriginalDeferredBufferSource source,int texelCount,int components)
    {
        int availableComponents=source.NativeViewFormat switch {2 or 10=>4,6=>3,16 or 34=>2,_=>1};
        if(source.TexelCount<texelCount || availableComponents<components)
            throw Error("source records lack their consumed texels/components");
    }
    private static void IntegerScalar(OriginalDeferredBufferSource source)
    {
        if(source.NativeViewFormat is not(42 or 57))throw Error("source index view must contain scalar uint texels");
    }
    private static uint Index(OriginalDeferredBufferSource source,int texel)
        => source.NativeViewFormat==57?BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.Span.Slice(checked(texel*2),2)):
            BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.Span.Slice(checked(texel*4),4));
    private static void Counts(params int[] values)
    {
        if(values.Any(v=>v<0)||values[0]<1)throw Error("selected source cell count must be positive and record counts nonnegative");
    }
    private static LoadedOriginalDeferredBufferSources Packet(LoadedOriginalDeferredBufferLayout layout,
        Dictionary<uint,OriginalDeferredBufferSource> rows,int cells,int points,int spots,int probes)
        =>new(layout,Array.AsReadOnly(rows.Values.OrderBy(b=>b.Declaration.SourceBindPoint).ToArray()),cells,points,spots,probes);
    private static InvalidDataException Error(string message)=>new("Original Deferred buffers: "+message+".");
}
