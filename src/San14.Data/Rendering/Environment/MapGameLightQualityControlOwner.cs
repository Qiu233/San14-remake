namespace San14.Data;

/// <summary>The application callback's current four controls. Source resources
/// stay immutable. The first registered directional actor with group80 owns
/// the callback; its mode child is distinct from the renderer's provider pair.</summary>
public sealed class MapGameLightQualityControlOwner
{
    private readonly Dictionary<uint,MapGameLightQualityWrites> controls=[];
    private readonly Dictionary<uint,DirectionalLightConstructionPacket> registered=[];
    private readonly Dictionary<uint,(DirectionalLightConstructionPacket Root,MapGameLightQualityWrites Controls,
        DirectionalLightProviderTemporary Value)> temporaries=[];
    private readonly Dictionary<uint,(DirectionalLightConstructionPacket Actor,MapGameLightQualityWrites Controls,byte[] Bytes)> selectedData=[];
    private readonly MapSeasonDirectionalLightProvider? callbackRoot;
    public MapSeasonDirectionalLightSourceSet Source {get;}
    public DirectionalLightConstructionPacket FirstRegisteredRoot {get;}

    public MapGameLightQualityControlOwner(IReadOnlyList<DirectionalLightConstructionPacket> registeredRoots)
    {
        if(registeredRoots.Count==0)throw new InvalidDataException("Game lighting controls require the actual registered actors.");
        FirstRegisteredRoot=registeredRoots[0];
        Source=registeredRoots[0].Source;
        if(registeredRoots.Count!=Source.Roots.Count)throw new InvalidDataException("Registered light controls need the complete source root list.");
        for(int index=0;index<registeredRoots.Count;index++)
        {
            var root=registeredRoots[index];var declaration=Source.Roots[index];
            if(!ReferenceEquals(root.Source,Source)||root.SourceObjectId!=declaration.Root.ObjectId)
                throw new InvalidDataException("Game lighting control registration has another source/order.");
            controls.Add(root.SourceObjectId,MapGameLightQualityOverlay.ReadCurrentControls(root.PreparedBytes));
            registered.Add(root.SourceObjectId,root);
            foreach(var child in declaration.Children)
            {
                var settings=Source.GetSettings(child.ObjectId);var extension=settings.Extensions.Span;
                controls.Add(child.ObjectId,new(extension[1],extension[2],extension[5],settings.Shadow.ReceiveShadowType));
            }
            // CApp stops at its first compatible actor whose group includes80.
            if(callbackRoot is null&&(root.Metadata.SourceGroupMask&0x80)!=0)callbackRoot=declaration;
        }
    }

    /// <summary>Apply the Game callback once at this actual preparation point.
    /// CC7480 selects list[MainMode] when a same-class child exists, otherwise
    /// the root. Previously selected actors retain their own control state.</summary>
    public uint? ApplyCurrent(int quality,int mainMode)
    {
        if(callbackRoot is null)return null;
        uint selected=mainMode>0&&mainMode<=callbackRoot.Children.Count
            ?callbackRoot.Children[mainMode-1].ObjectId:callbackRoot.Root.ObjectId;
        controls[selected]=MapGameLightQualityOverlay.ReadWrites(quality,mainMode);
        return selected;
    }

    public MapGameLightQualityWrites ReadCurrentControls(uint objectId)=>controls.TryGetValue(objectId,out var value)
        ?value:throw new InvalidDataException("Game lighting controls do not own this actor.");

    internal ReadOnlyMemory<byte> PrepareSelectedData(DirectionalLightConstructionPacket actor)
    {
        if(!ReferenceEquals(actor.Source,Source))throw new InvalidDataException("Selected actor has another quality source generation.");
        var current=ReadCurrentControls(actor.SourceObjectId);
        if(selectedData.TryGetValue(actor.SourceObjectId,out var cached)&&ReferenceEquals(cached.Actor,actor)&&cached.Controls==current)
            return cached.Bytes;
        byte[] bytes=DirectionalLightConstruction.PrepareSelectedGameLightData(actor,current);
        selectedData[actor.SourceObjectId]=(actor,current,bytes);return bytes;
    }

    /// <summary>The present renderer consumes its real root/root pair. Other
    /// modes retain selected child controls, not fabricated child GPU state.</summary>
    public DirectionalLightProviderTemporary PrepareRootTemporary(DirectionalLightConstructionPacket root)
    {
        if(!registered.TryGetValue(root.SourceObjectId,out var actual)||!ReferenceEquals(actual,root))
            throw new InvalidDataException("Current provider controls require a registered root of this generation.");
        var current=ReadCurrentControls(root.SourceObjectId);
        if(temporaries.TryGetValue(root.SourceObjectId,out var cached)&&ReferenceEquals(cached.Root,root)&&cached.Controls==current)
            return cached.Value;
        var value=current==MapGameLightQualityOverlay.ReadCurrentControls(root.PreparedBytes)
            ?DirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root)
            :DirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root,current);
        temporaries[root.SourceObjectId]=(root,current,value);
        return value;
    }
}
