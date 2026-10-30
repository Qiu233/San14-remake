using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual named IES map and its ordered profile declarations.
/// Owns the generated image once for this resource generation; a current
/// shader binding does not reload resources or regenerate its texture.</summary>
public sealed class G1EffectIesTextureSource
{
    private sealed record ImageOrigin(RenderResourceDescriptor Map,
        IReadOnlyList<RenderResourceDescriptor?> Profiles) : G1EffectGpuTextureSource;
    private sealed record Profile(RenderResourceDescriptor Descriptor, int VerticalCount,
        int HorizontalCount, float Scale, byte VerticalType, byte HorizontalType, float[] Candela)
    {
        internal (int Width, int Height, float[] Values) PrepareLattice()
        {
            int width = VerticalType switch { 0 => VerticalCount, 1 => checked(VerticalCount * 2 - 1),
                _ => throw new NotSupportedException("IES vertical-angle data type has no prepared image.") };
            int horizontalInterval = HorizontalCount > 1 ? HorizontalCount - 1 : HorizontalCount;
            int height = HorizontalType switch { 0 => horizontalInterval, 1 => checked(horizontalInterval * 2),
                2 => checked(horizontalInterval * 4),
                _ => throw new NotSupportedException("IES horizontal-angle data type has no prepared image.") };
            if (width <= 0 || height <= 0)
                throw new InvalidDataException("IES profile has no positive source lattice.");
            var values = new float[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                int row = y < horizontalInterval ? y : y < horizontalInterval * 2
                    ? horizontalInterval * 2 - y : y < horizontalInterval * 3
                    ? y - horizontalInterval * 2 : horizontalInterval * 4 - y;
                if ((uint)row >= (uint)HorizontalCount)
                    throw new InvalidDataException("IES mirrored lattice row is outside its declared candela data.");
                Array.Copy(Candela, checked(row * VerticalCount), values, checked(y * width), VerticalCount);
                // The source pads the unrecorded vertical hemisphere with zero;
                // it does not mirror that axis.
            }
            return (width, height, values);
        }
    }

    private readonly Profile?[] profiles;
    private readonly uint[] profileNames;
    private readonly byte[] sampler;
    private readonly ImageOrigin origin;
    private G1EffectGpuTexture? prepared;

    private G1EffectIesTextureSource(RenderResourceDescriptor map, int width, int height,
        uint[] profileNames, Profile?[] profiles)
    {
        Map = map; Width = width; Height = height;
        this.profileNames = profileNames; this.profiles = profiles;
        origin = new(map, Array.AsReadOnly(profiles.Select(profile => profile?.Descriptor).ToArray()));
        // The one-mip wrapper constructor's W address survives the source
        // IES post-writes; U clamps, V wraps, filtering is linear.
        Span<byte> key = stackalloc byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(key, 0x9411);
        BinaryPrimitives.WriteUInt16LittleEndian(key[2..], 0x30);
        BinaryPrimitives.WriteUInt32LittleEndian(key[4..], 0);
        sampler = G1TextureSamplerReader.DecodeKey(key).EncodeD3D11Descriptor();
    }

    public RenderResourceDescriptor Map { get; }
    public int Width { get; }
    public int Height { get; }
    public int LayerCount => profiles.Length;

    public static G1EffectIesTextureSource Load(RenderResourceContext context, uint name)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (name == 0) throw new ArgumentOutOfRangeException(nameof(name));
        var map = context.Lookup(name) ?? throw new InvalidDataException("Current Camera IES map is not registered.");
        RequireClass(map, 0x5B50E299, "IES map");
        int width = I32(map.Object, 0xB809B0DA, 4), height = I32(map.Object, 0xAFA78DC7, 4);
        var list = Field(map.Object, 0xFE042EEF, 5, 4);
        if (width <= 0 || height <= 0 || list.ElementCount == 0)
            throw new InvalidDataException("Current Camera IES map requires positive dimensions and profile layers.");
        var names = new uint[list.ElementCount]; var profiles = new Profile?[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            uint profileName = names[i] = BinaryPrimitives.ReadUInt32LittleEndian(list.RawValue.AsSpan(i * 4));
            var descriptor = context.Lookup(profileName);
            // The original loader retains absent named handles as null; the
            // generator explicitly clears their output layer, preserving order.
            if (descriptor is null) continue;
            RequireClass(descriptor, 0xD7441E4E, "IES profile");
            var value = descriptor.Object;
            int vertical = I32(value, 0x94EC0490, 5), horizontal = I32(value, 0xEF5CBE7E, 5);
            var data = Field(value, 0x75FB1E92, 8, 4);
            if (vertical <= 0 || horizontal <= 0 || data.ElementCount != checked(vertical * horizontal))
                throw new InvalidDataException("IES candela array does not match its declared angle dimensions.");
            var candela = new float[data.ElementCount];
            for (int k = 0; k < candela.Length; k++)
                candela[k] = BinaryPrimitives.ReadSingleLittleEndian(data.RawValue.AsSpan(k * 4));
            profiles[i] = new(descriptor, vertical, horizontal,
                BinaryPrimitives.ReadSingleLittleEndian(One(value, 0xAD62E684, 8, 4).RawValue),
                One(value, 0xD90AF21F, 1, 1).RawValue[0],
                One(value, 0xA776844D, 1, 1).RawValue[0], candela);
        }
        return new(map, width, height, names, profiles);
    }

    /// <summary>Called on namespace revision changes, not for each parameter
    /// read or frame. Unrelated registrations preserve this retained owner.</summary>
    public bool IsCurrent(RenderResourceContext context)
    {
        if (!ReferenceEquals(context.Lookup(Map.Object.ObjectId), Map)) return false;
        for (int i = 0; i < profiles.Length; i++)
            if (!ReferenceEquals(context.Lookup(profileNames[i]), profiles[i]?.Descriptor)) return false;
        return true;
    }

    public G1EffectGpuTexture PrepareTexture()
    {
        if (prepared is not null) return prepared;
        var mips = new G1EffectGpuTextureMip[profiles.Length];
        for (int slice = 0; slice < profiles.Length; slice++)
        {
            float[] values;
            if (profiles[slice] is { } profile)
            {
                var lattice = profile.PrepareLattice();
                values = IesProfileResampler.Resample(lattice.Values, lattice.Width, lattice.Height,
                    profile.Scale, Width, Height);
            }
            else values = new float[checked(Width * Height)];
            var bytes = new byte[checked(values.Length * 4)];
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
            mips[slice] = new(0, Width, Height, checked(Width * 4), bytes.Length, bytes) { ArraySlice = slice };
        }
        var formats = RenderTextureFormatMath.Resolve(2, 0, 0);
        // Publish only after every ordered layer has been generated. The
        // effect encoder assigns its own stable upload ID by object identity.
        return prepared = new(0, -1, origin, Width, Height, formats.ResourceFormat, formats.ShaderViewFormat,
            Array.AsReadOnly(mips), sampler) { ArraySize = profiles.Length, ViewDimension = 5 };
    }

    private static void RequireClass(RenderResourceDescriptor descriptor, uint id, string role)
    {
        if (descriptor.Object.Kind != DokObjectKind.Inline || descriptor.Object.ClassId != id)
            throw new NotSupportedException("Current " + role + " requires its actual supported inline declaration.");
    }
    private static DokProperty Field(DokObject value, uint name, uint type, int size)
    {
        var field = value.Properties.Single(p => p.NameHash == name);
        if (field.TypeCode != type || field.ElementSize != size || field.RawValue.Length != checked(field.ElementCount * size))
            throw new InvalidDataException("IES resource field has another format.");
        return field;
    }
    private static DokProperty One(DokObject value, uint name, uint type, int size)
    {
        var field = Field(value, name, type, size);
        if (field.ElementCount != 1) throw new InvalidDataException("IES scalar resource field has another element count.");
        return field;
    }
    private static int I32(DokObject value, uint name, uint type) =>
        BinaryPrimitives.ReadInt32LittleEndian(One(value, name, type, 4).RawValue);
}
