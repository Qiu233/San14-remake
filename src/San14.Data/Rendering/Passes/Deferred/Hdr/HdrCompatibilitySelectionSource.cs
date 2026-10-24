using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Adapter for existing texture-plan callers. A loaded plan owns one
/// imported program bank; no current frame or producer receipts enter its cache.</summary>
internal sealed class HdrCompatibilitySelectionSource
{
    private static readonly ConditionalWeakTable<LoadedDeferredTexturePlan,HdrCompatibilitySelectionSource> Sources=new();
    private readonly ConcurrentDictionary<int,LoadedDeferredTexturePlan> texturePlans=new();
    private readonly Dictionary<int,MapSelectedSourceImageLayout> imageLayouts=new();
    private HdrCompatibilitySelectionSource(LoadedDeferredTexturePlan plan)
    {
        Source=plan;
        bool ocean=plan.Node.Call.Arguments.Single(argument=>argument.NameHash==0xE8F3DC36).RawValue.Single()!=0;
        Bank=HdrProgramBank.Import(plan.Shader,ocean);
    }
    internal static HdrCompatibilitySelectionSource Read(LoadedDeferredTexturePlan plan)
        =>Sources.GetValue(plan,source=>new(source));
    internal LoadedDeferredTexturePlan Source {get;}
    internal HdrProgramBank Bank {get;}
    internal MapSelectedSourceImageLayout ImageLayout(HdrShaderProgram program)
    {
        lock(imageLayouts)
        {
            if(imageLayouts.TryGetValue(program.CombinationIndex,out var existing)) return existing;
            //Publish only a complete import. A failed import leaves no partial
            //layout, cached exception or current-frame state in this owner.
            var imported=MapSelectedSourceImagePlan.ImportLayout(TexturePlan(program),program);
            imageLayouts.Add(program.CombinationIndex,imported);
            return imported;
        }
    }
    internal LoadedDeferredTexturePlan TexturePlan(HdrShaderProgram program)
        =>texturePlans.GetOrAdd(program.CombinationIndex,_=>
        {
            var bindings=program.ResourceBindings;
            var definitions=program.PixelDefinitions;
            var inputs=Source.Inputs.Select(input=>
            {
                var mapping=bindings.Entries.Single(row=>row.PrimaryEncoded==input.EncodedParameter);
                var declaration=definitions.Bindings.Single(row=>row.InputType==2&&row.Name.Text==mapping.Name);
                if(mapping.SlotIndex<0 || declaration.BindPoint!=(uint)mapping.SlotIndex ||
                    declaration.Dimension!=input.PixelBinding.Dimension || declaration.ReturnType!=5 || declaration.BindCount!=1)
                    throw new InvalidDataException("HDR target and selected resource binding have incompatible types.");
                return input with {ResourceMapping=mapping,PixelBinding=declaration};
            }).ToArray();
            return Source with {ResourceBindings=bindings,PixelDefinitions=definitions,Inputs=Array.AsReadOnly(inputs)};
        });
}
