using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11OceanTextureInputSource(string Prefix,string Stage,uint Slot,uint Dimension,
    int Width,int Height,int Depth,int MipCount,int LayerCount,uint ResourceFormat,uint ShaderViewFormat,
    string DefinitionUri,uint DefinitionObjectId,string ResourceUri,string DecodedSha256,int ImageIndex);

/// <summary>The four stock static Ocean texture inputs, prepared once from the
/// user's installation. Encoded source mips, source sampler keys/descriptors and
/// headers are retained. The selected texture namespaces are caller inputs;
/// object IDs are resolved through their declared context and TID. Generated
/// files are private rebuildable cache contents, not distributable assets.
/// Current WV publication, animation, prepass and GPU readiness are separate.</summary>
public sealed class D3D11OceanTextureInputPreparation
{
    private enum Role{Normal3d,NormalNoise2d,Bubble,HeightMask}
    private readonly IReadOnlyDictionary<string,byte[]> files;

    private D3D11OceanTextureInputPreparation(Dictionary<string,byte[]> preparedFiles,
        D3D11OceanTextureInputSource[] sources)
    {
        files=preparedFiles;Textures=Array.AsReadOnly(sources);
    }
    public IReadOnlyList<D3D11OceanTextureInputSource> Textures{get;}
    public IReadOnlyList<string> PreparedFilenames=>Array.AsReadOnly(files.Keys.ToArray());

    /// <summary>Namespace choices are explicit: stock normal textures reside in
    /// system and stock WV Static contexts in RRPreview. No archive scan, saved
    /// receipt, research JSON or fallback texture is used. All resource IO,
    /// hash/profile validation and byte encoding happens at this boundary.</summary>
    public static D3D11OceanTextureInputPreparation Load(GameResourceRepository repository,
        NamedViewRendererResources renderer,string normalTextureArchive,string waveTextureArchive)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalTextureArchive);
        ArgumentException.ThrowIfNullOrWhiteSpace(waveTextureArchive);
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var rows=new List<string>();var sources=new List<D3D11OceanTextureInputSource>();
        var contexts=new Dictionary<(string Archive,uint Context),LoadedTextureContainer>();
        var common=renderer.Definitions;
        AddGroup(renderer.NormalVolumeGroup,Role.Normal3d,"normal3d","PS",0);
        AddGroup(renderer.NormalNoiseGroup,Role.NormalNoise2d,"normal-noise2d","PS",3);
        var waveDefinitions=repository.LoadObjects("KIDSSystemResource",0xD2ECCCDF);
        Add(waveDefinitions,0x93CD9BB8,waveTextureArchive,Role.Bubble,"wv-bubble","PS",4);
        Add(waveDefinitions,0x5F6373E6,waveTextureArchive,Role.HeightMask,"wv-height-mask","VS",0);
        files.Add("ocean-textures.tsv",Encoding.UTF8.GetBytes(string.Join(Environment.NewLine,rows)+Environment.NewLine));
        return new(files,sources.ToArray());

        void AddGroup(uint groupId,Role role,string prefix,string stage,uint slot)
        {
            var matches=common.Document.Objects.Where(o=>o.ObjectId==groupId).ToArray();
            if(matches.Length!=1||matches[0].Kind!=DokObjectKind.Inline||matches[0].ClassId!=0x3059B9C3)
                throw new InvalidDataException($"Ocean {prefix} requires its actual inline texture-group declaration {groupId:X8}.");
            var tableField=Field(matches[0],0x7A1E1EF8,5);
            var table=repository.LoadRdbAsset("system",BinaryPrimitives.ReadUInt32LittleEndian(tableField.RawValue));
            string text;
            try{text=new UTF8Encoding(false,true).GetString(table.Bytes).TrimStart('\uFEFF');}
            catch(DecoderFallbackException error){throw new InvalidDataException($"Ocean {prefix} group table is not valid UTF8.",error);}
            var entries=text.Split('\n').Select(line=>line.Trim()).Where(line=>line.Length>0&&!line.StartsWith(';')).ToArray();
            if(entries.Length!=1)
                throw new NotSupportedException($"Ocean {prefix} requires the selected source group's unique first texture entry; multiple-entry publication is outside this input leaf.");
            string[] entry=entries[0].Split(',',2);
            if(entry.Length!=2||!int.TryParse(entry[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out int sourceSlot)||
                sourceSlot!=0||string.IsNullOrWhiteSpace(entry[1]))
                throw new InvalidDataException($"Ocean {prefix} source group requires its exact slot0 named texture entry.");
            Add(common,ResourceNameHash.Compute(entry[1]),normalTextureArchive,role,prefix,stage,slot);
        }

        void Add(LoadedObjects definitions,uint objectId,string archive,Role role,string prefix,string stage,uint slot)
        {
            var matches=definitions.Document.Objects.Where(o=>o.ObjectId==objectId).ToArray();
            if(matches.Length!=1||matches[0].Kind!=DokObjectKind.Inline||matches[0].ClassId!=0xFF7DBFD4)
                throw new InvalidDataException($"Ocean {prefix} requires one actual inline Static texture {objectId:X8}.");
            var definition=matches[0];var context=Field(definition,0x6C7321D2,5);var tid=Field(definition,0x00200571,4);
            uint contextId=BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue);
            if(!contexts.TryGetValue((archive,contextId),out var resource))
            {
                // Repository caches the requested RDB index and reads only this
                // record's payload. Its returned source hash covers decoded bytes.
                var source=repository.LoadRdbAsset(archive,contextId);
                try{resource=new(ReadContainer(source.Bytes,role),source);}
                catch(Exception error) when(error is InvalidDataException or NotSupportedException)
                {throw new InvalidDataException($"Ocean {prefix} source {source.ResourceUri}: {error.Message}",error);}
                contexts.Add((archive,contextId),resource);
            }
            uint imageIndex=BinaryPrimitives.ReadUInt32LittleEndian(tid.RawValue);
            if(imageIndex>=resource.Container.Textures.Count)
                throw new InvalidDataException($"Ocean {prefix} Static TID{imageIndex} lies outside {resource.Source.ResourceUri}.");
            var binding=new StaticTextureBinding(definitions,definition,context,tid,resource,(int)imageIndex);
            var image=binding.Image;
            if(image.RawHeader.Length!=20||BinaryPrimitives.ReadUInt32LittleEndian(image.RawHeader.AsSpan(8,4))!=12)
                throw new InvalidDataException($"Ocean {prefix} requires the original 12-byte texture extra header.");
            byte colorSpace=(byte)(image.RawHeader[19]&1);
            var formats=image.PixelFormat==0x5D?new ModelTextureFormats(82,colorSpace==0?83u:0):
                ModelTextureFormatMath.Resolve(image.PixelFormat,colorSpace);
            uint dimension=image.TextureSystem==2?8u:image.LayerCount>1?5u:4u;
            var expected=role switch
            {
                Role.Normal3d=>(Dimension:8u,Resource:82u,View:83u),
                Role.NormalNoise2d=>(Dimension:4u,Resource:79u,View:80u),
                Role.Bubble=>(Dimension:5u,Resource:90u,View:91u),
                Role.HeightMask=>(Dimension:4u,Resource:39u,View:41u),
                _=>throw new InvalidDataException("Unknown Ocean source role.")
            };
            if(dimension!=expected.Dimension||formats.ResourceFormat!=expected.Resource||formats.ShaderViewFormat!=expected.View||
                image.Width<=0||image.Height<=0||image.Depth<=0||image.MipCount<=0||image.LayerCount<=0||
                (dimension!=8&&image.Depth!=1)||(role!=Role.Bubble&&image.LayerCount!=1))
                throw new NotSupportedException($"Ocean {prefix} texture resource/SRV dimension/format differs from the supported Native loader profile.");
            var sampler=G1TextureSamplerReader.ReadRetainedSource(image);
            files.Add(prefix+"-sampler.bin",sampler.EncodeD3D11Descriptor());
            files.Add(prefix+"-sampler-key.bin",sampler.RawKey.ToArray());
            files.Add(prefix+"-g1t-header.bin",image.RawHeader.ToArray());
            if(image.SubresourceLayout==G1TextureSubresourceLayout.ArrayOrderUnresolved)
                throw new NotSupportedException($"Ocean {prefix} array subresource upload order is unresolved.");
            for(int layer=0;layer<image.LayerCount;layer++)for(int mip=0;mip<image.MipCount;mip++)
            {
                var m=image.Mips[mip];
                byte[] bytes=(image.TextureSystem==2?image.GetEncodedVolumeMip(resource.Source.Bytes,mip):
                    image.GetEncodedMip(resource.Source.Bytes,mip,layer)).ToArray();
                int blockBytes=image.PixelFormat is 0x59 or 0x5C?8:16;
                int rowPitch=image.PixelFormat>=0x59?Math.Max(1,(m.Width+3)/4)*blockBytes:checked(m.Width*4);
                int slicePitch=checked(image.PixelFormat>=0x59?rowPitch*Math.Max(1,(m.Height+3)/4):rowPitch*m.Height);
                if(bytes.Length!=checked(slicePitch*m.Depth))
                    throw new InvalidDataException($"Ocean {prefix} encoded mip{mip}/layer{layer} pitch/body mismatch.");
                string filename=image.LayerCount==1?$"{prefix}-mip{mip}.bin":$"{prefix}-layer{layer}-mip{mip}.bin";
                files.Add(filename,bytes);
            }
            rows.Add(FormattableString.Invariant($"{prefix}\t{slot}\t{dimension}\t{image.Width}\t{image.Height}\t{image.Depth}\t{image.MipCount}\t{image.LayerCount}\t{formats.ResourceFormat}\t{formats.ShaderViewFormat}"));
            sources.Add(new(prefix,stage,slot,dimension,image.Width,image.Height,image.Depth,image.MipCount,image.LayerCount,
                formats.ResourceFormat,formats.ShaderViewFormat,definitions.Source.ResourceUri,objectId,
                resource.Source.ResourceUri,resource.Source.DecodedSha256,binding.ImageIndex));
        }
    }

    /// <summary>Writes only the prepared static texture bank. No source reads,
    /// source hashing, format parsing or prepass processing occurs here.</summary>
    public void WriteNativeInputs(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root=Path.GetFullPath(directory);Directory.CreateDirectory(root);
        foreach(var file in files)File.WriteAllBytes(Path.Combine(root,file.Key),file.Value);
    }

    private static DokProperty Field(DokObject obj,uint hash,ushort type)
    {
        var fields=obj.Properties.Where(p=>p.NameHash==hash).ToArray();
        if(fields.Length!=1||fields[0].TypeCode!=type||fields[0].ElementCount!=1||fields[0].ElementSize!=4||
            fields[0].RawValue is not{Length:4})
            throw new InvalidDataException($"Ocean source {obj.ObjectId:X8} requires one type{type} scalar {hash:X8}.");
        return fields[0];
    }

    // Only Ocean's three extra source families are adapted here. The shared
    // G1T reader keeps its other unsupported profiles unchanged.
    private static G1TextureContainer ReadContainer(ReadOnlySpan<byte> data,Role role)
    {
        if(role is Role.NormalNoise2d or Role.Bubble)
        {
            var ordinary=G1TextureContainerReader.Read(data);
            if(role==Role.NormalNoise2d)return ordinary;
            if(ordinary.Textures.Count!=1||ordinary.Textures[0].TextureSystem!=3||ordinary.Textures[0].MipCount!=1)
                throw new NotSupportedException("Ocean Bubble requires its source single-image/system3/single-mip array; multi-mip ordering remains unsupported.");
            // One mip has only layer ordering; retain the original contiguous
            // slices without extending support to an unproved multi-mip array.
            return ordinary with{Textures=Array.AsReadOnly(new[]{ordinary.Textures[0] with{
                SubresourceLayout=G1TextureSubresourceLayout.MipMajorArray}})};
        }
        if(data.Length<36||!data[..4].SequenceEqual("GT1G"u8))
            throw new InvalidDataException("Original Ocean G1T container header is missing.");
        string version=Encoding.ASCII.GetString(data.Slice(4,4));
        uint platform=BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20,4));
        int table=BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12,4));
        int count=BinaryPrimitives.ReadInt32LittleEndian(data.Slice(16,4));
        if(platform!=0xA||count!=1||table<28||table>4096||table>data.Length-24||
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(table,4))!=4)
            throw new InvalidDataException("Original Ocean special G1T family requires its Windows single-image, nonoverlapping directory.");
        int start=table+4;byte[] header=data.Slice(start,20).ToArray();
        if((header[7]>>4)!=1||BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8,4))!=12)
            throw new InvalidDataException("Original Ocean special image requires its exact 12-byte extra header.");
        G1TextureImage image;
        if(role==Role.HeightMask)
        {
            if(version!="1600"||header[0]!=0x10||header[1]!=2||header[2]!=0xBB||header[3]!=0||
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(16,2))!=0x10||data.Length-start!=20+2048*2048*4)
                throw new NotSupportedException("Original HeightMask requires its exact system0/R32/2048x2048 single-mip family with retained extra-word0x10.");
            var range=new G1EncodedRange(start+20,2048*2048*4);
            image=new(0,2,2048,2048,1,1,0,1,0x10,G1TextureSubresourceLayout.SingleLayerMipChain,
                new(start,data.Length-start),range,header,Array.AsReadOnly(new[]{new G1TextureMip(0,2048,2048,2048*2048*4,range)}),1,version);
        }
        else
        {
            int system=header[0]&15,mips=header[0]>>4,width=1<<(header[2]&15),height=1<<(header[2]>>4),depth=1<<(header[3]&15);
            if(version!="0600"||header[1]!=0x5D||system!=2||mips==0||
                (BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(16,2))>>4)!=0)
                throw new NotSupportedException("Original Ocean normal3d requires its BC5/0600/system2 volume family with no array count.");
            var levels=new List<G1TextureMip>();int at=start+20,w=width,h=height,d=depth;
            for(int mip=0;mip<mips;mip++)
            {
                if(w<4||h<4)throw new NotSupportedException("Original BC5 volume mip below4x4 needs a separately supported uploader profile.");
                int slice=checked(((w+3)/4)*((h+3)/4)*16),length=checked(slice*d);
                if(at>data.Length-length)throw new InvalidDataException("Original Ocean BC5 mip lies outside its encoded body.");
                levels.Add(new(mip,w,h,slice,new(at,length),d));at=checked(at+length);
                w=Math.Max(1,w>>1);h=Math.Max(1,h>>1);d=Math.Max(1,d>>1);
            }
            if(at!=data.Length)throw new InvalidDataException("Original Ocean BC5 mips must cover the encoded body exactly.");
            image=new(0,0x5D,width,height,(byte)mips,mips,2,1,BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(16,2)),
                G1TextureSubresourceLayout.VolumeMipChain,new(start,data.Length-start),new(start+20,data.Length-start-20),header,levels.AsReadOnly(),depth,version);
        }
        // Native Windows loader does not use header08 as a body-length producer;
        // preserve that raw word and prove full actual directory/body coverage.
        return new(version,platform,data.Length,table,[4],data[..start].ToArray(),Array.AsReadOnly(new[]{image})){
            RawDeclaredSourceByteLength=BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8,4)),
            DeclaredLengthSourceEvidence="Windows-header08-not-consumed;validated-single-image-directory-and-exact-body-coverage"};
    }
}
