using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One actual static generation's compiled Forward numeric schema and
/// declarations. No current packet, World, camera, CB or target is retained.</summary>
public sealed class D3D11PrimaryForwardCpuResources
{
    public D3D11PrimaryForwardNumericInputPreparation Numeric { get; }
    public uint DeclaredLightGroup { get; }
    public RenderTextureDefinition SubDeclaration { get; }
    public D3D11PrimaryForwardAllocationFields Allocation { get; }
    public int EligibleCount { get; }
    private D3D11PrimaryForwardCpuResources(D3D11PrimaryForwardNumericInputPreparation numeric,
        uint group,RenderTextureDefinition sub,D3D11PrimaryForwardAllocationFields allocation,int eligible)
        =>(Numeric,DeclaredLightGroup,SubDeclaration,Allocation,EligibleCount)=(numeric,group,sub,allocation,eligible);

    internal static D3D11PrimaryForwardCpuResources Prepare(GameResourceRepository repository,
        D3D11PrimaryForwardNumericInputPreparation numeric,int eligible)
    {
        var node=repository.LoadRenderGraph("system",0x024496EE).Document.Nodes[187];
        var group=node.Call.Arguments.Single(value=>value.NameHash==0x59BD6FC4).RawValue;
        if(group.Length!=4)throw new InvalidDataException("Forward light group requires its scalar declaration.");
        var subKeys=node.Call.Arguments.Single(value=>value.NameHash==0xBC31BFD4).RawValue;
        var sub=RenderTextureResources.Read(repository.LoadObjects("system",0x93FB3280),
            BinaryPrimitives.ReadUInt32LittleEndian(subKeys.AsSpan(4,4)));
        var view=repository.LoadObjects("system",DefaultView0Source.ViewResourceId).Document.Objects
            .Single(value=>value.ObjectId==DefaultView0Source.ViewObjectId);
        uint U(uint hash,int lane=0)=>BinaryPrimitives.ReadUInt32LittleEndian(
            view.Properties.Single(value=>value.NameHash==hash).RawValue.AsSpan(lane*4,4));
        var allocation=new D3D11PrimaryForwardAllocationFields(
            U(0x91221B9A),U(0x2103C71C),U(0x3C7FF710),U(0x2563818C),U(0x2563818C,1),U(0x2563818C,2),U(0x739F83E1),
            checked((uint)sub.Width),checked((uint)sub.Height),checked((uint)sub.ArrayDepth!.Value),RenderTextureFormatMath.Resolve(sub));
        return new(numeric,BinaryPrimitives.ReadUInt32LittleEndian(group),sub,allocation,eligible);
    }
}
