namespace San14.Data;

/// <summary>The source1158160 two arrays own distinct wrapper pointers for
/// physical85/86 mip0..mipCount-2. An alias copies one of these pointers;
/// dynamic RT setters continue to use the full physical wrapper plus a mip.
/// This CPU directory provides no shader, view, pixels or device authority.</summary>
public sealed class OriginalMapPostEffect3EyeCloneWrapperDirectory
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly Dictionary<int,OriginalMapPostEffect3ResolvedTextureParameter> full=[];
    private readonly Dictionary<(int Physical,int Mip),Entry> entries=[];
    private OriginalMapPostEffect3EyeCloneWrapperDirectory(LoadedOriginalMapPostEffect3EyeAdaptation2Source source,
        OriginalMapPostEffect3EyeAdaptation2Frame frame,OriginalMapPostEffect3TextureParameterState table)
    {
        Source=source;Frame=frame;this.table=table;
        foreach(int physical in new[]{85,86})
        {
            var pointer=table.Require(physical);
            if(pointer.Reference.PhysicalAllocationParameterIndex!=physical||pointer.Reference.HasSourceSelectedMipView)
                throw new InvalidDataException("Eye clone array requires its actual full physical85/86 pointer.");
            full.Add(physical,pointer);
            var plan=source.ReadOwnedBloomViewPlan(frame.Allocation,physical==86);
            foreach(int mip in plan.ClonedWrapperMipLevels)
                entries.Add((physical,mip),new(this,pointer,plan,mip));
        }
    }
    public LoadedOriginalMapPostEffect3EyeAdaptation2Source Source{get;}
    public OriginalMapPostEffect3EyeAdaptation2Frame Frame{get;}
    public IReadOnlyList<Entry> Entries=>Array.AsReadOnly(entries.Values.OrderBy(e=>e.PhysicalParameterIndex).ThenBy(e=>e.NativeMipLevel).ToArray());
    public bool ProvesGpuViewCreation=>false;
    public bool GrantsPixelReadiness=>false;
    public string SourceEvidence=>Source.SourceEvidence+"-clone-distinct-pointer-arrays;(i,i)-single-mip-SRV/RTV"+
        "native-full-wrapper40/42-retained;arrays85/86-exclude-last-physical-mip;CPU-pointer-recipe-only";
    public void RequireCurrent()
    {
        Frame.RequireCurrent();
        if(!ReferenceEquals(table.Allocation,Frame.Allocation)||table.FrameNumber!=Frame.FrameNumber||
            table.ContextOwnerIdentity!=Frame.OwnerIdentity||table.SourceSha256!=Frame.State.SourceSha256)
            throw new InvalidDataException("Eye clone directory belongs to another current source frame/table.");
        foreach(var pointer in full.Values)table.ValidateCurrent(pointer);
    }
    public void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actual)
    {if(!ReferenceEquals(actual,table))throw new InvalidDataException("Eye clone arrays belong to another actual pointer table.");RequireCurrent();}
    public Entry RequireEntry(int physical,int mip)
    {
        RequireCurrent();return entries.TryGetValue((physical,mip),out var entry)?entry:
            throw new InvalidDataException("Eye source clone array excludes this physical texture/mip.");
    }
    public Entry ResolveCurrentAlias(int logicalParameter)
    {
        RequireCurrent();var pointer=table.Require(logicalParameter);var reference=pointer.Reference;
        if(reference.PhysicalAllocationParameterIndex is not(85 or 86)||reference.NativeFirstMipLevel is not{} first||
            reference.NativeMipLevelCount!=1)
            throw new InvalidDataException("Eye alias is not a source single-mip clone pointer.");
        var entry=RequireEntry(reference.PhysicalAllocationParameterIndex.Value,checked((int)first));
        if(reference.InstanceIndex!=entry.InstanceIndex||
            reference.ProducerOwnerIdentity!=entry.FullPointer.Reference.ProducerOwnerIdentity+"/source-cloned-mip"+first)
            throw new InvalidDataException("Eye copied clone pointer changed its source full physical owner/side.");
        return entry;
    }
    public Entry RequireDynamicSource(OriginalMapPostEffect3EyeBeforeBindings before)
    {
        RequireBefore(before);
        if(!before.RequiresDynamicPixelTextureBinding||before.SourceTextureParameterIndex is not{} physical||before.SourceMipLevel is not{} mip)
            throw new InvalidDataException("This Eye Before does not write a dynamic array-clone PS t0 pointer.");
        return RequireEntry(physical,mip);
    }
    public Entry RequireTargetView(OriginalMapPostEffect3EyeBeforeBindings before)
    {
        RequireBefore(before);
        return RequireEntry(before.TargetTextureParameterIndex!.Value,before.TargetMipLevel!.Value);
    }
    private void RequireBefore(OriginalMapPostEffect3EyeBeforeBindings before)
    {
        RequireCurrent();
        //This exact shader/source validator checks original cached ordinal,
        //selected physical/mip order, CB20 writer and source three vertices.
        _=OriginalMapPostEffect3EyeGraphicsWriteCoverage.Prepare(Frame.Allocation,Frame,before);
    }
    public static OriginalMapPostEffect3EyeCloneWrapperDirectory Prepare(LoadedOriginalMapPostEffect3EyeAdaptation2Source source,
        OriginalMapPostEffect3EyeAdaptation2Frame actualFrame,OriginalMapPostEffect3TextureParameterState actualTable)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualFrame);ArgumentNullException.ThrowIfNull(actualTable);
        var result=new OriginalMapPostEffect3EyeCloneWrapperDirectory(source,actualFrame,actualTable);
        result.RequireCurrent();return result;
    }
    public sealed class Entry
    {
        private readonly OriginalMapPostEffect3EyeCloneWrapperDirectory owner;
        internal Entry(OriginalMapPostEffect3EyeCloneWrapperDirectory owner,OriginalMapPostEffect3ResolvedTextureParameter pointer,
            OriginalMapPostEffect3EyeBloomViewPlan plan,int mip)
            =>(this.owner,FullPointer,Plan,NativeMipLevel)=(owner,pointer,plan,mip);
        public OriginalMapPostEffect3ResolvedTextureParameter FullPointer{get;}
        public OriginalMapPostEffect3EyeBloomViewPlan Plan{get;}
        public int PhysicalParameterIndex=>Plan.PhysicalTextureParameterIndex;
        public int InstanceIndex=>FullPointer.Reference.InstanceIndex;
        public int NativeMipLevel{get;}
        public int NativeMipLevelCount=>1;
        public int NativeWrapperWidth40=>Plan.NativeWidth40;
        public int NativeWrapperHeight42=>Plan.NativeHeight42;
        public string SourceEvidence=>owner.SourceEvidence+$";physical{PhysicalParameterIndex}/side{InstanceIndex}/clone{NativeMipLevel};source-wrapper-identity-distinct";
        public void RequireCurrent()=>owner.RequireCurrent();
    }
}
