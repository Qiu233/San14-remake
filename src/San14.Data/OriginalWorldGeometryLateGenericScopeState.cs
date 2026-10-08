using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Values left unwritten by 970570/97A580 but read by 97D430.
/// These are retained storage inputs, rather than invented constructor defaults.</summary>
public sealed record OriginalWorldGeometryLateGenericRetainedScalars(
    Vector4 Ambient78D0, Vector4 Fog78D150, Vector4 Fog78D1D0,
    Vector4 Clip80D0, Vector4 Clip80D150,
    int Count88A50, int Count88AD0, int Count88B50, int Count88BD0);

/// <summary>Actual resource-owner loan and frontend handle. Clearing a loan
/// does not dispose its resource owner.</summary>
public sealed record OriginalWorldGeometryLateGenericResourceLoan(object Owner, ulong FrontendHandle);
public readonly record struct OriginalWorldGeometryLateGenericFrontendCommand(uint Code, ulong Handle);

/// <summary>Mutable CPU parameter ownership for the normal node58 zero-count
/// scope, including the six source resource-clear setters. No source import,
/// fingerprints, shader submission or frame-number special cases occur here.</summary>
public sealed class OriginalWorldGeometryLateGenericScopeState
{
    private readonly Dictionary<int, byte[]> groups;
    private readonly OriginalWorldGeometryLateGenericResourceLoan?[] loans = new OriginalWorldGeometryLateGenericResourceLoan?[6];
    private readonly ulong[] frontendHandles = new ulong[6];
    private readonly bool[] resourcePending = new bool[6];
    private readonly List<OriginalWorldGeometryLateGenericFrontendCommand> commands = [];
    private readonly List<byte[]> additionalGroups = [];
    private readonly byte[] rawFog = new byte[96];
    private Matrix4x4 cameraMatrix68 = Matrix4x4.Identity;
    private Matrix4x4 transposedCameraC062C0 = Matrix4x4.Identity;
    private ushort[]? shaderResourceWords;
    private OriginalWorldGeometryLateGenericDescriptor? parameterDescriptorC8;
    private bool legacyDescriptorWordsOnly;

    private OriginalWorldGeometryLateGenericScopeState(Dictionary<int,byte[]> actualGroups)
        => groups = actualGroups;

    public uint WordD0 { get; set; }
    public uint WordD4 { get; private set; }
    public uint WordD8 { get; set; }
    public int CountF0 { get; set; }
    public int CountF4 { get; set; }
    public int CountF8 { get; set; }
    public int CountFC { get; set; }
    public bool FogEnabled120 { get; set; }
    public byte Pending121 { get; private set; }
    public int FogMode12C { get; set; }
    public int FogProfile130 { get; set; }
    public ulong Group88ResourceMask { get; set; }
    public Matrix4x4 CurrentCameraMatrix68 => cameraMatrix68;
    public Matrix4x4 CurrentTransposedCameraC062C0 => transposedCameraC062C0;
    public ReadOnlyMemory<byte> CurrentFog140 => rawFog;
    public IReadOnlyList<OriginalWorldGeometryLateGenericFrontendCommand> FrontendCommands => commands.AsReadOnly();
    /// <summary>The real nullable parameter-layout owner at Generic+C8.
    /// Six legacy fixture words do not establish this owner identity.</summary>
    public OriginalWorldGeometryLateGenericDescriptor? CurrentParameterDescriptorC8
        =>legacyDescriptorWordsOnly?throw new InvalidOperationException("Legacy descriptor words do not supply an actual C8 owner."):parameterDescriptorC8;
    public const string FreshConstructorProvenance = "TC97A580/SC97B710;970570/971700;97A8F0..97A9A2;97AD7D..97B0D3";

    /// <summary>Apply the proven fresh constructor fields. The nine scalar
    /// storage inputs above are the small cache holes read by the normal scope.
    /// This new managed allocation supplies its own null C8 storage seed;
    ///97A580 does not explicitly initialize that field. An existing singleton
    /// must import its actual C8 owner instead of assuming this fresh seed.
    /// Full opaque byte images are not required.</summary>
    public static OriginalWorldGeometryLateGenericScopeState CreateFresh(
        OriginalWorldGeometryLateGenericRetainedScalars actualRetained)
    {
        ArgumentNullException.ThrowIfNull(actualRetained);
        int[] offsets = [0x68,0x70,0x78,0x80,0x88,0x90];
        var data = new Dictionary<int,byte[]>();
        for(int group=0;group<offsets.Length;group++) data.Add(offsets[group], CreateParameterGroup(Descriptors[group]));
        var owner = new OriginalWorldGeometryLateGenericScopeState(data) {
            WordD0=0,WordD4=0,WordD8=0x20,FogEnabled120=true,Pending121=0,FogMode12C=0,FogProfile130=1
        };
        Array.Fill(owner.resourcePending,true); //97A975..97A98D bytes123..128.
        owner.WriteRetainedVector(0x78,0,actualRetained.Ambient78D0);
        owner.WriteRetainedVector(0x78,1,actualRetained.Fog78D150);
        owner.WriteRetainedVector(0x78,2,actualRetained.Fog78D1D0);
        owner.WriteRetainedVector(0x80,0,actualRetained.Clip80D0);
        owner.WriteRetainedVector(0x80,1,actualRetained.Clip80D150);
        foreach(var item in new[]{(19,actualRetained.Count88A50),(20,actualRetained.Count88AD0),
            (21,actualRetained.Count88B50),(22,actualRetained.Count88BD0)}) I32(data[0x88],0xD0+item.Item1*0x80,item.Item2);
        //97AB59..97AB8C force seven contiguous parameter statuses to5.
        for(int i=3;i<10;i++) U16(data[0x78],0x82+i*0x80,5);
        //These pointer-backed entries have source-owned storage. The numeric
        //pointer is not exposed as an invented native address.
        foreach(var item in new[]{(0x68,0),(0x68,1),(0x68,2),(0x68,3)})
            U16(data[item.Item1],0x82+item.Item2*0x80,4);
        //Actual constructor literals used by this scope and D71980.
        owner.SetVector(0x88,30,new(0,0,3600,0));
        owner.SetVector(0x88,24,Vector4.One);
        owner.SetInt(0x88,3,0);
        owner.SetInt(0x70,6,0);
        foreach(var item in new[]{(0,Vector4.One),(1,Vector4.One),(2,new Vector4(0,0,0,1)),
            (3,new Vector4(0,0,0,1)),(4,new Vector4(0,0,1,1))}) owner.SetVector(0x70,item.Item1,item.Item2);
        //971710(88,35,C0+62C0,0x30) sets its array status4, count3.
        U16(data[0x88],0x82+35*0x80,4); U16(data[0x88],0x84+35*0x80,3);
        return owner;
    }

    /// <summary>Import current CPU parameter storage once, preserving cache
    /// statuses, dirty bits, other resource classes and modified controls.</summary>
    public static OriginalWorldGeometryLateGenericScopeState ImportCurrent(
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> actualGroups,
        Matrix4x4 actualCameraMatrix68, Matrix4x4 actualTransposedCameraC062C0,
        uint actualD0,uint actualD4,uint actualD8,int actualF0,int actualF4,int actualF8,int actualFC,
        bool actual120,byte actual121,int actual12C,int actual130,ulong actual88ResourceMask)
    {
        var copies = new Dictionary<int,byte[]>();
        int[] offsets=[0x68,0x70,0x78,0x80,0x88,0x90];
        for(int i=0;i<offsets.Length;i++) {
            if(!actualGroups.TryGetValue(offsets[i],out var bytes)||bytes.Length<0x70+Descriptors[i].Length*0x80)
                throw new InvalidDataException("Current Generic parameter group is absent or truncated.");
            copies.Add(offsets[i],bytes.ToArray());
        }
        return new(copies) {cameraMatrix68=actualCameraMatrix68,transposedCameraC062C0=actualTransposedCameraC062C0,
            WordD0=actualD0,WordD4=actualD4,WordD8=actualD8,CountF0=actualF0,CountF4=actualF4,
            CountF8=actualF8,CountFC=actualFC,FogEnabled120=actual120,Pending121=actual121,
            FogMode12C=actual12C,FogProfile130=actual130,Group88ResourceMask=actual88ResourceMask};
    }
    public ReadOnlyMemory<byte> CurrentParameterGroup(int genericOffset) => groups[genericOffset];
    public ulong DirtyMask(int genericOffset) => R64(groups[genericOffset],0);
    public Vector4 ReadVector(int genericOffset,int index) => Vector(groups[genericOffset],0xD0+index*0x80);
    public float ReadActualParameterTime()
    {
        Vector4 value=ReadVector(0x88,30);float product=value.Z*value.Y;return product+value.X;
    }
    public void SetActualParameterTime(float requested)
    {
        Vector4 current=ReadVector(0x88,30);
        float quotient=requested/current.Z,whole=MathF.Floor(quotient),product=whole*current.Z;
        SetVector(0x88,30,new(requested-product,whole,current.Z,0));
    }

    /// <summary>97BBA0->97D430; scope00..0C counts0, ambient30 zero,
    /// fog40..90 zero, A0 is the actual current View+10 Camera.</summary>
    public void EnterActualZeroCountScope58(ReadOnlySpan<byte> actualCameraPrefix)
    {
        if(actualCameraPrefix.Length<0x140)throw new InvalidDataException("Generic scope requires current Camera+10 prefix through far134.");
        ClearScopeArray(0,19,CountF0); ClearScopeArray(1,20,CountF4);
        ClearScopeArray(2,21,CountF8); ClearScopeArray(3,22,CountFC);
        Pending121=1;
        SetVector(0x78,0,Vector4.Zero);
        float near=RF(actualCameraPrefix,0x130),far=RF(actualCameraPrefix,0x134);
        float span=far-near, inverse=1/span, product=inverse*near;
        Vector4 clip=new(inverse,BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(product)^0x80000000),span,near);
        //97D510 retains R8=Camera from97D4C4;975F90 therefore reads
        //Camera's first float4. Only the following manual cache writer uses
        //the four stack scalars calculated above.
        SetVector(0x80,0,Vector(actualCameraPrefix,0));SetVector(0x80,1,clip);
        Matrix4x4 next=Matrix(actualCameraPrefix,0xF0);
        if(!Equal(cameraMatrix68,next)){cameraMatrix68=next;OrDirty(groups[0x68],2);}
        transposedCameraC062C0=Matrix4x4.Transpose(Matrix(actualCameraPrefix,0x70));
        MarkRange(groups[0x88],35,0,3); //97C070 unconditionally writes source matrix and marks array.
        Array.Clear(rawFog);
        SetVector(0x78,2,Vector4.Zero);
        //With source scope+50=0, fog activation is false in every control mode.
        //The normal fresh profile130=1 yields0 independently of mode12C.
        if(FogProfile130==1) SetVector(0x78,1,Vector4.Zero);
        else if(FogMode12C==1) SetVector(0x78,1,new(0,1,0,0));
        else if(FogMode12C is 4 or 7) throw new NotSupportedException("Edited Generic fog profile modes4/7 require their current Scat storage owner.");
        else SetVector(0x78,1,Vector4.Zero);
    }

    /// <summary>D71980; unchanged D4 returns without reading D8/cache or
    /// running97D8B0. Normal requested0 clears only the three lane cache.</summary>
    public void SetActualWordD4(uint requested)
    {
        if(WordD4==requested)return;
        if(requested!=0)throw new NotSupportedException("This normal node58 owner closes the requested0 D71980 branch.");
        WordD4=requested;SetInt(0x88,23,unchecked((int)WordD8));
        SetInlineInts(0x88,10,[0,0,0]); //97D8D4 ->97D9ED ->973C70(index10,start0,count3).
    }

    /// <summary>Import each current borrow/cache before invoking the six NULL
    /// setters. Null current shader is explicit; nonnull supplies actual six
    /// descriptor words20..2A, whose bit15 controls parameter preparation.</summary>
    public void SetActualResourceState(int resourceClass,OriginalWorldGeometryLateGenericResourceLoan? actualLoan,
        ulong actualFrontendHandle,bool actualPending)
    {
        if((uint)resourceClass>=6)throw new ArgumentOutOfRangeException(nameof(resourceClass));
        loans[resourceClass]=actualLoan;frontendHandles[resourceClass]=actualFrontendHandle;resourcePending[resourceClass]=actualPending;
    }
    public void SetActualShaderResourceWords(ReadOnlySpan<ushort> actualWords,bool actualShaderIsNull)
    {
        if(actualShaderIsNull){ImportCurrentDescriptor(null);return;}
        if(actualWords.Length!=6)throw new InvalidDataException("Current Generic shader requires six descriptor words.");
        shaderResourceWords=actualWords.ToArray();parameterDescriptorC8=null;legacyDescriptorWordsOnly=true;
    }
    /// <summary>Borrow the actual current descriptor once at initialization or
    /// at a current-owner boundary. Importing a seed changes no pending byte,
    /// resource loan, frontend handle or parameter mapping.</summary>
    public void ImportCurrentDescriptor(OriginalWorldGeometryLateGenericDescriptor? actualDescriptorC8)
    {
        parameterDescriptorC8=actualDescriptorC8;
        shaderResourceWords=actualDescriptorC8?.StageWords20To2A.ToArray();
        legacyDescriptorWordsOnly=false;
    }
    /// <summary>Accept only the C8/cache endpoint of an already completed
    ///984700 publication. Its9754D0/974C30 parameter mapping writes must have
    /// completed in the caller's real producer. This method does not perform
    /// those mapping writes. Rejected/skipped publication and same-owner
    /// early return preserve all state; a changed nonnull endpoint clears
    /// bytes123..128 exactly as the completed publisher did.</summary>
    public void AcceptActualCompletedDescriptorC8Endpoint(
        OriginalWorldGeometryLateGenericDescriptorPublication actualCompletedPublication)
    {
        ArgumentNullException.ThrowIfNull(actualCompletedPublication);
        if(!actualCompletedPublication.CallsGenericC8Publisher)return;
        var descriptor=actualCompletedPublication.Descriptor??
            throw new InvalidDataException("Completed cached C8 publication has no actual descriptor owner.");
        if(!legacyDescriptorWordsOnly&&parameterDescriptorC8 is {} current&&
            ReferenceEquals(current.ActualOwner,descriptor.ActualOwner))return;
        if(!actualCompletedPublication.ChangesGenericC8)return;
        ImportCurrentDescriptor(descriptor);Array.Fill(resourcePending,false);
    }
    /// <summary>Actual984700 NULL writer: retain all six stage loans, pending
    /// bytes, frontend caches and parameter groups; only C8 becomes null.</summary>
    public void ClearActualParameterDescriptorC8()=>ImportCurrentDescriptor(null);
    public void AddActualAdditionalParameterGroup(ReadOnlySpan<byte> actualGroup)
    {
        if(additionalGroups.Count==5)throw new InvalidOperationException("Generic98..B8 has five additional groups.");
        if(actualGroup.Length<0x70||actualGroup.Length<0x70+checked(R32(actualGroup,0x54)*0x80))
            throw new InvalidDataException("Additional Generic parameter group is truncated.");
        additionalGroups.Add(actualGroup.ToArray());
    }
    public OriginalWorldGeometryLateGenericResourceLoan? CurrentResourceLoan(int resourceClass)=>loans[resourceClass];
    public ulong CurrentFrontendHandle(int resourceClass)=>frontendHandles[resourceClass];
    public bool CurrentResourcePending(int resourceClass)=>resourcePending[resourceClass];
    public bool IsActualResourcePreparationSuppressed(int resourceClass)
        =>shaderResourceWords is not null&&(shaderResourceWords[resourceClass]&0x8000)==0;
    /// <summary>Import the endpoint of an already completed original stage
    /// setter. Descriptor suppression preserves bytes123..128; otherwise
    /// the leaf sets its byte0 before comparing the previous loan. Parameter
    /// writes of the completed draw remain owned by its source adapter.</summary>
    public void AcceptActualCompletedStageBinding(int resourceClass,
        OriginalWorldGeometryLateGenericResourceLoan? actualLoan,ulong actualFrontendHandle)
    {
        if((uint)resourceClass>=6)throw new ArgumentOutOfRangeException(nameof(resourceClass));
        if(!IsActualResourcePreparationSuppressed(resourceClass))resourcePending[resourceClass]=false;
        loans[resourceClass]=actualLoan;frontendHandles[resourceClass]=actualFrontendHandle;
    }
    public void ClearActualSixResourceLoans58()
    {
        foreach(int kind in new[]{0,2,3,4,5,1}) ClearActualResourceLoan(kind);
    }
    public void ClearActualResourceLoan(int kind)
    {
        if((uint)kind>=6)throw new ArgumentOutOfRangeException(nameof(kind));
        bool suppressed=IsActualResourcePreparationSuppressed(kind);
        if(!suppressed) {
            bool changed=resourcePending[kind]||frontendHandles[kind]!=(loans[kind]?.FrontendHandle??0);
            resourcePending[kind]=false;
            if(changed||loans[kind] is not null)foreach(int offset in new[]{0x68,0x70,0x78,0x80,0x88}) ClearGroupResource(groups[offset],kind,offset==0x88);
            foreach(byte[] group in additionalGroups)ClearGroupResource(group,kind,false);
        }
        loans[kind]=null;
        if(frontendHandles[kind]!=0) {
            commands.Add(new(kind switch{0=>0xDu,1=>0xEu,2=>0x10u,3=>0x11u,4=>0xFu,_=>0x12u},0));
            frontendHandles[kind]=0;
        }
    }

    private void ClearScopeArray(int index,int countIndex,int retainedCount)
    {
        byte[] array=groups[0x90];int at=0x70+index*0x80;
        //971710 null/count0 writes only these fields and clears corresponding
        //dirty bit; existing range86/88 remains untouched.
        U16(array,at+0x12,0);U16(array,at+0x14,Descriptors[5][index].Count);
        U64(array,at+0x60,0);I32(array,at+0x68,0);U64(array,at+0x70,0);
        U64(array,0,R64(array,0)&~(1UL<<index));
        int count=retainedCount;
        if(index<3&&WordD0==(uint)(index+1)&&count>0)count--;
        SetInt(0x88,countIndex,count);
    }
    private void ClearGroupResource(byte[] data,int kind,bool actual88)
    {
        int count=R32(data,0x54),header=kind==0?0x10:kind==1?0x18:0x20+(kind-2)*8;
        if(kind>=2&&!actual88&&R32(data,0x58+kind*4)==0){U64(data,header,0);return;}
        int ordinal=kind<2?0x1E+kind*2:0x4C+(kind-2)*2;
        for(int i=0;i<count;i++) {
            int at=0x70+i*0x80;if(unchecked((short)R16(data,at+ordinal))<0)continue;
            if(kind<2){I32(data,at+kind*4,0);I32(data,at+8+kind*4,0);U16(data,at+0x1A+kind*2,0);}
            else{I32(data,at+0x24+(kind-2)*4,0);I32(data,at+0x34+(kind-2)*4,0);
                U16(data,at+0x44+(kind-2)*2,0);U16(data,at+0x54,(ushort)(R16(data,at+0x54)&~(1<<(kind-2))));}
            bool active=R32(data,at)!=0||R32(data,at+4)!=0||R32(data,at+8)!=0||R32(data,at+0xC)!=0||R16(data,at+0x54)!=0;
            ulong mask=1UL<<i;U64(data,8,active?R64(data,8)|mask:R64(data,8)&~mask);
        }
        if(actual88)U64(data,8,R64(data,8)&Group88ResourceMask);
        U64(data,header,0);
    }
    private void WriteRetainedVector(int group,int index,Vector4 value)=>WriteVector(groups[group],0xD0+index*0x80,value);
    private void SetVector(int group,int index,Vector4 value)
    {
        byte[] data=groups[group];int at=0x70+index*0x80;
        if(R16(data,at+0x12)==1&&Equal(Vector(data,at+0x60),value))return;
        WriteVector(data,at+0x60,value);U16(data,at+0x12,1);OrDirty(data,1UL<<index);
    }
    private void SetInt(int group,int index,int value)
    {
        byte[] data=groups[group];int at=0x70+index*0x80;
        if(R16(data,at+0x12)==1&&R32(data,at+0x60)==value)return;
        I32(data,at+0x60,value);U16(data,at+0x12,1);OrDirty(data,1UL<<index);
    }
    private void SetInlineInts(int group,int index,ReadOnlySpan<int> values)
    {
        byte[] data=groups[group];int at=0x70+index*0x80;
        bool changed=R16(data,at+0x12)!=1;
        for(int i=0;i<values.Length;i++){changed|=R32(data,at+0x60+i*4)!=values[i];I32(data,at+0x60+i*4,values[i]);}
        U16(data,at+0x12,1);if(changed){OrDirty(data,1UL<<index);MarkRange(data,index,0,values.Length);}
    }
    private static void MarkRange(byte[] data,int index,int start,int count)
    {
        int at=0x70+index*0x80;
        int oldStart=R16(data,at+0x16),oldCount=R16(data,at+0x18);
        int end=Math.Max(start+count,oldStart+oldCount),begin=Math.Min(start,oldStart);
        U16(data,at+0x16,(ushort)begin);U16(data,at+0x18,unchecked((ushort)(end-begin)));OrDirty(data,1UL<<index);
    }
    private static byte[] CreateParameterGroup(Descriptor[] descriptors)
    {
        byte[] data=new byte[0x70+descriptors.Length*0x80];I32(data,0x50,1);I32(data,0x54,descriptors.Length);
        for(int i=0;i<descriptors.Length;i++) {
            var d=descriptors[i];int at=0x70+i*0x80;
            U16(data,at+0x10,d.Type);U16(data,at+0x12,(ushort)(d.Type==7||d.Count>4?0:1));U16(data,at+0x14,d.Count);
            for(int kind=0;kind<6;kind++) {
                int ordinal=kind<2?0x1E+kind*2:0x4C+(kind-2)*2;
                if((d.Flags&(1<<kind))!=0){U16(data,at+ordinal,(ushort)R32(data,0x58+kind*4));I32(data,0x58+kind*4,R32(data,0x58+kind*4)+1);}
                else U16(data,at+ordinal,0xFFFF);
            }
        }
        return data;
    }
    private readonly record struct Descriptor(byte Flags,byte Type,ushort Count);
    private static readonly Descriptor[][] Descriptors =
    [
        [new(1,4,1536),new(31,7,0),new(11,7,4),new(3,7,4),new(1,4,1536)],
        [new(3,4,0),new(3,4,0),new(3,4,0),new(3,4,0),new(3,4,0),new(3,4,0),new(3,6,0)],
        [new(35,4,0),new(51,4,0),new(35,4,0),new(51,4,24),new(35,4,16),new(35,4,32),new(35,4,24),new(35,4,16),new(35,4,8),new(34,4,6)],
        [new(63,4,0),new(3,4,0)],
        [new(1,7,0),new(1,4,3),new(1,6,3),new(1,5,0),new(1,4,2),new(3,1,0),new(3,4,3),new(2,4,21),new(2,4,0),new(2,4,0),new(2,6,3),new(1,1,0),new(1,6,0),new(1,4,1),new(1,4,0),new(1,4,0),new(1,4,0),new(1,4,0),new(2,4,0),new(3,5,0),new(3,5,0),new(3,5,0),new(3,5,0),new(2,5,0),new(2,4,0),new(51,4,4),new(2,4,0),new(2,4,0),new(2,4,0),new(2,4,12),new(0,4,0),new(2,4,0),new(2,4,0),new(2,4,4),new(3,4,2),new(35,4,3),new(2,1,0),new(2,4,8),new(2,1,0),new(2,4,4),new(2,4,4),new(2,5,0),new(3,4,0),new(2,4,0)],
        [new(0,4,16),new(0,4,32),new(0,4,48),new(0,4,24),new(0,4,6)]
    ];
    private static bool Equal(Vector4 a,Vector4 b)=>a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.W==b.W;
    private static bool Equal(Matrix4x4 a,Matrix4x4 b)=>Equal(new Vector4(a.M11,a.M12,a.M13,a.M14),new Vector4(b.M11,b.M12,b.M13,b.M14))&&
        Equal(new Vector4(a.M21,a.M22,a.M23,a.M24),new Vector4(b.M21,b.M22,b.M23,b.M24))&&Equal(new Vector4(a.M31,a.M32,a.M33,a.M34),new Vector4(b.M31,b.M32,b.M33,b.M34))&&Equal(new Vector4(a.M41,a.M42,a.M43,a.M44),new Vector4(b.M41,b.M42,b.M43,b.M44));
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> data,int at)=>new(RF(data,at),RF(data,at+4),RF(data,at+8),RF(data,at+12),RF(data,at+16),RF(data,at+20),RF(data,at+24),RF(data,at+28),RF(data,at+32),RF(data,at+36),RF(data,at+40),RF(data,at+44),RF(data,at+48),RF(data,at+52),RF(data,at+56),RF(data,at+60));
    private static Vector4 Vector(ReadOnlySpan<byte> data,int at)=>new(RF(data,at),RF(data,at+4),RF(data,at+8),RF(data,at+12));
    private static void WriteVector(byte[] data,int at,Vector4 v){F32(data,at,v.X);F32(data,at+4,v.Y);F32(data,at+8,v.Z);F32(data,at+12,v.W);}
    private static ushort R16(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(at,2));
    private static int R32(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at,4));
    private static ulong R64(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(at,8));
    private static float RF(ReadOnlySpan<byte> data,int at)=>BinaryPrimitives.ReadSingleLittleEndian(data.Slice(at,4));
    private static void U16(byte[] data,int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at,2),value);
    private static void I32(byte[] data,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at,4),value);
    private static void U64(byte[] data,int at,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(at,8),value);
    private static void F32(byte[] data,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(at,4),value);
    private static void OrDirty(byte[] data,ulong bits)=>U64(data,0,R64(data,0)|bits);
}
