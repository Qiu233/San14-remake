using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmyGraphicsAction(int Category,int RawActionId,
    LoadedOriginalArmyAnimationAction Animation,float Duration,float StartFrame,float DeclaredLength,
    float EndOffset,float Interpolation,float LoopDeclaration,uint ContinuationAction,uint ContinuationObject,
    uint PlayerFlags,int AdditionalAnimationCount);

/// <summary>Actual ActionID (7FB88531) to declared player/clip mapping. Body
/// graphics actions0/1/2, siege0/2 and flag0 are distinct from gameplay Action37.</summary>
public sealed class OriginalArmyGraphicsActions
{
    public IReadOnlyDictionary<(int Category,int RawAction),OriginalArmyGraphicsAction> Actions {get;}
    public OriginalArmyGraphicsActions(OriginalGameRepository repository):this(repository,Enumerable.Range(0,4)){}
    public OriginalArmyGraphicsActions(OriginalGameRepository repository,IEnumerable<int> categories)
    {
        var catalog=OriginalCeArmyModels.LoadCatalog(repository);
        var singleton=repository.LoadObjects("KIDSSystemResource",OriginalArmyAnimationResources.SingletonId);
        var result=new Dictionary<(int,int),OriginalArmyGraphicsAction>();
        foreach(int category in categories){
            uint definition=OriginalCeArmyModels.KnownSourceCategories[category].DefinitionId;
            var setting=catalog.Catalog.Document.Objects.Single(obj=>obj.Kind==DokObjectKind.Inline&&obj.ClassId==0xD186CEEB&&Word(obj,0x68A6F779)==definition);
            var array=setting.Properties.Single(p=>p.NameHash==0x7245DD82);
            if(array.ElementSize!=4||array.RawValue.Length!=array.ElementCount*4)
                throw new InvalidDataException("Army declared action array shape differs.");
            var ordinals=new Dictionary<int,int>();
            for(int ordinal=0;ordinal<array.ElementCount;++ordinal){
                uint id=BinaryPrimitives.ReadUInt32LittleEndian(array.RawValue.AsSpan(ordinal*4,4));
                var player=singleton.Document.Objects.Single(obj=>obj.ObjectId==id);
                int raw=checked((int)Word(player,0x7FB88531));
                if(!ordinals.TryAdd(raw,ordinal))throw new InvalidDataException("Army source ActionID mapping is ambiguous.");
            }
            foreach(int raw in category<2?new[]{0,1,2}:category is 5 or 6?new[]{0,2}:new[]{0}){
                var animation=OriginalArmyAnimationResources.LoadAction(repository,catalog,category,ordinals[raw]);
                if(animation.AnimationData.Count!=1)
                    throw new NotSupportedException("Army graphics action requires its separate multi-clip player consumer.");
                var obj=animation.ActionObject;var clip=animation.AnimationData[0].Clip;
                var extra=obj.Properties.Single(p=>p.NameHash==0xE1773A02);
                result.Add((category,raw),new(category,raw,animation,clip.FrameCount/clip.FrameRate,
                    Float(obj,0x750E63CB),Float(obj,0xF977F44D),Float(obj,0xD3F46C71),Float(obj,0x4BEF4ECC),
                    Float(obj,0xC2AFEDCF),Word(obj,0xA26BA507),Word(obj,0xA87D31E6),Word(obj,0x05D92ACC),extra.ElementCount));
            }
        }
        Actions=new ReadOnlyDictionary<(int,int),OriginalArmyGraphicsAction>(result);
    }
    private static uint Word(DokObject obj,uint key)=>BinaryPrimitives.ReadUInt32LittleEndian(obj.Properties.Single(p=>p.NameHash==key).RawValue);
    private static float Float(DokObject obj,uint key)=>BitConverter.UInt32BitsToSingle(Word(obj,key));
}
