using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Iced.Intel;

namespace San14.Data;

public static partial class OriginalDirectionalLightConstruction
{
    // This owner retains decoded operations, not an installed file or process handle.
    // A source load is shared by the selected seasons and their constructed actors.
    private static readonly ConcurrentDictionary<string,Lazy<Circuit>> LoadedCircuits=new(StringComparer.OrdinalIgnoreCase);
    private static Circuit LoadCircuit(OriginalMapSeasonDirectionalLightSourceSet source)
    {
        string directory=Path.GetDirectoryName(Path.GetDirectoryName(source.Source.IndexSource.PhysicalPath))
            ??throw new InvalidDataException("Directional source container has no installation directory.");
        string path=Path.GetFullPath(Path.Combine(directory,"SAN14CE_TC.exe"));
        return LoadedCircuits.GetOrAdd(path,p=>new(()=>ReadInstalledCircuit(p))).Value;
    }

    private static Circuit ReadInstalledCircuit(string path)
    {
        byte[] exe=File.ReadAllBytes(path);
        if(Convert.ToHexString(SHA256.HashData(exe))!="0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464")
            throw new InvalidDataException("Directional executable source revision differs.");
        var image=new OriginalDeferredSamplerSources.NativeImage(exe);
        byte[] text=image.DecodeText(out int textRva);
        if(Convert.ToHexString(SHA256.HashData(text))!="FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766")
            throw new InvalidDataException("Directional decoded source revision differs.");
        byte[] ReadSource(ulong address,int count)
        {
            int rva=checked((int)(address-Image));
            if(rva>=textRva&&rva-textRva<=text.Length-count)
                return text.AsSpan(rva-textRva,count).ToArray();
            return image.Raw(rva,count).ToArray();
        }
        // Function boundaries are format/profile metadata. Their instruction and
        // constant contents come exclusively from the user's installed executable.
        (int Start,int End)[] ranges=[
            (0xCDBD10,0xCDBEC0),(0xCB65D0,0xCB67E3),(0xC0F4D0,0xC0F4EA),
            (0xCBFAB0,0xCC4DF4),(0xCB9CC0,0xCBA225),(0xCE4A00,0xCE4AF5),
            (0xCBA530,0xCBC465),(0xC0FE40,0xC0FED5),(0xC0FEE0,0xC0FF75),
            (0x16700,0x16886),(0xCBC6D0,0xCBC768),
            (0xCB6510,0xCB65CF),(0xCDCA90,0xCE097E),(0xABD4A0,0xABD6F4),
            (0xA71A70,0xA71E7E),(0xCE6180,0xCE6184),
            (0xCD1C70,0xCDA214),(0xC3F0D0,0xC3F0F6),(0xC3F2F0,0xC3F30E)];
        var instructions=new List<Instruction>();var constants=new Dictionary<ulong,string>();
        foreach(var range in ranges)
        {
            var decoder=Decoder.Create(64,text.AsSpan(range.Start-textRva,range.End-range.Start).ToArray(),Image+(ulong)range.Start);
            while(decoder.IP<Image+(ulong)range.End)
            {
                decoder.Decode(out var native);
                if(native.Code==Code.INVALID||native.NextIP>Image+(ulong)range.End)
                    throw new InvalidDataException("Directional source function is not a complete x64 instruction range.");
                var instruction=DecodeInstruction(native);instructions.Add(instruction);
                if(instruction.Name=="lea")continue;
                foreach(var p in instruction.Ops.Where(p=>p.Kind==3&&p.Base==-2))
                {
                    ulong at=unchecked(Image+(ulong)(instruction.Rva+instruction.Length)+(ulong)p.Disp);
                    constants[at]=Convert.ToHexString(ReadSource(at,p.Size));
                }
            }
        }
        ulong callback=Image+0x16BDA20+0x430;
        constants[callback]=Convert.ToHexString(ReadSource(callback,8));
        var circuit=new Circuit(instructions.ToArray(),constants.Select(c=>new Constant(c.Key,c.Value)).ToArray(),[]);
        // The descriptor switch writes its own actual 561 records into our memory.
        // Neither a copied descriptor list nor a development oracle ships in Data.
        var enumerator=new Machine(circuit,[],new(0,"native descriptor load",[]),new byte[LightDataBytes],readSource:ReadSource);
        return circuit with {Fields=enumerator.ReadNativeFields()};
    }

    private static Instruction DecodeInstruction(Iced.Intel.Instruction native)
    {
        string name=native.Mnemonic.ToString().ToLowerInvariant();
        if(native.HasLockPrefix)name="lock "+name;
        Operand[] operands=new Operand[native.OpCount];
        int DestinationSize()=>native.Op0Kind==OpKind.Register?native.Op0Register.GetSize():native.MemorySize.GetSize();
        for(int i=0;i<operands.Length;i++)
        {
            OpKind kind=native.GetOpKind(i);
            if(kind==OpKind.Register)
            {
                Register r=native.GetOpRegister(i);int size=r.GetSize(),reg=RegisterIndex(r);
                operands[i]=new(1,size,reg,size==4&&reg<16,0,-1,-1,0,0);
            }
            else if(kind==OpKind.Memory)
            {
                int size=native.MemorySize.GetSize();if(size==0)size=DestinationSize();
                long displacement=native.IsIPRelativeMemoryOperand
                    ?unchecked((long)(native.IPRelativeMemoryAddress-native.NextIP)):unchecked((long)native.MemoryDisplacement64);
                operands[i]=new(3,size,-1,false,0,RegisterIndex(native.MemoryBase),RegisterIndex(native.MemoryIndex),
                    native.MemoryIndexScale,displacement);
            }
            else if(kind==OpKind.NearBranch64)
                operands[i]=new(2,8,-1,false,native.NearBranch64,-1,-1,0,0);
            else if(kind is OpKind.Immediate8 or OpKind.Immediate16 or OpKind.Immediate32 or OpKind.Immediate64 or
                OpKind.Immediate8to16 or OpKind.Immediate8to32 or OpKind.Immediate8to64 or OpKind.Immediate32to64)
            {
                int size=name is "shufps" or "btr" or "shl"?1:DestinationSize();
                operands[i]=new(2,size,-1,false,native.GetImmediate(i),-1,-1,0,0);
            }
            else throw new NotSupportedException("Directional source operand kind "+kind);
        }
        return new(checked((int)(native.IP-Image)),native.Length,name,operands);
    }

    private static int RegisterIndex(Register register)
    {
        if(register==Register.None)return -1;if(register is Register.RIP or Register.EIP)return -2;
        if(register is >=Register.XMM0 and <=Register.XMM15)return 16+register-Register.XMM0;
        Register full=register.GetFullRegister();
        if(full is >=Register.RAX and <=Register.R15)return full-Register.RAX;
        throw new NotSupportedException("Directional source register "+register);
    }

    private sealed partial class Machine
    {
        public Field[] ReadNativeFields()
        {
            const ulong output=Aux+0x8000;
            trackSeed=false;Set(2,output,8,true);Set(8,561,4,true);Set(9,0,4,true);
            Run(0xCD1C70);byte[] raw=Read(output,561*24);
            return Enumerable.Range(0,561).Select(i=>
            {
                uint format=BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i*24,4));
                return new Field(i,BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i*24+4,4)),
                    (format>>24)&15,(int)(format&0xFFFFFF));
            }).ToArray();
        }
    }
}
