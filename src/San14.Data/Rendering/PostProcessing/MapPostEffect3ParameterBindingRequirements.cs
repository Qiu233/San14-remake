using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapPostEffect3ParameterShaderValueRange(string Name,int Offset,int ByteCount);

/// <summary>A native ordered CB binding is distinct from compiler usage. Null
/// ShaderDeclaredByteCount means that the selected shader declares no CB at
/// this register. It does not manufacture a zero-valued native CB or an upload.</summary>
public sealed record MapPostEffect3ParameterBindingRequirement(int SourceRowIndex,
    int ParameterIndex,uint StageCode,int RegisterIndex,
    MapPostEffect3ConstantBufferParameter NativeDeclaration,
    MapPostEffect3Program Program,string? ShaderDeclaredName,int? ShaderDeclaredByteCount,
    IReadOnlyList<MapPostEffect3ParameterShaderValueRange> UsedVariables);

public static class MapPostEffect3ParameterBindingRequirements
{
    private static readonly uint[] Stages=[5,1,0,2,3,4];
    private static readonly ConditionalWeakTable<MapPostEffect3AllocationPlan,LoadedRequirements> Loaded=new();

    /// <summary>Join the actual pass+28 type6 span to counted type4 resources
    /// and selected stage DXBC. Preserve duplicate source rows and stage order.
    /// D3D_SVF_USED(2) is retained explicitly, not interpreted as initializer data.
    /// Register absence is a valid native binding optimized out of the shader.</summary>
    public static IReadOnlyList<MapPostEffect3ParameterBindingRequirement> Read(
        MapPostEffect3AllocationPlan allocation,int passIndex)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if((uint)passIndex>=allocation.Passes.Count)throw new ArgumentOutOfRangeException(nameof(passIndex));
        return Loaded.GetValue(allocation,static a=>new(a)).Passes[passIndex].Value;
    }

    // Allocation shapes, source type6 rows and selected DXBC declarations are
    // immutable for this loaded allocation. No current CB, texture pointer,
    // frame or controller value belongs in this cache.
    private sealed class LoadedRequirements
    {
        internal readonly Lazy<IReadOnlyList<MapPostEffect3ParameterBindingRequirement>>[] Passes;
        internal LoadedRequirements(MapPostEffect3AllocationPlan allocation)
            =>Passes=Enumerable.Range(0,allocation.Passes.Count).Select(pass=>
                new Lazy<IReadOnlyList<MapPostEffect3ParameterBindingRequirement>>(
                    ()=>ReadLoaded(allocation,pass))).ToArray();
    }
    private static IReadOnlyList<MapPostEffect3ParameterBindingRequirement> ReadLoaded(
        MapPostEffect3AllocationPlan allocation,int passIndex)
    {
        var pass=allocation.Passes[passIndex];var result=new List<MapPostEffect3ParameterBindingRequirement>();
        foreach(var b in pass.ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead))
        {
            var native=allocation.ConstantBufferParameters[b.ParameterIndex];
            for(int bit=0;bit<Stages.Length;bit++)
            {
                if((b.StageMask&pass.NativeActiveStageMask&(1<<bit))==0)continue;
                var stage=pass.Pass.ProgramReferences.Single(p=>p.StageCode==Stages[bit]);
                if(stage.StageIndex is not int i)continue;
                var program=allocation.Source.GetProgram(Stages[bit],i);
                var rdef=DxbcResourceDefinitionReader.Read(program.Bytes.Span);
                var registers=rdef.Bindings.Where(r=>r.InputType==0&&r.BindPoint==b.RegisterIndex).ToArray();
                if(registers.Length==0)
                {
                    result.Add(new(b.SourceRowIndex,b.ParameterIndex,Stages[bit],b.RegisterIndex,native,program,
                        null,null,Array.Empty<MapPostEffect3ParameterShaderValueRange>()));continue;
                }
                if(registers.Length!=1||registers[0].BindCount!=1)
                    throw new InvalidDataException("3SPK CB register is not a single actual selected-program binding.");
                var buffer=rdef.Buffers.SingleOrDefault(r=>r.Name.Text==registers[0].Name.Text)
                    ??throw new InvalidDataException("3SPK selected program CB binding lacks its actual buffer declaration.");
                if(buffer.ByteSize<0||buffer.ByteSize>native.ByteCount)
                    throw new InvalidDataException("3SPK actual shader CB extent exceeds its native counted storage.");
                var used=buffer.Variables.Where(v=>(v.Flags&2)!=0).Select(v=>
                    new MapPostEffect3ParameterShaderValueRange(v.Name.Text,v.StartOffset,v.ByteSize)).ToArray();
                if(used.Any(v=>v.Offset<0||v.ByteCount<0||(long)v.Offset+v.ByteCount>buffer.ByteSize))
                    throw new InvalidDataException("3SPK used shader CB variable exceeds its declared extent.");
                result.Add(new(b.SourceRowIndex,b.ParameterIndex,Stages[bit],b.RegisterIndex,native,program,
                    buffer.Name.Text,buffer.ByteSize,Array.AsReadOnly(used)));
            }
        }
        return result.AsReadOnly();
    }
}
