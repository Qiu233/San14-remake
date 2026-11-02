using System.Globalization;
using System.Security.Cryptography;
using San14.Data.Formats;
namespace San14.Data;

public sealed partial class GameResourceRepository
{
    private readonly Dictionary<(string Resource,string Sprite),LoadedUiSpriteSource> encodedPortraitSources=[];
    private int encodedPortraitVersion=-1;
    internal LoadedUiSpriteSource LoadExternalPortraitSource(string resourceName,string spriteName)
    {
        int version=PortraitResourceVersion;
        if(encodedPortraitVersion!=version){encodedPortraitSources.Clear();encodedPortraitVersion=version;}
        if(encodedPortraitSources.TryGetValue((resourceName,spriteName),out var retained))return retained;
        int bank,appearance,part=0;string? relative=null;DlcPortraitDescriptor? descriptor=null;
        bool user=TryUserPortraitResource(resourceName,out string userRelative,out bank,out appearance);
        if(user)
        {
            relative=userRelative;
            if(spriteName!="portrait"||!LoadUserPortraits().IsRegistered(appearance)||!LoadUserPortraits().HasResource(appearance,bank))
                throw new InvalidDataException("officer.portrait.user_resource_identity");
        }
        else
        {
            descriptor=DlcPortraitDescriptor.ForResource(resourceName)??throw new InvalidDataException("officer.portrait.dlc_resource_identity");
            string[] parts=spriteName.Split('_');
            if(parts.Length!=4||parts[0]!="portrait"||!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out appearance)||
                !int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out bank)||
                !int.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out part)||bank is not(9 or 10 or 11)||
                (bank==9?part is <1 or >3:part!=0)||!descriptor.Contains(appearance)||descriptor.SpriteName(appearance,bank,part)!=spriteName)
                throw new InvalidDataException("officer.portrait.dlc_sprite_identity");
        }
        // A bank9 archive entry is one original image with three authored UV
        // views. Reuse its existing first-part source entry, including when a
        // caller requests part2 first. No second cache or decoded pixels.
        string primaryName=bank==9&&!user?descriptor!.SpriteName(appearance,9,1):spriteName;
        LoadedTextureContainer resource;
        if(encodedPortraitSources.TryGetValue((resourceName,primaryName),out var primary))resource=primary.Resource;
        else
        {
            byte[] decoded;LoadedRdbAsset source;
            if(user)
            {
                var file=ReadUserPortraitFile(relative!);decoded=Zp1Reader.Read(file.Bytes).DecodedPayload;
                source=new(decoded,file.Provenance.ResourceUri,file.Provenance.PhysicalPath,0,
                    Convert.ToHexString(SHA256.HashData(decoded)),file.Provenance,file.Provenance.IsOverride?file.Provenance:null);
            }
            else
            {
                _=RequireDlcPortrait(appearance,bank);var archive=LoadPortraitLinkBin(descriptor!).Value!;
                int entry=descriptor!.EntryIndex(appearance,bank);var record=archive.Index.Entries[entry];
                if(record.Length>int.MaxValue)throw new InvalidDataException("officer.portrait.dlc_entry_size");
                var generation=BeginResourceRead(GameResourceId.File(descriptor.RelativePath),SourcePath(descriptor.RelativePath),archive.Source.PhysicalPath);
                using var stream=File.OpenRead(archive.Source.PhysicalPath);
                if(stream.Length!=archive.Index.ContainerLength)throw new InvalidDataException("officer.portrait.dlc_archive_changed_reimport_required");
                stream.Position=record.Offset;var bytes=new byte[(int)record.Length];stream.ReadExactly(bytes);ObserveResidentRead(generation);
                decoded=Zp1Reader.Read(bytes).DecodedPayload;
                source=new(decoded,descriptor.ResourceUri,archive.Source.PhysicalPath,(uint)entry,
                    Convert.ToHexString(SHA256.HashData(decoded)),archive.Source,archive.Source.IsOverride?archive.Source:null);
            }
            resource=new(G1TextureContainerReader.Read(decoded),source);
            encodedPortraitSources[(resourceName,primaryName)]=Create(primaryName,bank==9&&!user?1:part,resource);
        }
        var loaded=Create(spriteName,part,resource);encodedPortraitSources[(resourceName,spriteName)]=loaded;return loaded;

        LoadedUiSpriteSource Create(string name,int selectedPart,LoadedTextureContainer imageResource)
        {
            float left=0,right=bank==10?242f/256f:1,bottom=1;
            if(bank==9)
            {
                bottom=900f/1024f;
                if(user)right=636f/1024f;
                else (left,right)=selectedPart switch
                {1=>(0f,636f/2048f),2=>(637f/2048f,1273f/2048f),3=>(1274f/2048f,1910f/2048f),_=>throw new InvalidDataException("officer.portrait.dlc_part")};
            }
            var definition=new SpriteDefinition(name,0,new(left,0),new(right,0),new(left,bottom),new(right,bottom),0);
            var image=imageResource.Container.Textures[0];_=definition.GetRectangle(image.Width,image.Height);
            return new(definition,imageResource,imageResource.Source);
        }
    }
}
