using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

/// <summary>Source gauge UV curves and endpoint/constant alpha samples.
/// Keys come from the layout; no sampled original execution is stored.</summary>
internal sealed class UiSourceGaugeCurve
{
    private sealed record Key(float Time,float OutX,float OutY,float InX,float InY,float OutValue,float InValue);
    private readonly Key[] keys;
    private readonly float defaultValue,minimum,maximum;
    private readonly bool endpointOnly;
    private readonly bool staticUvEndpoint;
    private UiSourceGaugeCurve(byte[] bytes,float defaultValue,float minimum,float maximum,bool endpointOnly,
        bool staticUvEndpoint=false)
    {
        this.staticUvEndpoint=staticUvEndpoint;
        this.endpointOnly=endpointOnly;
        this.defaultValue=defaultValue;this.minimum=minimum;this.maximum=maximum;
        if(bytes.Length%64!=0||!float.IsFinite(defaultValue)||float.IsNaN(minimum)||float.IsNaN(maximum)||minimum>maximum)
            throw new InvalidDataException("Invalid gauge curve key boundary or limits.");
        keys=new Key[bytes.Length/64];
        for(int i=0;i<keys.Length;i++)
        {
            int offset=i*64;
            float Read(int field)=>(float)BinaryPrimitives.ReadDoubleLittleEndian(bytes.AsSpan(offset+field*8,8));
            float[] v=Enumerable.Range(0,7).Select(Read).ToArray();
            if(v.Any(f=>!float.IsFinite(f))||bytes[offset+56]!=3 && !(staticUvEndpoint&&bytes[offset+56]==2))
                throw new InvalidDataException("Unsupported common gauge curve mode.");
            keys[i]=new(v[0],v[1],v[2],v[3],v[4],v[5],v[6]);
            if(i>0)
            {
                var a=keys[i-1];var b=keys[i];
                if(!(a.Time<b.Time) || !endpointOnly &&
                    !(a.Time<=a.Time+a.OutX&&a.Time+a.OutX<=b.Time+b.InX&&b.Time+b.InX<=b.Time))
                    throw new InvalidDataException("Gauge curve requires a monotone time span.");
            }
        }
    }

    internal static UiSourceGaugeCurve ForNode(LcskDocument document,int node)
        => ReadChannel(document,node,0x2D);

    internal static UiSourceGaugeCurve ForAlphaNode(LcskDocument document,int node)
        => ReadChannel(document,node,0x0A);

    // The diplomacy gauge seeks its UV translation action to its end once.
    // Its mode2 first key is admitted only for this static endpoint reader.
    internal static UiSourceGaugeCurve ForUvOffsetNode(LcskDocument document,int node,bool vertical=false)
        => ReadChannel(document,node,vertical?(byte)0x2A:(byte)0x29,true);

    private static UiSourceGaugeCurve ReadChannel(LcskDocument document,int node,byte property,bool staticUvEndpoint=false)
    {
        if(document.Signature is not ("LCSK8500" or "LCSK9500"))throw new InvalidDataException("Unsupported gauge animation version.");
        var bindings=document.Blocks.Single(b=>b.Tag==2);
        if(bindings.Kind!=1||bindings.RecordBytes.Length!=bindings.RecordCount*12)
            throw new InvalidDataException("Invalid gauge animation bindings.");
        int? curve=null;
        for(int i=0;i<bindings.RecordCount;i++)
        {
            var r=bindings.RecordBytes.AsSpan(i*12,12);
            if(BinaryPrimitives.ReadUInt16LittleEndian(r[4..])!=node)continue;
            if(r[8]!=0 || r[7] is not (0x2D or 0x0A or 0x29 or 0x2A))
                throw new InvalidDataException("Unsupported gauge UV binding.");
            if(r[7]!=property)continue;
            if(curve.HasValue)throw new InvalidDataException("Duplicate gauge channel binding.");
            curve=BinaryPrimitives.ReadInt32LittleEndian(r);
        }
        var block=document.Blocks.Single(b=>b.Tag==9);
        var pool=block.Pools.Single(p=>p.Type==3);
        if(block.Kind!=1||block.RecordBytes.Length!=block.RecordCount*32||
            curve is not int index||index<0||index>=block.RecordCount)
            throw new InvalidDataException("Gauge UV curve is missing.");
        var record=block.RecordBytes.AsSpan(index*32,32);
        int entry=BinaryPrimitives.ReadUInt16LittleEndian(record[24..]);
        if(entry>=pool.Entries.Count)throw new InvalidDataException("Gauge key pool reference is missing.");
        return new(pool.Entries[entry].Data,(float)BinaryPrimitives.ReadDoubleLittleEndian(record),
            (float)BinaryPrimitives.ReadDoubleLittleEndian(record[8..]),(float)BinaryPrimitives.ReadDoubleLittleEndian(record[16..]),
            property==0x0A||staticUvEndpoint,staticUvEndpoint);
    }

    internal static (int Start,int End,int[] Nodes) ReadAction(LcskDocument document,string name)
    {
        var actions=document.Blocks.Single(b=>b.Tag==4);var groups=document.Blocks.Single(b=>b.Tag==3);
        if(document.Signature is not ("LCSK8500" or "LCSK9500")||actions.Kind!=2||groups.Kind!=2||
            actions.RecordBytes.Length!=actions.RecordCount*44||groups.RecordBytes.Length!=groups.RecordCount*4)
            throw new InvalidDataException("Unsupported gauge action/group format.");
        byte[] Entry(LcskBlock block,int type,int index)
        {
            var pool=block.Pools.Single(p=>p.Type==type);
            if(index<0||index>=pool.Entries.Count)throw new InvalidDataException("Gauge action pool reference is missing.");
            return pool.Entries[index].Data;
        }
        int[] Words(byte[] bytes)
        {
            if(bytes.Length%2!=0)throw new InvalidDataException("Invalid gauge pane/group list.");
            return Enumerable.Range(0,bytes.Length/2).Select(i=>(int)BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i*2,2))).ToArray();
        }
        for(int i=0;i<actions.RecordCount;i++)
        {
            var record=actions.RecordBytes.AsSpan(i*44,44);
            if(Encoding.UTF8.GetString(Entry(actions,9,BinaryPrimitives.ReadUInt16LittleEndian(record)))!=name)continue;
            int start=BinaryPrimitives.ReadUInt16LittleEndian(record[8..]),end=BinaryPrimitives.ReadUInt16LittleEndian(record[10..]);
            if(start>end)throw new InvalidDataException("Invalid gauge action range.");
            int reference=BinaryPrimitives.ReadUInt16LittleEndian(record[2..]);
            var panes=new List<int>();
            if(reference!=ushort.MaxValue)foreach(int group in Words(Entry(actions,10,reference)))
            {
                if(group>=groups.RecordCount)throw new InvalidDataException("Gauge group is missing.");
                int list=BinaryPrimitives.ReadUInt16LittleEndian(groups.RecordBytes.AsSpan(group*4+2,2));
                panes.AddRange(Words(Entry(groups,8,list)));
            }
            return(start,end,panes.ToArray());
        }
        throw new InvalidDataException("Gauge action is missing.");
    }

    internal float Evaluate(float time)
    {
        if(!float.IsFinite(time))throw new ArgumentOutOfRangeException(nameof(time));
        if(keys.Length==0)return Math.Clamp(defaultValue,minimum,maximum);
        time=Math.Clamp(time,0,keys[^1].Time);
        if(time<=keys[0].Time)return Math.Clamp(keys[0].OutValue,minimum,maximum);
        if(time>=keys[^1].Time)return Math.Clamp(keys[^1].InValue,minimum,maximum);
        if(staticUvEndpoint)throw new InvalidOperationException("This source UV curve consumer only seeks static endpoints.");
        for(int i=1;i<keys.Length;i++)
        {
            var b=keys[i];if(time>b.Time)continue;
            var a=keys[i-1];
            if(time<=a.Time)return Math.Clamp(a.OutValue,minimum,maximum);
            if(time>=b.Time)return Math.Clamp(b.InValue,minimum,maximum);
            if(endpointOnly)
            {
                // Alpha's time handles may be nonmonotone. Constant Y is
                // independent of that solver; other interior samples are not
                // supported by this finite static-gauge consumer.
                if(a.OutValue!=b.InValue||a.OutY!=0||b.InY!=0)
                    throw new InvalidDataException("Gauge alpha requires an unsupported nonconstant interior sample.");
                return Math.Clamp(a.OutValue,minimum,maximum);
            }
            // Monotone x has one legal root. Bisection solves the resource's
            // cubic, rather than linearly interpolating its rotation values.
            double low=0,high=1;
            for(int n=0;n<48;n++)
            {
                double u=(low+high)*.5;
                if(Cubic(a.Time,(double)a.Time+a.OutX,(double)b.Time+b.InX,b.Time,u)<time)low=u;else high=u;
            }
            return Math.Clamp((float)Cubic(a.OutValue,(double)a.OutValue+a.OutY,(double)b.InValue+b.InY,b.InValue,(low+high)*.5),minimum,maximum);
        }
        throw new InvalidDataException("Gauge animation span is missing.");
    }
    internal byte EvaluateAlphaByte(float time)
    {
        if(!endpointOnly)throw new InvalidOperationException("Gauge alpha requires its alpha channel.");
        return (byte)Math.Clamp((double)Evaluate(time)*255,0,255);
    }
    private static double Cubic(double a,double b,double c,double d,double u)
    {double v=1-u;return v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;}
}
