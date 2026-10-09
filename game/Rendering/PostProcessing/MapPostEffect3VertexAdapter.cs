using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data;
using San14.Data.Formats;

/// <summary>3SPK Vulkan viewport adaptation. Retains VertexIndex/InstanceIndex,
/// BaseVertex/BaseInstance and the original compiler's subtraction. The draw
/// owner supplies actual native draw offsets; no zero-offset assumption is made.</summary>
public static class MapPostEffect3VertexAdapter
{
    public static AdaptedVertexShader Adapt(ReadOnlySpan<byte> source)
    {
        var module=SpirvShaderInterfaceReader.Read(source);
        if(module.EntryPoints.Single() is not {ExecutionModel:0,Name:"main"})
            throw new InvalidDataException("3SPK raster adaptor requires the actual Vertex/main entry.");
        var positions=module.StageVariables.Where(v=>v.StorageClass==3&&v.BuiltIn==0).ToArray();
        if(positions.Length!=1||positions[0].Type is not
            {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}})
            throw new NotSupportedException("3SPK raster adaptor requires one explicit float4 raster position.");
        uint[] words=new uint[source.Length/4];
        for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(source[(i*4)..]);
        var instructions=new List<uint[]>();
        for(int at=5;at<words.Length;)
        {int n=(int)(words[at]>>16);instructions.Add(words.AsSpan(at,n).ToArray());at+=n;}
        uint position=positions[0].VariableId;
        uint pointer=instructions.Single(i=>(ushort)i[0]==59&&i[2]==position)[1];
        uint vector=instructions.Single(i=>(ushort)i[0]==32&&i[1]==pointer)[3];
        uint scalar=instructions.Single(i=>(ushort)i[0]==23&&i[1]==vector)[2];
        uint next=module.IdBound,loaded=next++,y=next++,negative=next++,changed=next++;
        static uint[] Op(ushort code,params uint[] args)=>new[]{checked((uint)(args.Length+1)<<16)|code}.Concat(args).ToArray();
        var result=new List<uint>();result.AddRange(words.AsSpan(0,5).ToArray());
        uint function=0;int returns=0;
        foreach(var i in instructions)
        {
            ushort code=(ushort)i[0];if(code==54)function=i[2];
            if(code==253&&function==module.EntryPoints[0].FunctionId)
            {
                result.AddRange(Op(61,vector,loaded,position));result.AddRange(Op(81,scalar,y,loaded,1));
                result.AddRange(Op(127,scalar,negative,y));result.AddRange(Op(82,vector,changed,negative,loaded,1));
                result.AddRange(Op(62,position,changed));returns++;
            }
            result.AddRange(i);if(code==56)function=0;
        }
        if(returns!=1)throw new NotSupportedException("3SPK raster adaptor requires one actual main return.");
        result[3]=next;byte[] bytes=new byte[result.Count*4];
        for(int i=0;i<result.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),result[i]);
        var after=SpirvShaderInterfaceReader.Read(bytes);
        // All original type/declaration instructions above are copied verbatim.
        // Compare stable coordinates, not record equality of newly parsed lists.
        static object Stage(SpirvInterfaceVariable v)=>
            (v.VariableId,v.StorageClass,v.BuiltIn,v.Location,string.Join(',',v.InterpolationDecorations));
        static object Descriptor(SpirvInterfaceVariable v)=>
            (v.VariableId,v.StorageClass,v.DescriptorSet,v.Binding,v.RequiredBufferBytes);
        if(!module.StageVariables.Select(Stage).SequenceEqual(after.StageVariables.Select(Stage))||
            !module.Descriptors.Select(Descriptor).SequenceEqual(after.Descriptors.Select(Descriptor))||
            !module.Capabilities.SequenceEqual(after.Capabilities)||!module.Extensions.SequenceEqual(after.Extensions))
            throw new InvalidDataException("3SPK raster adaptation changed an original interface or draw-parameter capability.");
        return new(bytes,module.Sha256,Convert.ToHexString(SHA256.HashData(bytes)),0,0,0,0,position,0,returns);
    }
}
