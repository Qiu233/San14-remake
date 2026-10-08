using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalSkyAtmoRawTable { Transmittance, Ambient2D }

/// <summary>Original StaticSky2AtmoTable declaration and its two complete raw
/// numeric resources. Source loading does not observe a current native wrapper
/// or claim that its asynchronous dependency publication has executed.</summary>
public sealed record LoadedOriginalSkyAtmoTableSources(LoadedObjects Definitions,
    DokObject Declaration, LoadedRdbAsset Transmittance, LoadedRdbAsset Ambient2D,
    uint PlaneStaObjectId, int Width, int Height, uint Flags,
    Vector4 MieExtinction, Vector4 RayleighExtinction, float MieHeight,
    float RayleighHeight, float PlanetRadius, float AtmoTopHeight)
{
    public int RowPitchBytes => checked(Width * 8);
    public bool CurrentOriginalWrapperReadinessObserved => false;
    public string PublicationEvidence => "StaticSky2AtmoTable factory+78 -> TC E49EB0; dependency index0 is " +
        "5BCD082B after B40360 prepend; ready-gated exact-length byte copy publishes CPU10/20; " +
        "index1 publishes 1DA023EB to CPU18/28; successful source route, no current wrapper observation; " +
        OriginalSkyAtmoTableResources.NativeEvidence;
    public string SourceEvidence => $"{Definitions.Source.ResourceUri}@{Definitions.Source.DecodedSha256}" +
        $"#object={Declaration.ObjectId:X8}/class={Declaration.ClassId:X8};" +
        $"TTable={Transmittance.ResourceUri}@{Transmittance.DecodedSha256};" +
        $"Amb2d={Ambient2D.ResourceUri}@{Ambient2D.DecodedSha256};" + OriginalSkyAtmoTableResources.NativeEvidence;

    /// <summary>C99C00 physical X is the height coordinate supplied in XMM2;
    /// physical Y is the angle coordinate supplied in XMM3. Sampling supports
    /// the exact declared64x256 raw half4 data with explicit unit coordinates.</summary>
    public Vector4 SampleGrid(float heightCoordinate, float angleCoordinate,
        OriginalSkyAtmoRawTable table = OriginalSkyAtmoRawTable.Transmittance) =>
        OriginalSkyAtmoTableResources.SampleGrid(this, heightCoordinate, angleCoordinate, table);
}

public static class OriginalSkyAtmoTableResources
{
    public const uint ObjectId = 0x0490C334;
    public const uint ClassId = 0x1AA5B65E;
    public const uint DefinitionFileId = 0xDAB638B2;
    public const string DefinitionSha256 = "357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271";
    public const uint TransmittanceFileId = 0x5BCD082B;
    public const string TransmittanceSha256 = "5F775D6A059141202ED2E50AD8B06E64EFAEDD0B6C296EEDC486F5C5F6D81827";
    public const uint Ambient2DFileId = 0x1DA023EB;
    public const string Ambient2DSha256 = "A732034E8D47474BC68CC37DDAA4C3612A373E8F8925155E7DBF02112CB4BF3E";
    public const uint RawResourceType = 0xD7F47FB1;
    public const string SystemIndexSha256 = "E6852DA6F0AC96918BC925DE68E69F27225FCF0180AA05CB5FA5A1536FFC6075";
    public const int TableByteLength = 131072;
    public const string NativeEvidence = "docs/.local/ce-sky-season-sta-20261002/contract.json";

    public static LoadedOriginalSkyAtmoTableSources Load(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Read(repository.LoadObjects("system", DefinitionFileId),
            repository.LoadRdbAsset("system", TransmittanceFileId), repository.LoadRdbAsset("system", Ambient2DFileId));
    }

    public static LoadedOriginalSkyAtmoTableSources LoadForSeason(OriginalGameRepository repository,
        OriginalMapSeasonCameraPackage package)
    {
        var sampling = OriginalSkySeasonPackageTextures.Read(package);
        var reference = Field(sampling.Package.Camera, 825, 0x063D4BE7, 5, 1);
        if (BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue) != ObjectId)
            throw new InvalidDataException("Selected seasonal Camera declares a different SkylightSTATableMap source.");
        return Load(repository);
    }

    public static LoadedOriginalSkyAtmoTableSources Read(LoadedObjects definitions,
        LoadedRdbAsset transmittance, LoadedRdbAsset ambient2D)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ValidateAsset(definitions.Source, DefinitionFileId, DefinitionSha256, 28336);
        ValidateAsset(transmittance, TransmittanceFileId, TransmittanceSha256, TableByteLength);
        ValidateAsset(ambient2D, Ambient2DFileId, Ambient2DSha256, TableByteLength);
        var document = DokReader.Read(definitions.Source.Bytes);
        var objects = document.Objects.Where(o => o.ObjectId == ObjectId).ToArray();
        if (objects.Length != 1 || objects[0].Kind != DokObjectKind.Inline ||
            objects[0].ClassId != ClassId || objects[0].Properties.Count != 12)
            throw new InvalidDataException("Original Skylight table requires its exact StaticSky2AtmoTable source declaration.");
        var declaration = objects[0];
        uint Key(int ordinal, uint hash, ushort type = 5) =>
            BinaryPrimitives.ReadUInt32LittleEndian(Field(declaration, ordinal, hash, type, 1).RawValue);
        float Scalar(int ordinal, uint hash) => BinaryPrimitives.ReadSingleLittleEndian(Field(declaration, ordinal, hash, 8, 1).RawValue);
        Vector4 Vector(int ordinal, uint hash)
        {
            var raw = Field(declaration, ordinal, hash, 8, 4).RawValue;
            return new(BinaryPrimitives.ReadSingleLittleEndian(raw), BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(4)),
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(8)), BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(12)));
        }
        uint plane = Key(0, 0xE67B59E3), flags = Key(4, 0x05D92ACC);
        int width = checked((int)Key(2, 0xB9AF5BFA, 4)), height = checked((int)Key(3, 0x59FD9627, 4));
        if (plane != 0x98026BD5 || Key(1, 0x4E513F7C) != TransmittanceFileId ||
            Key(10, 0x54489708) != Ambient2DFileId || width != 64 || height != 256 || flags != 1 ||
            checked(width * height * 8) != transmittance.Bytes.Length)
            throw new InvalidDataException("Original table raw-resource keys, flag, dimensions or native half4 stride differ.");
        return new(new(document, definitions.Source), declaration, transmittance, ambient2D,
            plane, width, height, flags, Vector(5, 0xA9D85746), Vector(6, 0x12F68AF8),
            Scalar(7, 0x1B9B45D0), Scalar(8, 0x775522AE), Scalar(9, 0x4680912A), Scalar(11, 0x743793F9));
    }

    public static void Validate(LoadedOriginalSkyAtmoTableSources source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var canonical = Read(source.Definitions, source.Transmittance, source.Ambient2D);
        if (source.Declaration.ObjectId != canonical.Declaration.ObjectId || source.Declaration.ClassId != canonical.Declaration.ClassId ||
            source.Declaration.Kind != canonical.Declaration.Kind || source.Declaration.Offset != canonical.Declaration.Offset ||
            source.Declaration.Length != canonical.Declaration.Length || source.Declaration.Properties.Count != canonical.Declaration.Properties.Count ||
            source.PlaneStaObjectId != canonical.PlaneStaObjectId || source.Width != canonical.Width || source.Height != canonical.Height ||
            source.Flags != canonical.Flags || source.MieExtinction != canonical.MieExtinction ||
            source.RayleighExtinction != canonical.RayleighExtinction || source.MieHeight != canonical.MieHeight ||
            source.RayleighHeight != canonical.RayleighHeight || source.PlanetRadius != canonical.PlanetRadius || source.AtmoTopHeight != canonical.AtmoTopHeight)
            throw new InvalidDataException("Original table retained settings differ from their complete source bytes.");
        foreach (var pair in source.Declaration.Properties.Zip(canonical.Declaration.Properties))
            if (pair.First.Index != pair.Second.Index || pair.First.NameHash != pair.Second.NameHash ||
                pair.First.TypeCode != pair.Second.TypeCode || pair.First.ElementCount != pair.Second.ElementCount ||
                pair.First.ElementSize != pair.Second.ElementSize || !pair.First.RawValue.AsSpan().SequenceEqual(pair.Second.RawValue))
                throw new InvalidDataException("Original table retained typed declaration differs from its complete source bytes.");
    }

    public static Vector4 SampleGrid(LoadedOriginalSkyAtmoTableSources source,
        float heightCoordinate, float angleCoordinate, OriginalSkyAtmoRawTable table = OriginalSkyAtmoRawTable.Transmittance)
    {
        // Read/Load already checks the source declaration and both complete
        // raw tables. Ordinary sampling consumes that loaded generation.
        ArgumentNullException.ThrowIfNull(source);
        if (!float.IsFinite(heightCoordinate) || !float.IsFinite(angleCoordinate) ||
            heightCoordinate is < 0 or > 1 || angleCoordinate is < 0 or > 1 ||
            table is not (OriginalSkyAtmoRawTable.Transmittance or OriginalSkyAtmoRawTable.Ambient2D))
            throw new InvalidDataException("Original half4 sampling requires explicit finite unit height/angle coordinates and an exact source table.");
        float x = Mul(heightCoordinate, source.Width), y = Mul(angleCoordinate, source.Height);
        int rawX = (int)x, rawY = (int)y;
        float fractionX = Sub(x, rawX), fractionY = Sub(y, rawY);
        int baseX = Math.Clamp(rawX, 0, source.Width - 1), baseY = Math.Clamp(rawY, 0, source.Height - 1);
        int nextX = Math.Clamp(rawX + (fractionX >= .5f ? 1 : -1), 0, source.Width - 1);
        int nextY = Math.Clamp(rawY + (fractionY >= .5f ? 1 : -1), 0, source.Height - 1);
        float distanceX = MathF.Abs(Sub(Add(rawX, .5f), x));
        float distanceY = MathF.Abs(Sub(Add(rawY, .5f), y));
        byte[] bytes = table == OriginalSkyAtmoRawTable.Transmittance ? source.Transmittance.Bytes : source.Ambient2D.Bytes;
        float Channel(int column, int row, int lane) => DecodeHalfC99(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(checked((row * source.Width + column) * 8 + lane * 2), 2)));
        float Lane(int lane)
        {
            float first = Channel(baseX, baseY, lane);
            if (distanceX <= 0)
                return distanceY <= 0 ? first : Add(Mul(first, Sub(1, distanceY)), Mul(Channel(baseX, nextY, lane), distanceY));
            float firstRow = Add(Mul(Sub(1, distanceX), first), Mul(Channel(nextX, baseY, lane), distanceX));
            if (distanceY <= 0) return firstRow;
            float secondRow = Add(Mul(Sub(1, distanceX), Channel(baseX, nextY, lane)), Mul(Channel(nextX, nextY, lane), distanceX));
            // Exact C99FF4..C9A016: second row times Y is added before
            // first row times its complement; no fused multiply/add.
            return Add(Mul(secondRow, distanceY), Mul(Sub(1, distanceY), firstRow));
        }
        return new(Lane(0), Lane(1), Lane(2), Lane(3));
    }

    /// <summary>Literal native half-channel bit conversion. Exponent31 follows
    /// the native nonzero-exponent path; this is not a general IEEE Half cast.</summary>
    public static float DecodeHalfC99(ushort value)
    {
        uint sign = (uint)value & 0x8000, mantissa = (uint)value & 0x3FF, exponent = (uint)value & 0x7C00;
        if (exponent != 0)
            return BitConverter.UInt32BitsToSingle(((sign * 8) | mantissa) << 13 | (exponent + 0x1C000) << 13);
        float onePlus = BitConverter.UInt32BitsToSingle((mantissa | 0x1FC00) << 13);
        return Mul(Mul(sign == 0 ? 1 : -1, BitConverter.UInt32BitsToSingle(0x38800000)), Sub(onePlus, 1));
    }

    private static DokProperty Field(DokObject owner, int ordinal, uint hash, ushort type, uint count)
    {
        if (owner.Properties.Count <= ordinal) throw new InvalidDataException("Original atmosphere table property is missing.");
        var field = owner.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type || field.ElementCount != count ||
            field.ElementSize != 4 || field.RawValue.Length != count * 4)
            throw new InvalidDataException("Original atmosphere table property differs from its exact typed source ordinal.");
        return field;
    }
    private static void ValidateAsset(LoadedRdbAsset source, uint fileId, string sha, int bytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.ResourceUri != GameResourceId.Rdb("system", fileId).ToString() || source.FileId != fileId ||
            source.OverrideSource is not null || source.Bytes.Length != bytes || source.DecodedSha256 != sha ||
            source.SourceContainerPath != "0002/system.rdb.bin" || source.IndexSource.IsOverride ||
            source.IndexSource.ResourceUri != GameResourceId.File("0002/system.rdb").ToString() ||
            source.IndexSource.Bytes != 1661712 || source.IndexSource.Sha256 != SystemIndexSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Original atmosphere table requires its complete original system source URI/hash/length and no override.");
    }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Sub(float a, float b) => a - b;
}
