using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDirectionalLightResourceBinding(uint SourceObjectKey, ulong DataHandle,
    string OwnerProvenance, bool CpuDataReady, uint InterfaceClassId);
public sealed record OriginalDirectionalLightConstructionInputs(uint ContextFlags3E4,
    string RetainedTemplateProvenance, IReadOnlyList<OriginalDirectionalLightResourceBinding> ResourceBindings);
public sealed record OriginalDirectionalLightConstructorMetadata(uint SourceObjectId, uint Flags30,
    uint MaximumPrimitiveCount, uint SourceGroupMask, uint Value3C, uint Value40, uint Value44,
    uint ProviderObjectKey, byte SourceF20);
public sealed record OriginalDirectionalLightByteRange(int Offset,int Length);
public sealed record OriginalDirectionalLightResourceSlot(int Slot,int LightDataOffset,uint DeclaredObjectKey,
    ulong EffectiveDataHandle,uint RequiredInterfaceClassId);

public sealed class OriginalDirectionalLightCurrentCameraPacket
{
    private readonly byte[] bytes,camera;
    internal OriginalDirectionalLightCurrentCameraPacket(OriginalDirectionalLightConstructionPacket source,
        ReadOnlySpan<byte> preparedCamera,OriginalCameraDirectionalLightDataPacket result)
    {Construction=source;camera=preparedCamera.ToArray();bytes=result.Bytes.ToArray();Applied=result.Applied;}
    public OriginalDirectionalLightConstructionPacket Construction { get; }
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public bool Applied { get; }
    public void ValidateCurrentSourceFrame()
    {
        Construction.ValidateSourceConstruction();
        var rebuilt=OriginalDirectionalLightConstruction.PrepareCamera(Construction,camera);
        if(rebuilt.Applied!=Applied||!rebuilt.Bytes.AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Directional current Camera frame differs from its owned constructor and Camera generation.");
    }
}

/// <summary>Independently owned complete source-constructor and null-Camera
/// v430 result. No original process pointer or HDRI template is consumed.</summary>
public sealed class OriginalDirectionalLightConstructionPacket
{
    private readonly byte[] seed, prepared, template;
    private readonly DokProperty[] properties;
    private readonly OriginalDirectionalLightConstructionInputs inputs;
    internal OriginalDirectionalLightConstructionPacket(uint objectId, string sourceUri, string sourceHash,
        DokProperty[] sourceProperties, ReadOnlySpan<byte> retained, OriginalDirectionalLightConstructionInputs input,
        byte[] constructor, byte[] loaderPrepared, OriginalDirectionalLightConstructorMetadata metadata,
        byte[] sourceWriteMask,uint[] declaredSlotKeys, OriginalDirectionalLightConstruction.Circuit circuit)
    {
        SourceObjectId=objectId; SourceUri=sourceUri; SourceSha256=sourceHash;
        properties=sourceProperties;template=retained.ToArray();inputs=input;
        seed=constructor.ToArray();prepared=loaderPrepared.ToArray();Metadata=metadata;SourceCircuit=circuit;
        ConstructorSha256=Convert.ToHexString(SHA256.HashData(seed));
        PreparedSha256=Convert.ToHexString(SHA256.HashData(prepared));
        ConstructorWrittenRanges=OriginalDirectionalLightConstruction.Ranges(sourceWriteMask,1);
        ConstructorRetainedRanges=OriginalDirectionalLightConstruction.Ranges(sourceWriteMask,0);
        ResourceSlots=Array.AsReadOnly(declaredSlotKeys.Select((key,slot)=>new OriginalDirectionalLightResourceSlot(slot,
            0xFE0+slot*8,key,BinaryPrimitives.ReadUInt64LittleEndian(prepared.AsSpan(0xFE0+slot*8,8)),
            slot==21?0x970DB365u:0xB672FAA2u)).ToArray());
    }
    public uint SourceObjectId { get; }
    public string SourceUri { get; }
    public string SourceSha256 { get; }
    public string ConstructorSha256 { get; }
    public string PreparedSha256 { get; }
    public OriginalDirectionalLightConstructorMetadata Metadata { get; }
    public ReadOnlyMemory<byte> ConstructorBytes => seed.ToArray();
    public ReadOnlyMemory<byte> Bytes => prepared.ToArray();
    public uint ContextFlags3E4 => inputs.ContextFlags3E4;
    public string RetainedTemplateProvenance => inputs.RetainedTemplateProvenance;
    public bool CurrentCameraApplied => false;
    public bool ProviderSelectionApplied => false;
    public IReadOnlyList<OriginalDirectionalLightByteRange> ConstructorWrittenRanges { get; }
    public IReadOnlyList<OriginalDirectionalLightByteRange> ConstructorRetainedRanges { get; }
    public IReadOnlyList<OriginalDirectionalLightResourceSlot> ResourceSlots { get; }
    // v430 classifies1F4/1F8, but CE4A00 propagates the two retained hemi W
    // words into130.W/140.W. Current mode6 Camera overwrites the96 fog bytes.
    public IReadOnlyList<OriginalDirectionalLightByteRange> PreparedTemplateDependentRanges { get; }=
        Array.AsReadOnly(new[]{new OriginalDirectionalLightByteRange(0x10C,4),new(0x11C,4),new(0x13C,4),new(0x14C,4),
            new(0x150,96),new(0xF21,3),new(0xF2C,4),new(0x10B8,8)});
    public IReadOnlyList<OriginalDirectionalLightResourceBinding> ResourceBindings => inputs.ResourceBindings;
    internal OriginalDirectionalLightConstruction.Circuit SourceCircuit { get; }
    /// <summary>Explicit source replay for diagnostics and fixtures. Ordinary
    /// callers use the constructor result without rebuilding it.</summary>
    public void ValidateSourceConstruction()
    {
        var result=OriginalDirectionalLightConstruction.Construct(SourceCircuit,properties,inputs,template);
        if(!result.Constructor.AsSpan().SequenceEqual(seed)||!result.Prepared.AsSpan().SequenceEqual(prepared))
            throw new InvalidDataException("Directional source constructor or prepared immutable snapshot changed.");
    }
}

/// <summary>Finite original CBFAB0/CB65D0 source-writing circuit. Factory
/// enumeration and B3E980 copy are replaced by their exact typed descriptor
/// rules. Supported source initialization/math/classification operations are
/// decoded from the installed executable once and interpreted in private buffers.
/// This never executes machine code or follows an original process address.</summary>
public static partial class OriginalDirectionalLightConstruction
{
    public const int LightDataBytes=0x10C0;
    private const ulong Image=0x140000000, Cpu=0x10000000000, Stack=0x30000000000, Aux=0x40000000000;
    internal sealed record Operand(int Kind,int Size,int Reg,bool Clear,ulong Imm,int Base,int Index,int Scale,long Disp);
    internal sealed record Instruction(int Rva,int Length,string Name,Operand[] Ops);
    internal sealed record Constant(ulong Address,string Hex);
    internal sealed record Field(int Ordinal,uint Hash,uint Type,int Count);
    internal sealed record Circuit(Instruction[] Instructions,Constant[] Constants,Field[] Fields)
    { internal IReadOnlyDictionary<int,Instruction> Program { get; }=Instructions.ToDictionary(i=>i.Rva); }

    public static OriginalDirectionalLightConstructionPacket Apply(OriginalMapSeasonDirectionalLightSourceSet source,
        uint sourceObjectId,ReadOnlySpan<byte> explicitComplete10C0,OriginalDirectionalLightConstructionInputs input)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(input);
        if(explicitComplete10C0.Length!=LightDataBytes||string.IsNullOrWhiteSpace(input.RetainedTemplateProvenance))
            throw new InvalidDataException("Directional construction needs a complete10C0 template with explicit provenance.");
        var checkedSource=OriginalMapSeasonDirectionalLightSources.Read(source.Database);
        if(!checkedSource.Roots.SelectMany(r=>r.Children.Prepend(r.Root)).Any(r=>r.ObjectId==sourceObjectId))
            throw new InvalidDataException("Directional construction needs an actual selected season root or ordered provider child.");
        var declaration=checkedSource.Database.Objects.Document.Objects.Single(o=>o.ObjectId==sourceObjectId);
        var properties=declaration.Properties.Select(p=>p with {RawValue=p.RawValue.ToArray()}).ToArray();
        var circuit=LoadCircuit(source);ValidateProperties(circuit,properties);
        var bindings=input.ResourceBindings?.ToArray()??throw new InvalidDataException("Explicit Directional resource lookup results required.");
        if(bindings.Any(b=>b is null||string.IsNullOrWhiteSpace(b.OwnerProvenance)||b.SourceObjectKey==0)||
            bindings.Select(b=>b.SourceObjectKey).Distinct().Count()!=bindings.Length)
            throw new InvalidDataException("Directional resource bindings need distinct source keys and ownership provenance.");
        foreach(var b in bindings)
            if((b.DataHandle==0&&b.CpuDataReady)||(b.DataHandle!=0&&!b.CpuDataReady))
                throw new InvalidDataException("Directional binding must explicitly identify unavailable/null or ready Data.");
        var ownedInput=input with {ResourceBindings=Array.AsReadOnly(bindings)};
        var result=Construct(circuit,properties,ownedInput,explicitComplete10C0);
        uint Word(int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(result.Cpu.AsSpan(offset,4));
        uint provider=BinaryPrimitives.ReadUInt32LittleEndian(properties[57].RawValue);
        var metadata=new OriginalDirectionalLightConstructorMetadata(sourceObjectId,Word(0x30),Word(0x34),Word(0x38),
            Word(0x3C),Word(0x40),Word(0x44),provider,result.Prepared[0xF20]);
        return new(sourceObjectId,checkedSource.Source.ResourceUri,checkedSource.Source.DecodedSha256,
            properties,explicitComplete10C0,ownedInput,result.Constructor,result.Prepared,metadata,result.SeedWriteMask,result.DeclaredSlotKeys,circuit);
    }

    public static OriginalDirectionalLightCurrentCameraPacket ApplyCurrentCamera(OriginalDirectionalLightConstructionPacket construction,
        ReadOnlySpan<byte> explicitPreparedCameraPrefix)
    {
        ArgumentNullException.ThrowIfNull(construction);
        return new(construction,explicitPreparedCameraPrefix,
            PrepareCamera(construction,explicitPreparedCameraPrefix));
    }
    internal static OriginalCameraDirectionalLightDataPacket PrepareCamera(OriginalDirectionalLightConstructionPacket construction,
        ReadOnlySpan<byte> camera)
    {
        byte[] light=construction.Bytes.ToArray();
        if(light[0x10B0]==2)
        {
            // CBC770 is a LightData consumer, independent of the originating
            // actor class. Its mode2/Camera3B8=1 branch writes onlyF1E=2.
            var result=OriginalMapSourceCreatedCameraLightData.ApplySourceMode2PreparedCamera(light,camera,construction.ContextFlags3E4);
            return new(result.Bytes,result.ContextApplied,false);
        }
        return OriginalCameraDirectionalLightData.Apply(light,camera,construction.ContextFlags3E4);
    }

    public static IReadOnlyList<uint> RequiredResourceKeys(OriginalMapSeasonDirectionalLightSourceSet source,uint sourceObjectId,uint contextFlags3E4)
        => RequiredResourceKeys(source,sourceObjectId,contextFlags3E4,[]);

    /// <summary>Enumerate conditional original lookups with the supplied known
    /// ready/unavailable results. Unspecified requests are unavailable only for
    /// this metadata pass. The no-results overload therefore includes fallback
    /// requests and does not describe a ready GPU binding generation.</summary>
    public static IReadOnlyList<uint> RequiredResourceKeys(OriginalMapSeasonDirectionalLightSourceSet source,uint sourceObjectId,uint contextFlags3E4,
        IReadOnlyList<OriginalDirectionalLightResourceBinding> knownLookupResults)
    {
        ArgumentNullException.ThrowIfNull(source);
        var checkedSource=OriginalMapSeasonDirectionalLightSources.Read(source.Database);
        var obj=checkedSource.Database.Objects.Document.Objects.Single(o=>o.ObjectId==sourceObjectId&&o.ClassId==OriginalDirectionalLightSource.SourceClass);
        var properties=obj.Properties.Select(p=>p with {RawValue=p.RawValue.ToArray()}).ToArray();
        var circuit=LoadCircuit(source);ValidateProperties(circuit,properties);
        var machine=new Machine(circuit,properties,new(contextFlags3E4,"metadata-only lookup request enumeration",knownLookupResults),new byte[LightDataBytes],true);
        machine.Run(0xCBFAB0);return Array.AsReadOnly(machine.LookupKeys.Order().ToArray());
    }
    internal static IReadOnlyList<OriginalDirectionalLightByteRange> Ranges(byte[] mask,byte value)
    {
        var rows=new List<OriginalDirectionalLightByteRange>();int i=0;
        while(i<mask.Length){if(mask[i]!=value){i++;continue;}int start=i;while(i<mask.Length&&mask[i]==value)i++;rows.Add(new(start,i-start));}
        return rows.AsReadOnly();
    }

    private static void ValidateProperties(Circuit circuit,DokProperty[] properties)
    {
        if(properties.Length!=561)throw new InvalidDataException("Directional constructor requires all561 source properties.");
        foreach(var field in circuit.Fields)
        {
            var p=properties[field.Ordinal];
            if(p.Index!=field.Ordinal||p.NameHash!=field.Hash||p.TypeCode!=field.Type||p.ElementCount<0||
                p.RawValue.Length!=(long)p.ElementSize*p.ElementCount)
                throw new InvalidDataException($"Directional source descriptor{field.Ordinal}/{field.Hash:X8} differs from its native factory.");
        }
        // This original optional v438 branch needs its own current field writer.
        if((properties[3].RawValue[0]&2)!=0)
            throw new NotSupportedException("Directional CPU30 optional v438 current writer needs its own explicit source consumer.");
    }
    internal sealed record ConstructionResult(byte[] Constructor,byte[] Prepared,byte[] Cpu,byte[] SeedWriteMask,uint[] DeclaredSlotKeys);
    internal static ConstructionResult Construct(Circuit circuit,DokProperty[] properties,OriginalDirectionalLightConstructionInputs input,ReadOnlySpan<byte> template)
    {
        var machine=new Machine(circuit,properties,input,template);
        machine.Run(0xCBFAB0);
        byte[] cpu=machine.Read(Cpu,0x21D0);
        var constructor=cpu.AsSpan(0x1110,LightDataBytes).ToArray();
        var prepared=cpu.AsSpan(0x50,LightDataBytes).ToArray();
        machine.TranslateDataHandles(constructor);machine.TranslateDataHandles(prepared);
        return new(constructor,prepared,cpu,machine.SeedWriteMask.ToArray(),machine.DeclaredSlotKeys());
    }

    private sealed partial class Machine
    {
        private readonly UInt128[] registers=new UInt128[32];
        private readonly byte[] cpu=new byte[0x21D0],stack=new byte[0x30000],aux=new byte[0x30000];
        private readonly Dictionary<ulong,byte> constants=new();
        private readonly Dictionary<ulong,OriginalDirectionalLightResourceBinding> dataOwners=new();
        private readonly DokProperty[] properties;
        private readonly OriginalDirectionalLightConstructionInputs input;
        private readonly HashSet<uint> lookups=new();
        private readonly bool enumerateLookups;
        private readonly IReadOnlyDictionary<int,Instruction> program;
        private readonly Circuit circuit;
        private readonly Func<ulong,int,byte[]>? sourceRead;
        private bool trackSeed;
        public byte[] SeedWriteMask { get; }=new byte[LightDataBytes];
        public IEnumerable<uint> LookupKeys=>lookups;
        public uint[] DeclaredSlotKeys()
        {
            const ulong rbp=Stack+0x20000-0x3CB8;
            uint[] keys=Enumerable.Range(0,21).Select(i=>(uint)Word(rbp+0x20D0+(ulong)i*4,4)).Append(
                BinaryPrimitives.ReadUInt32LittleEndian(properties[63].RawValue)).ToArray();
            return keys;
        }
        private bool zero,carry,parity;
        private int steps;
        public Machine(Circuit sourceCircuit,DokProperty[] source,OriginalDirectionalLightConstructionInputs settings,
            ReadOnlySpan<byte> template,bool metadataOnly=false,Func<ulong,int,byte[]>? readSource=null)
        {
            circuit=sourceCircuit;program=circuit.Program;sourceRead=readSource;
            properties=source;input=settings;enumerateLookups=metadataOnly;registers[4]=Stack+0x20000;
            foreach(var c in circuit.Constants)
            {byte[] raw=Convert.FromHexString(c.Hex);for(int i=0;i<raw.Length;i++)constants[c.Address+(ulong)i]=raw[i];}
            ulong context=Aux+0x300,asset=Aux+0x500,allocator=Aux+0x1200,allocVt=Aux+0x1300;
            PutWord(context+0x50,asset,8);PutWord(context+8,allocator,8);PutWord(asset+0x3E4,input.ContextFlags3E4,4);
            PutWord(allocator,allocVt,8);PutWord(allocVt+0x28,Image+0x4000000,8);
            Set(1,Aux+0x200,8,true);Set(2,0,8,true);Set(8,context,8,true);Set(9,Aux+0x1000,8,true);
            // The loader's entire explicit light template is placed before any
            // native initializer/property write. Unknown holes remain caller-owned.
            Write(Stack+0x20000-0x3CB8+0x1B0,template);
            trackSeed=true;
        }
        public byte[] Read(ulong address,int count)
        {
            var (buffer,offset)=Region(address,count);
            if(buffer is not null)return buffer.AsSpan(offset,count).ToArray();
            if(sourceRead is not null)return sourceRead(address,count);
            var result=new byte[count];
            for(int i=0;i<count;i++)
                if(!constants.TryGetValue(address+(ulong)i,out result[i]))
                    throw new NotSupportedException($"Directional circuit cannot follow an unknown address {address:X}/{count}.");
            return result;
        }
        private (byte[]? Buffer,int Offset) Region(ulong address,int count)
        {
            foreach(var r in new[]{(Cpu,cpu),(Stack,stack),(Aux,aux)})
                if(address>=r.Item1&&address-r.Item1+(ulong)count<=(ulong)r.Item2.Length)return(r.Item2,(int)(address-r.Item1));
            return(null,0);
        }
        private void Write(ulong address,ReadOnlySpan<byte> value)
        {
            var (buffer,offset)=Region(address,value.Length);
            if(buffer is null)throw new InvalidDataException("Directional circuit write escaped private constructor memory.");
            value.CopyTo(buffer.AsSpan(offset,value.Length));
            if(trackSeed)
            {
                const ulong light=Stack+0x20000-0x3CB8+0x1B0;
                ulong lo=Math.Max(address,light),hi=Math.Min(address+(ulong)value.Length,light+LightDataBytes);
                if(lo<hi)Array.Fill(SeedWriteMask,(byte)1,(int)(lo-light),(int)(hi-lo));
            }
        }
        private static UInt128 Mask(int bytes)=>bytes==16?UInt128.MaxValue:((UInt128)1<<(bytes*8))-1;
        private UInt128 Reg(int reg,int bytes=8)=>reg<0?0:registers[reg]&Mask(bytes);
        private void Set(int reg,UInt128 value,int bytes,bool clear)=>registers[reg]=clear||bytes==8||bytes==16?value&Mask(bytes):
            (registers[reg]&~Mask(bytes))|(value&Mask(bytes));
        private ulong Address(Operand p,Instruction i)=>unchecked((ulong)(p.Base==-2?Image+(ulong)(i.Rva+i.Length):(ulong)Reg(p.Base))+
            (ulong)Reg(p.Index)*(ulong)p.Scale+(ulong)p.Disp);
        private UInt128 Get(Operand p,Instruction i)
        {
            if(p.Kind==1)return Reg(p.Reg,p.Size);
            if(p.Kind==2)return (UInt128)p.Imm&Mask(p.Size);
            byte[] raw=Read(Address(p,i),p.Size);UInt128 result=0;
            for(int j=0;j<raw.Length;j++)result|=(UInt128)raw[j]<<(j*8);return result;
        }
        private void Put(Operand p,UInt128 value,Instruction i)
        {
            if(p.Kind==1){Set(p.Reg,value,p.Size,p.Clear);return;}
            byte[] bytes=new byte[p.Size];for(int j=0;j<bytes.Length;j++)bytes[j]=(byte)(value>>(j*8));Write(Address(p,i),bytes);
        }
        private ulong Word(ulong address,int size)=>size==4?BinaryPrimitives.ReadUInt32LittleEndian(Read(address,size)):
            BinaryPrimitives.ReadUInt64LittleEndian(Read(address,size));
        private void PutWord(ulong address,ulong value,int size)
        {byte[] raw=new byte[size];for(int j=0;j<size;j++)raw[j]=(byte)(value>>(j*8));Write(address,raw);}
        private void Push(ulong value){registers[4]-=8;PutWord((ulong)Reg(4),value,8);}
        private ulong Pop(){ulong value=Word((ulong)Reg(4),8);registers[4]+=8;return value;}
        private static float Float(UInt128 value)=>BitConverter.UInt32BitsToSingle((uint)value);
        private static uint Bits(float value)
        {if(!float.IsFinite(value))throw new InvalidDataException("Directional source circuit consumed nonfinite math.");return BitConverter.SingleToUInt32Bits(value);}
        private void PutFloat(Operand p,float value,Instruction i)=>Put(p,(Get(p,i)&~(UInt128)uint.MaxValue)|Bits(value),i);
        private bool Hook(int target)
        {
            if(target is 0xFFB100 or 0xFFB170)return true;
            if(target==0x101CD70)
            {Write((ulong)Reg(1),Enumerable.Repeat((byte)Reg(2),checked((int)Reg(8))).ToArray());Set(0,Reg(1),8,true);return true;}
            if(target==0x101C1A0)
            {Write((ulong)Reg(1),Read((ulong)Reg(2),checked((int)Reg(8))));Set(0,Reg(1),8,true);return true;}
            if(target==0x101FAD8)
            {Set(16,(registers[16]&~(UInt128)uint.MaxValue)|Bits(MathF.Sqrt(Float(registers[16]))),16,true);return true;}
            if(target==0x4000000)
            {if(Reg(2,4)!=0x21D0)throw new InvalidDataException("Directional allocation extent changed.");Set(0,Cpu,8,true);return true;}
            if(target==0xB3E980)
            {
                if(Reg(9,4)!=561)throw new InvalidDataException("Directional typed source-copy count changed.");
                ulong pointers=(ulong)Reg(2),sizes=(ulong)Reg(8);
                foreach(var f in circuit.Fields)
                {
                    var p=properties[f.Ordinal];ulong destination=Word(pointers+(ulong)f.Ordinal*8,8);
                    int maximum=(int)Word(sizes+(ulong)f.Ordinal*4,4);
                    // Native exact descriptor matching intentionally leaves an
                    // empty/different-count array at the initialized fallback.
                    if(destination!=0&&maximum>0&&p.TypeCode==f.Type&&p.ElementCount==f.Count)
                        Write(destination,p.RawValue.AsSpan(0,Math.Min(maximum,p.RawValue.Length)));
                }
                Set(0,561,4,true);return true;
            }
            if(target==0xB3B6F0)
            {
                uint key=(uint)Reg(8,4);lookups.Add(key);
                var binding=input.ResourceBindings.SingleOrDefault(b=>b.SourceObjectKey==key);
                if(binding is null&&enumerateLookups){Set(0,0,8,true);return true;}
                if(binding is null)throw new InvalidDataException($"Directional resource {key:X8} needs an explicit ready or unavailable/null lookup result.");
                if(binding.DataHandle==0){Set(0,0,8,true);return true;}
                ulong handle=Aux+0x14000+(ulong)dataOwners.Count*0x100,data=handle+0x40,factory=handle+0x80,vt=handle+0xA0;
                dataOwners[data]=binding;PutWord(handle+8,data,8);PutWord(data+0x18,factory,8);PutWord(data+0x20,handle+0xC0,8);
                PutWord(factory,vt,8);PutWord(vt+0x30,Image+0x4000010,8);PutWord(vt+0x38,Image+0x4000020,8);
                Set(0,handle,8,true);return true;
            }
            if(target==0xB34660){Set(0,Word((ulong)Reg(1)+8,8),8,true);return true;}
            if(target==0xB34D90)return true;
            if(target==0xB39B70)return true;
            if(target is 0xBD7D70 or 0xCB40D0)
            {
                ulong factory=(ulong)Reg(1);var owner=dataOwners.Single(p=>p.Key+0x40==factory).Value;
                uint required=target==0xBD7D70?0xB672FAA2u:0x970DB365u;
                Set(0,owner.InterfaceClassId==required?1u:0u,1,false);return true;
            }
            if(target==0x4000010)
            {ulong factory=(ulong)Reg(1);Set(0,dataOwners.Single(p=>p.Key+0x40==factory).Value.InterfaceClassId,4,true);return true;}
            if(target==0x4000020){Set(0,0,4,true);return true;}
            return false;
        }
        public void TranslateDataHandles(byte[] light)
        {
            //21 texture wrapper slots plus the separate1088 typed970DB365
            // reference written at CC4689/CC4692; no virtual pointer escapes.
            for(int slot=0;slot<22;slot++)
            {
                int offset=0xFE0+slot*8;ulong value=BinaryPrimitives.ReadUInt64LittleEndian(light.AsSpan(offset,8));
                if(value==0)continue;
                if(!dataOwners.TryGetValue(value,out var binding))throw new InvalidDataException("Directional resource slot lacks its explicit owned Data binding.");
                BinaryPrimitives.WriteUInt64LittleEndian(light.AsSpan(offset,8),binding.DataHandle);
            }
            if(!lookups.SetEquals(input.ResourceBindings.Select(b=>b.SourceObjectKey)))
                throw new InvalidDataException("Directional bindings include an unused/unsupported source key.");
        }
        public void Run(int entry)
        {
            int pc=entry;
            while(true)
            {
                if(++steps>100000)throw new InvalidDataException("Directional finite source circuit exceeded its instruction budget.");
                if(!program.TryGetValue(pc,out var i))throw new NotSupportedException($"Unsupported Directional circuit target {pc:X}.");
                var p=i.Ops;int next=pc+i.Length;UInt128 a,b,v;
                switch(i.Name)
                {
                    case "mov":case "movzx":case "movaps":case "movups":case "movdqa":case "movsd":Put(p[0],Get(p[1],i),i);break;
                    case "movss":v=Get(p[1],i);if(p[0].Kind==1&&p[1].Kind==1)v=(Get(p[0],i)&~(UInt128)uint.MaxValue)|(v&uint.MaxValue);Put(p[0],v,i);break;
                    case "lea":Put(p[0],Address(p[1],i),i);break;
                    case "xor":case "xorps":case "and":case "or":case "test":
                        a=Get(p[0],i);b=Get(p[1],i);v=i.Name.StartsWith("xor")?a^b:i.Name is "and" or "test"?a&b:a|b;
                        if(i.Name!="test")Put(p[0],v,i);if(i.Name!="xorps"){zero=v==0;carry=false;parity=false;}break;
                    case "add":case "sub":case "inc":case "neg":case "sbb":case "cmp":
                        a=Get(p[0],i);b=p.Length>1?Get(p[1],i):1;UInt128 mask=Mask(p[0].Size);
                        if(i.Name=="neg"){v=unchecked(0-a);carry=a!=0;}
                        else if(i.Name=="inc")v=unchecked(a+1);
                        else if(i.Name=="add"){v=unchecked(a+b);carry=v>mask;}
                        else {UInt128 borrow=i.Name=="sbb"&&carry?1u:0u;v=unchecked(a-b-borrow);carry=a<b+borrow;}
                        zero=(v&mask)==0;parity=false;if(i.Name!="cmp")Put(p[0],v,i);break;
                    case "shl":case "shr":a=Get(p[0],i);b=Get(p[1],i);v=i.Name=="shl"?a<<(int)b:a>>(int)b;Put(p[0],v,i);zero=v==0;break;
                    case "not":Put(p[0],~Get(p[0],i),i);break;
                    case "mulss":case "divss":case "addss":case "subss":
                        float av=Float(Get(p[0],i)),bv=Float(Get(p[1],i));
                        PutFloat(p[0],i.Name=="mulss"?av*bv:i.Name=="divss"?av/bv:i.Name=="addss"?av+bv:av-bv,i);break;
                    case "shufps":a=Get(p[0],i);b=Get(p[1],i);int bits=(int)Get(p[2],i);v=0;
                        for(int j=0;j<4;j++)v|=(((j<2?a:b)>>(32*((bits>>(2*j))&3)))&uint.MaxValue)<<(32*j);Put(p[0],v,i);break;
                    case "ucomiss":case "comiss":float af=Float(Get(p[0],i)),bf=Float(Get(p[1],i));
                        parity=float.IsNaN(af)||float.IsNaN(bf);carry=parity||af<bf;zero=parity||af==bf;break;
                    case "sete":Put(p[0],zero?1u:0u,i);break;
                    case "setae":Put(p[0],!carry?1u:0u,i);break;
                    case "cmovne":case "cmove":if(zero==(i.Name=="cmove"))Put(p[0],Get(p[1],i),i);break;
                    case "cmova":if(!carry&&!zero)Put(p[0],Get(p[1],i),i);break;
                    case "btr":Put(p[0],Get(p[0],i)&~((UInt128)1<<(int)Get(p[1],i)),i);break;
                    case "lock cmpxchg":v=Get(p[0],i);zero=v==Reg(0,4);if(zero)Put(p[0],Get(p[1],i),i);else Set(0,v,4,true);break;
                    case "push":Push((ulong)Get(p[0],i));break;
                    case "pop":Put(p[0],Pop(),i);break;
                    case "call":int target=checked((int)((ulong)Get(p[0],i)-Image));
                        if(!Hook(target)){Push(Image+(ulong)next);Run(target);if(Pop()!=Image+(ulong)next)throw new InvalidDataException("Directional circuit private return stack changed.");}break;
                    case "jmp":case "jne":case "je":case "jp":case "jb":case "jbe":case "ja":
                        bool take=i.Name=="jmp"||i.Name=="jne"&&!zero||i.Name=="je"&&zero||i.Name=="jp"&&parity||i.Name=="jb"&&carry||i.Name=="jbe"&&(carry||zero)||i.Name=="ja"&&!carry&&!zero;
                        if(take)next=checked((int)((ulong)Get(p[0],i)-Image));break;
                    case "ret":return;
                    case "nop":break;
                    default:throw new NotSupportedException("Unsupported Directional source-writing operation "+i.Name);
                }
                pc=next;
            }
        }
    }
}
