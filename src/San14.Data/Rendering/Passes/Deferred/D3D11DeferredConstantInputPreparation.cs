using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11DeferredConstantPacket(byte[] Bytes,
    IReadOnlyList<string> NamedFields, IReadOnlyList<string> CameraWriteLayout);

/// <summary>Full named-field initialization from typed scene values. Only
/// undeclared buffer gaps and a proven unread PS383 domain are normalized.</summary>
public static class D3D11DeferredConstantInputPreparation
{
    private static readonly string[] CameraNames = ["vEye","mW2P","mWV","mV2W","mP2W","mP2V"];

    public static D3D11DeferredConstantPacket Prepare(
        DxbcResourceDefinitions canonical, DxbcResourceDefinitions selected,
        DeferredSceneInputs scene, LoadedDeferredEarlyCausticsSource caustics,
        bool normalizeUnread383)
    {
        var globals = selected.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        var canonicalGlobals = canonical.Buffers.Single(buffer => buffer.Name.Text == "$Globals");
        // Hatching's reset IEEE words are copied, not evaluated as finite math.
        var computed = DeferredSceneConstants.Apply(canonical,
            new byte[DeferredSceneConstants.ByteSize], scene with {Hatching = null});
        byte[] bytes = new byte[globals.ByteSize];
        HashSet<string> supplied = [];
        List<string> cameraWrites = [];
        var active = normalizeUnread383 ? DeferredSelected383Constants.ActiveScalarByteOffsets.ToHashSet() : null;
        foreach (var target in globals.Variables)
        {
            var source = canonicalGlobals.Variables.SingleOrDefault(variable => variable.Name.Text == target.Name.Text);
            if (source is null || !computed.Writes.Any(write => write.Name == target.Name.Text)) continue;
            if (source.ByteSize != target.ByteSize)
                throw new InvalidDataException("Deferred field extent differs: " + target.Name.Text);
            var a = canonical.Types.Single(type => type.Offset == source.TypeOffset);
            var b = selected.Types.Single(type => type.Offset == target.TypeOffset);
            if (a.Class != b.Class || a.BaseType != b.BaseType || a.Rows != b.Rows || a.Columns != b.Columns || a.Elements != b.Elements)
                throw new InvalidDataException("Deferred field type differs: " + target.Name.Text);
            computed.Bytes.AsSpan(source.StartOffset,source.ByteSize).CopyTo(bytes.AsSpan(target.StartOffset,target.ByteSize));
            supplied.Add(target.Name.Text);
            int kind = Array.IndexOf(CameraNames,target.Name.Text);
            if (kind < 0) continue;
            for (int local=0;local<target.ByteSize;)
            {
                if (active is not null && !active.Contains(target.StartOffset+local)) { local+=4; continue; }
                int begin=local; local+=4;
                while(local<target.ByteSize && (active is null || active.Contains(target.StartOffset+local))) local+=4;
                cameraWrites.Add(FormattableString.Invariant($"{kind} {target.StartOffset+begin} {begin} {local-begin}"));
            }
        }
        Write("vCausticsRange",RenderRuleNumericEncoding.Vectors(Vector4.Zero));
        Write("fCausticsScaler",Float(1));
        Write("mW2Ca",caustics.IdentityBytes);
        Write("fAmbSpcLclOccStp",Float(DeferredRefractionMath.FreshLocalAmbientOcclusionStepScale));
        if (scene.Hatching is { } hatch)
        {
            Write("hsRampPrm",RenderRuleNumericEncoding.Vectors(hatch.Ramp));
            Write("hsAOPrm",RenderRuleNumericEncoding.Vectors(hatch.AmbientOcclusion));
            byte[] rows=new byte[28];
            foreach (var (offset,value) in new[] {(0,hatch.UvRow0.X),(4,hatch.UvRow0.Y),(8,hatch.UvRow0.Z),
                (16,hatch.UvRow1.X),(20,hatch.UvRow1.Y),(24,hatch.UvRow1.Z)})
                BinaryPrimitives.WriteSingleLittleEndian(rows.AsSpan(offset),value);
            Write("hsUVMat",rows);
        }
        string[] missing=globals.Variables.Where(variable=>!supplied.Contains(variable.Name.Text)).Select(variable=>variable.Name.Text).ToArray();
        if (missing.Length!=0) throw new NotSupportedException("Deferred initialization has no typed producer for: "+string.Join(", ",missing));
        if(active is not null)
        {
            if(globals.ByteSize!=DeferredSelected383Constants.ByteSize)
                throw new InvalidDataException("PS383 unread domain belongs to its736-byte layout.");
            for(int offset=0;offset<bytes.Length;offset+=4)
                if(!active.Contains(offset))bytes.AsSpan(offset,4).Clear();
        }
        return new(bytes,supplied.Order(StringComparer.Ordinal).ToArray(),cameraWrites.AsReadOnly());

        void Write(string name,ReadOnlySpan<byte> values)
        {
            var target=globals.Variables.SingleOrDefault(variable=>variable.Name.Text==name);
            if(target is null)return;
            if(values.Length!=target.ByteSize)throw new InvalidDataException("Deferred initializer field extent differs: "+name);
            values.CopyTo(bytes.AsSpan(target.StartOffset,target.ByteSize)); supplied.Add(name);
        }
        static byte[] Float(float value) {byte[] bytes=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(bytes,value);return bytes;}
    }

    public static void WriteNativeInputs(string directory, D3D11DeferredConstantPacket packet)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,"source-ps-b0.bin"),packet.Bytes);
        File.WriteAllLines(Path.Combine(directory,"camera-writes.txt"),packet.CameraWriteLayout);
    }
}
