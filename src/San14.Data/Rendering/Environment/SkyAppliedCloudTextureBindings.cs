using San14.Data.Formats;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkyAppliedCloudTextureBinding(uint SourceBindPoint,string TextureName,uint SourceDimension,
    StaticTextureBinding SourceStaticBinding,uint NativeViewFormat,byte[] CompleteSamplerDescriptor,string SourceProvenance);

/// <summary>Current1193A90 binderfalse: source Noise0/1/2, Cirrus0 and
/// primary selected Skylight STA3/1/0. Native primary alias registration
/// encoded128..132 starts at CPU198 and supplies CPU918/930/948/960/978.
/// The actual selected pass0 RDEF verifies the consumed register/dimension/name set.
/// No previous texture binding, name guessing, fallback group or pixels.</summary>
public static class SkyAppliedCloudTextureBindings
{
    private sealed record Consumer(uint Slot,string Name,uint Dimension,int Group,int Entry);
    private static readonly ConditionalWeakTable<LoadedShaderContainer,
        ConcurrentDictionary<(int Combination,int Pixel),IReadOnlyList<Consumer>>> Directories=new();
    private static IReadOnlyList<Consumer> ReadDirectory(LoadedShaderContainer shader,int combination,int pixel)
    {
        var mappings=shader.ReadG1ResourceBindings(combination).Entries;
        (int Encoded,string Name,int Slot,int Offset)[] expected=[(128,"sBase",0,0x918),(129,"sCirrus",3,0x930),
            (130,"sSkyAmbientAltitude2dTable",4,0x948),(131,"sTransmit2dTable",5,0x960),(132,"sSctr3dTable",6,0x978)];
        if(mappings.Count!=expected.Length)throw new InvalidDataException("Cloud actual selected primary source alias count changed.");
        foreach(var e in expected)
        {
            var a=mappings.SingleOrDefault(a=>a.PrimaryEncoded==e.Encoded);
            int encoded=e.Encoded,descriptor=(encoded&7)+8*(((encoded>>3)&15)+10*(encoded>>7));
            if(a is null||a.Name!=e.Name||a.SlotIndex!=e.Slot||a.SecondarySamplerEncoded!=e.Encoded||0x198+descriptor*24!=e.Offset)
                throw new InvalidDataException("Cloud native primary alias registration differs from its exact source slot.");
        }
        var rdef=shader.ReadResourceDefinitions(pixel);
        Consumer[] rows=[new(0,"sBase",8,0,0),new(1,"sDetail",8,0,1),new(2,"sDistort",4,0,2),
            new(3,"sCirrus",4,1,0),new(4,"sSkyAmbientAltitude2dTable",4,2,3),
            new(5,"sTransmit2dTable",4,2,1),new(6,"sSctr3dTable",8,2,0)];
        if(rdef.Bindings.Count(b=>b.InputType==2)!=rows.Length||rdef.Bindings.Count(b=>b.InputType==3)!=rows.Length)
            throw new InvalidDataException("Cloud current program has an unsupported texture/sampler readset.");
        foreach(var row in rows)
        {
            var binding=rdef.Bindings.Single(b=>b.InputType==2&&b.BindPoint==row.Slot);
            var sampler=rdef.Bindings.Single(b=>b.InputType==3&&b.BindPoint==row.Slot);
            if(binding.Name.Text!=row.Name||binding.Dimension!=row.Dimension||sampler.Name.Text!="__smp"+row.Name)
                throw new InvalidDataException("Cloud original consumer readset differs from its real current source binding.");
        }
        return Array.AsReadOnly(rows);
    }
    public static IReadOnlyList<SkyAppliedCloudTextureBinding> PrepareCurrent(
        SkyAppliedSourceCloudSelection selection,SkyAppliedSeasonCloudTextureSources sources)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(sources);
        selection.ValidateCurrent(selection.AcceptedPresent,selection.SourceStages);
        var package=selection.AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        sources.RequireSelectedPackage(package);
        if(sources.Noise.ObjectId!=0xF4A532C4||sources.Noise.Entries.Count!=3||sources.Cirrus.ObjectId!=0x7E43F947||sources.Cirrus.Entries.Count!=1||
            sources.SkylightSta.ObjectId!=0x98026BD5||sources.SkylightSta.Entries.Count!=4)
            throw new InvalidDataException("Cloud current binders require these exact selected source groups.");
        var directory=Directories.GetValue(selection.Shader,static _=>new());
        var rows=directory.GetOrAdd((selection.ColorCombination,selection.ColorPixelProgram),
            key=>ReadDirectory(selection.Shader,key.Combination,key.Pixel));
        SkyCloudSourceTextureGroup[] groups=[sources.Noise,sources.Cirrus,sources.SkylightSta];
        var output=new List<SkyAppliedCloudTextureBinding>();
        foreach(var row in rows)
        {
            var group=groups[row.Group];var image=group.Entries[row.Entry];var descriptor=group.ReadSourceSamplerDescriptor(row.Entry);
            if(descriptor.Length!=52||(image.Image.TextureSystem==2?8u:4u)!=row.Dimension)
                throw new InvalidDataException("Cloud source image/sampler does not cover the current texture read.");
            output.Add(new(row.Slot,row.Name,row.Dimension,image,group.ReadSourceShaderViewFormat(row.Entry),descriptor,
                selection.SourceProvenance+";1196026-current-binderfalse;source-group="+group.ObjectId.ToString("X8")+
                "/entry="+row.Entry+";97FAC0/97E100-primaryalias;exact-RDEF:"+row.Name));
        }
        return output.AsReadOnly();
    }
}
