using System.Buffers.Binary;

namespace San14.Data;

public sealed record TerrainGbufferColorMaskCurrent(long FrameNumber,string ViewIdentity,
    string ContextOwnerIdentity,uint CurrentContextFlags3E4,uint CurrentContextFlags3F0,
    int ActualBoundColorCount,uint SelectedLightF24Bits,string SelectedLightOwnerIdentity,
    string CurrentProducerProvenance,bool? CurrentDenormalsAreZero=null);

public sealed class TerrainGbufferColorMaskRecipe
{
    internal TerrainGbufferColorMaskRecipe(TerrainGbufferColorMaskSource source,
        TerrainGbufferColorMaskCurrent current,bool disableRt4)
    { Source=source; Current=current; DisableRt4=disableRt4; }
    public TerrainGbufferColorMaskSource Source { get; }
    public TerrainGbufferColorMaskCurrent Current { get; }
    public bool DisableRt4 { get; }
    // D712F0 high target bit drives RGB7 AND alpha1; off is RGBA0.
    public uint PackedColorWriteMasks => DisableRt4?0x0FFFFu:0xFFFFFu;
    public byte NativeTargetEnableBits => DisableRt4?(byte)0x0F:(byte)0x1F;
    public ulong NativeD712F0Flags => (ulong)NativeTargetEnableBits<<56;
    public long FrameNumber => Current.FrameNumber;
    public string ViewIdentity => Current.ViewIdentity;
    public string SourceProvenance => Source.SourceProvenance+";"+Current.CurrentProducerProvenance+
        ";D2D94B..D2D99E-selected-current-Q/full-L-F24;D712F0-RGB-and-alpha-together;not-GPU-receipt";
}

/// <summary>Exact F8 ordinary Landscape five-MRT color writer. It reads the
/// current Q3E4/Q3F0 and the selected full LightData F24, rather than a G1 draw
/// group or a guessed source scalar default. Float comparison follows the
/// completed native COMISS/JB flags, including unordered and signed zero.</summary>
public sealed class TerrainGbufferColorMaskSource
{
    private readonly TerrainGbufferSourceParameters parameters;
    private bool retained=true;
    private TerrainGbufferColorMaskSource(TerrainGbufferSourceParameters source)
    { parameters=source; }
    public string SourceProvenance => parameters.SourceProvenance+";ordinary-F8-five-MRT-mask-source";
    public static TerrainGbufferColorMaskSource ReadSource(GameResourceRepository repository,
        string locale,LoadedTerrainGbiMapResources actualMaterial)
        =>new(TerrainGbufferSourceParameters.ReadSourceDefaults(repository,locale,actualMaterial));

    public TerrainGbufferColorMaskRecipe PrepareCurrent(TerrainGbufferColorMaskCurrent current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if(!retained)throw new InvalidDataException("Terrain mask writer scope was replaced or extended.");
        _=parameters.PrepareCliffInfo(); // Check the retained actual writer scope; source import was already verified.
        if(current.FrameNumber<1 || current.ActualBoundColorCount!=5 ||
            string.IsNullOrWhiteSpace(current.ViewIdentity) || string.IsNullOrWhiteSpace(current.ContextOwnerIdentity) ||
            string.IsNullOrWhiteSpace(current.SelectedLightOwnerIdentity) || string.IsNullOrWhiteSpace(current.CurrentProducerProvenance))
            throw new InvalidDataException("F8 mask requires the actual same-frame selected Q/LightData and five bound source MRTs.");
        bool disable=false;
        // Native short circuits before the float instruction at D2D97D.
        if((current.CurrentContextFlags3E4&3)==1 && (current.CurrentContextFlags3F0&0xA00000)==0)
        {
            uint bits=current.SelectedLightF24Bits, magnitude=bits&0x7FFFFFFF;
            bool nan=magnitude>0x7F800000;
            bool subnormal=magnitude is >0 and <0x00800000;
            if(subnormal && current.CurrentDenormalsAreZero is null)
                throw new InvalidDataException("A subnormal F24 requires the actual current COMISS denormal mode.");
            // COMISS(0,L): JB sees CF=1 for 0<L or unordered. NaN takes
            // the skip branch. +/-zero is equal and reaches AND EDX,~0x10.
            bool treatedAsZero=magnitude==0 || (subnormal&&current.CurrentDenormalsAreZero==true);
            disable=!nan && (treatedAsZero || (bits&0x80000000)!=0);
        }
        return new(this,current,disable);
    }
    public TerrainGbufferColorMaskRecipe PrepareCurrentFullLightData(long frameNumber,string viewIdentity,
        string contextOwnerIdentity,uint currentContextFlags3E4,uint currentContextFlags3F0,
        int actualBoundColorCount,ReadOnlySpan<byte> actualSelectedFullLightData,
        string selectedLightOwnerIdentity,string currentProducerProvenance,bool? currentDenormalsAreZero=null)
    {
        if(actualSelectedFullLightData.Length!=MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("F8 must read F24 from the selected complete10C0 source LightData.");
        return PrepareCurrent(new(frameNumber,viewIdentity,contextOwnerIdentity,currentContextFlags3E4,currentContextFlags3F0,
            actualBoundColorCount,BinaryPrimitives.ReadUInt32LittleEndian(actualSelectedFullLightData.Slice(0xF24,4)),
            selectedLightOwnerIdentity,currentProducerProvenance,currentDenormalsAreZero));
    }
    public void MarkImportedEditedOrUnknownWriter(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);retained=false; }
    internal void RequireRetainedAcceptedScope()
    {if(!retained||!parameters.RetainsOwnedConstructorCliffInfo)throw new InvalidDataException("Accepted Terrain source scope has an imported or unknown current writer.");}
}
