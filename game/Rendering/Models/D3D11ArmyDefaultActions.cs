using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual constructor ActionID0 mapped through each setting's source
/// action array. Array ordinal0 is not a default-action identity.</summary>
public sealed class D3D11ArmyDefaultActions
{
    public IReadOnlyDictionary<int,LoadedArmyAnimationAction> Categories {get;}
    public ArmyGraphicsActions Graphics {get;}
    public D3D11ArmyDefaultActions(GameResourceRepository repository):this(repository,Enumerable.Range(0,4)){}
    public D3D11ArmyDefaultActions(GameResourceRepository repository,IEnumerable<int> categories)
        :this(new ArmyGraphicsActions(repository,categories)){}
    public D3D11ArmyDefaultActions(ArmyGraphicsActions graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        Graphics=graphics;
        var selected=graphics.Actions.Keys.Select(key=>key.Category).Distinct();
        var actions=new Dictionary<int,LoadedArmyAnimationAction>();
        foreach(int category in selected){
            var action=Graphics.Actions[(category,0)].Animation;
            if(action.AnimationData.Count!=1||Word(action.ActionObject,0x7FB88531)!=0)
                throw new NotSupportedException("Army default action needs its actual single declared clip.");
            var obj=action.ActionObject;
            float start=Float(obj,0x750E63CB),length=Float(obj,0xF977F44D),end=Float(obj,0xD3F46C71),interpolation=Float(obj,0x4BEF4ECC),loop=Float(obj,0xC2AFEDCF);
            var additional=obj.Properties.Single(p=>p.NameHash==0xE1773A02);
            if(new[]{start,length,end,interpolation,loop}.Any(value=>!float.IsFinite(value))||
                start!=0||length>0||end!=0||interpolation!=0||loop>=0||
                Word(obj,0xA26BA507)!=uint.MaxValue||Word(obj,0xA87D31E6)!=uint.MaxValue||Word(obj,0x05D92ACC)!=0||
                additional.ElementCount!=0||additional.RawValue.Length!=0)
                throw new NotSupportedException("Army default player requires actual zero-start, nonpositive-length, no transition/continuation, empty extra array and negative-loop profile.");
            actions.Add(category,action);
        }
        Categories=actions;
    }
    static uint Word(DokObject obj,uint key)=>BinaryPrimitives.ReadUInt32LittleEndian(obj.Properties.Single(p=>p.NameHash==key).RawValue);
    static float Float(DokObject obj,uint key)=>BitConverter.UInt32BitsToSingle(Word(obj,key));
}
