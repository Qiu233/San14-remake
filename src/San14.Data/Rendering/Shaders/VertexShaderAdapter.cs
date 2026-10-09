using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AdaptedVertexShader(byte[] Bytes, string InputSha256, string AdaptedSha256,
    int BaseVertex, int FirstVertex, uint FirstInstance, uint RemovedBaseVariableId,
    uint RasterPositionVariableId, int ReplacedBaseLoads, int AdaptedReturns);

/// <summary>Finite backend adaptation for an own zero-base draw. Substitutes the
/// proven zero BaseVertex, removes that extension dependency, and flips only the
/// final raster position Y for Godot's positive-height viewport. Original vertex
/// mathematics, descriptor contents and interpolants are retained.</summary>
public static class VertexShaderAdapter
{
    /// <summary>Viewport adaptation for a source vertex-buffer program without
    /// VertexID/BaseVertex. Only the final raster Y changes; varying outputs and
    /// descriptors remain untouched.</summary>
    public static AdaptedVertexShader AdaptPositionInput(ReadOnlySpan<byte> source)
    {
        var module=SpirvShaderInterfaceReader.Read(source);
        if(module.EntryPoints.Count!=1||module.EntryPoints[0].ExecutionModel!=0||module.EntryPoints[0].Name!="main"||
            module.Capabilities.Contains(4427u)||module.StageVariables.Any(v=>v.BuiltIn is 4424 or 4425 or 4426))
            throw new InvalidDataException("Position-input adapter requires Vertex/main without draw-parameter builtins.");
        var positions=module.StageVariables.Where(v=>v.StorageClass==3&&v.BuiltIn==0).ToArray();
        if(positions.Length!=1||positions[0].Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}})
            throw new InvalidDataException("Position-input adapter requires one float4 raster position.");
        uint[] words=new uint[source.Length/4];
        for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(source[(i*4)..]);
        var instructions=new List<Instruction>();
        for(int at=5;at<words.Length;)
        {int length=(int)(words[at]>>16);instructions.Add(new((ushort)words[at],words.AsSpan(at,length).ToArray()));at+=length;}
        uint positionId=positions[0].VariableId;
        uint pointer=instructions.Single(i=>i.Opcode==59&&i.Words[2]==positionId).Words[1];
        uint vector=instructions.Single(i=>i.Opcode==32&&i.Words[1]==pointer).Words[3];
        uint scalar=instructions.Single(i=>i.Opcode==23&&i.Words[1]==vector).Words[2];
        uint next=module.IdBound,loaded=next++,y=next++,negative=next++,changed=next++;
        static uint[] Op(ushort opcode,params uint[] operands)=>
            new[]{checked((uint)(operands.Length+1)<<16)|opcode}.Concat(operands).ToArray();
        var output=new List<uint>(words.Length+24);output.AddRange(words.AsSpan(0,5).ToArray());
        uint function=0;int returns=0;
        foreach(var instruction in instructions)
        {
            if(instruction.Opcode==54)function=instruction.Words[2];
            if(instruction.Opcode==253&&function==module.EntryPoints[0].FunctionId)
            {
                output.AddRange(Op(61,vector,loaded,positionId));output.AddRange(Op(81,scalar,y,loaded,1));
                output.AddRange(Op(127,scalar,negative,y));output.AddRange(Op(82,vector,changed,negative,loaded,1));
                output.AddRange(Op(62,positionId,changed));returns++;
            }
            output.AddRange(instruction.Words);if(instruction.Opcode==56)function=0;
        }
        if(returns!=1)throw new InvalidDataException("Position-input adapter requires one main return.");
        output[3]=next;byte[] adapted=new byte[output.Count*4];
        for(int i=0;i<output.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i*4,4),output[i]);
        var checkedInterface=SpirvShaderInterfaceReader.Read(adapted);
        if(checkedInterface.Descriptors.Count!=module.Descriptors.Count||checkedInterface.StageVariables.Count!=module.StageVariables.Count)
            throw new InvalidDataException("Position-input adapter changed the original interface.");
        return new(adapted,module.Sha256,Convert.ToHexString(SHA256.HashData(adapted)),0,0,0,0,positionId,0,returns);
    }

    private sealed record Instruction(ushort Opcode, uint[] Words);

    public static AdaptedVertexShader Adapt(ReadOnlySpan<byte> source,
        int baseVertex, int firstVertex, uint firstInstance)
    {
        if (baseVertex != 0 || firstVertex != 0 || firstInstance != 0)
            throw new InvalidDataException("Vertex adapter requires an explicitly zero-base draw contract.");
        SpirvShaderInterface module = SpirvShaderInterfaceReader.Read(source);
        if (module.EntryPoints[0].ExecutionModel != 0 || module.EntryPoints[0].Name != "main")
            throw new InvalidDataException("Vertex adapter requires one Vertex/main entry point.");
        var bases = module.StageVariables.Where(v => v.BuiltIn is 4424 or 4425 or 4426).ToArray();
        if (bases.Length != 1 || bases[0].BuiltIn != 4424 || bases[0].StorageClass != 1 ||
            bases[0].Type is not { Kind: "int", Bits: 32, Signed: true } ||
            !module.Capabilities.Contains(4427u) ||
            !module.Extensions.Contains("SPV_KHR_shader_draw_parameters"))
            throw new InvalidDataException("Vertex adapter requires the exact single BaseVertex draw-parameter interface.");
        var positions = module.StageVariables.Where(v => v.StorageClass == 3 && v.BuiltIn == 0).ToArray();
        if (positions.Length != 1 || positions[0].Type is not { Kind: "vector", ComponentCount: 4,
            ComponentType: { Kind: "float", Bits: 32 } })
            throw new InvalidDataException("Vertex adapter requires one float4 raster-position output.");
        uint baseId = bases[0].VariableId, positionId = positions[0].VariableId;
        uint[] words = new uint[source.Length / 4];
        for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(source[(i * 4)..]);
        var instructions = new List<Instruction>();
        for (int at = 5; at < words.Length;)
        {
            int length = (int)(words[at] >> 16);
            instructions.Add(new((ushort)words[at], words.AsSpan(at, length).ToArray())); at += length;
        }
        Instruction Variable(uint id) => instructions.Single(i => i.Opcode == 59 && i.Words[2] == id);
        Instruction Type(uint id) => instructions.Single(i => i.Opcode is >= 19 and <= 33 && i.Words[1] == id);
        uint basePointer = Variable(baseId).Words[1];
        uint integerType = Type(basePointer).Words[3];
        uint positionPointer = Variable(positionId).Words[1];
        uint vectorType = Type(positionPointer).Words[3], floatType = Type(vectorType).Words[2];
        if (Type(basePointer).Opcode != 32 || Type(positionPointer).Opcode != 32)
            throw new InvalidDataException("Vertex adapter pointer types are inconsistent.");
        uint nextId = module.IdBound;
        uint Allocate() => checked(nextId++);
        uint zero = instructions.FirstOrDefault(i => i.Opcode == 43 && i.Words.Length == 4 &&
            i.Words[1] == integerType && i.Words[3] == 0)?.Words[2] ?? 0;
        bool addZero = zero == 0; if (addZero) zero = Allocate();
        uint loadedPosition = Allocate(), y = Allocate(), negativeY = Allocate(), changedPosition = Allocate();
        var output = new List<uint>(words.Length + 32); output.AddRange(words.AsSpan(0, 5).ToArray());
        uint function = 0; int replaced = 0, returns = 0, declarations = 0;
        bool emittedZero = false;
        static uint[] Op(ushort opcode, params uint[] operands) =>
            new[] { checked((uint)(operands.Length + 1) << 16) | opcode }.Concat(operands).ToArray();
        foreach (Instruction instruction in instructions)
        {
            uint[] w = instruction.Words;
            if (instruction.Opcode == 17 && w[1] == 4427) continue;
            if (instruction.Opcode == 10)
            {
                byte[] text = new byte[(w.Length - 1) * 4];
                for (int i = 1; i < w.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(text.AsSpan((i - 1) * 4), w[i]);
                if (Encoding.UTF8.GetString(text).TrimEnd('\0') == "SPV_KHR_shader_draw_parameters") continue;
            }
            if (instruction.Opcode == 15)
            {
                int start = 3;
                while (start < w.Length)
                {
                    uint textWord = w[start++];
                    if ((textWord & 0xff) == 0 || (textWord & 0xff00) == 0 ||
                        (textWord & 0xff0000) == 0 || (textWord & 0xff000000) == 0) break;
                }
                var entry = w.Take(start).Concat(w.Skip(start).Where(id => id != baseId)).ToArray();
                entry[0] = (uint)entry.Length << 16 | 15u; output.AddRange(entry); continue;
            }
            if (instruction.Opcode is 5 or 71 && w[1] == baseId) continue;
            if (instruction.Opcode == 59 && w[2] == baseId) { declarations++; continue; }
            if (instruction.Opcode == 54)
            {
                function = w[2];
                if (addZero && !emittedZero) { output.AddRange(Op(43, integerType, zero, 0)); emittedZero = true; }
            }
            if (instruction.Opcode == 61 && w.Length == 4 && w[3] == baseId)
            {
                if (w[1] != integerType) throw new InvalidDataException("BaseVertex load type differs from the declared integer.");
                output.AddRange(Op(83, w[1], w[2], zero)); replaced++; continue;
            }
            // OpLine's line/column are literals, not variable references.
            if (function != 0 && instruction.Opcode is not (54 or 8 or 317) && w.AsSpan(1).Contains(baseId))
                throw new InvalidDataException("BaseVertex has a use outside the verified direct-load contract.");
            if (instruction.Opcode == 253 && function == module.EntryPoints[0].FunctionId)
            {
                output.AddRange(Op(61, vectorType, loadedPosition, positionId));
                output.AddRange(Op(81, floatType, y, loadedPosition, 1));
                output.AddRange(Op(127, floatType, negativeY, y));
                output.AddRange(Op(82, vectorType, changedPosition, negativeY, loadedPosition, 1));
                output.AddRange(Op(62, positionId, changedPosition)); returns++;
            }
            output.AddRange(w);
            if (instruction.Opcode == 56) function = 0;
        }
        if (declarations != 1 || replaced != 1 || returns != 1 || (addZero && !emittedZero))
            throw new InvalidDataException("Vertex adapter requires one BaseVertex declaration/load and one main return.");
        output[3] = nextId;
        byte[] adapted = new byte[checked(output.Count * 4)];
        for (int i = 0; i < output.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(adapted.AsSpan(i * 4), output[i]);
        SpirvShaderInterface checkedInterface = SpirvShaderInterfaceReader.Read(adapted);
        if (checkedInterface.Capabilities.Contains(4427u) || checkedInterface.StageVariables.Any(v => v.VariableId == baseId) ||
            checkedInterface.Descriptors.Count != module.Descriptors.Count)
            throw new InvalidDataException("Adapted vertex interface did not preserve the bounded backend contract.");
        return new(adapted, module.Sha256, Convert.ToHexString(SHA256.HashData(adapted)),
            baseVertex, firstVertex, firstInstance, baseId, positionId, replaced, returns);
    }
}
