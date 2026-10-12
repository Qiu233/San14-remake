using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public enum SkyAtmoRawTable { Transmittance, Ambient2D }

public static class SkyAtmoTableResources
{
    public const uint ObjectId = 0x0490C334;
    public const uint ClassId = 0x1AA5B65E;
    public const uint DefinitionFileId = 0xDAB638B2;
    public const uint RawResourceType = 0xD7F47FB1;
    public const string NativeEvidence = "docs/.local/ce-sky-season-sta-20261002/contract.json";

    /// <summary>Import at an explicit resource load boundary. Existing season
    /// owners retain their tables; a new load observes current resource overrides.</summary>
    public static LoadedSkyAtmoTableSources Load(GameResourceRepository repository,uint objectId=ObjectId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var definitions=repository.LoadObjects("system",DefinitionFileId);
        var declaration=ReadDeclaration(definitions,objectId);
        uint transmittance=Key(declaration,0x4E513F7C),ambient=Key(declaration,0x54489708);
        return Read(definitions,transmittance==0 ? null : repository.LoadRdbAsset("system",transmittance),
            ambient==0 ? null : repository.LoadRdbAsset("system",ambient),objectId);
    }

    public static LoadedSkyAtmoTableSources? LoadForSeason(GameResourceRepository repository,
        MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        uint key=Key(package.Camera,0x063D4BE7);
        return key==0 ? null : Load(repository,key);
    }

    public static LoadedSkyAtmoTableSources Read(LoadedObjects definitions,
        LoadedRdbAsset? transmittance, LoadedRdbAsset? ambient2D,uint objectId=ObjectId)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var declaration=ReadDeclaration(definitions,objectId);
        float Scalar(uint hash) => BinaryPrimitives.ReadSingleLittleEndian(Field(declaration,hash,8,1).RawValue);
        Vector4 Vector(uint hash)
        {
            var raw = Field(declaration, hash, 8, 4).RawValue;
            return new(BinaryPrimitives.ReadSingleLittleEndian(raw), BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(4)),
                BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(8)), BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(12)));
        }
        uint plane=Key(declaration,0xE67B59E3),flags=Key(declaration,0x05D92ACC);
        uint declaredWidth=Key(declaration,0xB9AF5BFA,4),declaredHeight=Key(declaration,0x59FD9627,4);
        ulong cells=(ulong)declaredWidth*declaredHeight;
        if(declaredWidth==0||declaredHeight==0||cells>int.MaxValue/8)
            throw new InvalidDataException("Atmosphere table dimensions must define a supported positive half4 byte range.");
        int byteLength=(int)cells*8;
        RequireTable(transmittance,Key(declaration,0x4E513F7C),byteLength);
        RequireTable(ambient2D,Key(declaration,0x54489708),byteLength);
        return new(objectId,definitions.Source,transmittance,ambient2D,plane,(int)declaredWidth,(int)declaredHeight,flags,
            Vector(0xA9D85746),Vector(0x12F68AF8),Scalar(0x1B9B45D0),Scalar(0x775522AE),Scalar(0x4680912A),Scalar(0x743793F9));
    }

    private static DokObject ReadDeclaration(LoadedObjects definitions,uint objectId)
    {
        var objects=definitions.Document.Objects.Where(value=>value.ObjectId==objectId).ToArray();
        if(objects.Length!=1||objects[0].Kind!=DokObjectKind.Inline||objects[0].ClassId!=ClassId)
            throw new InvalidDataException("Atmosphere reference must select a unique StaticSky2AtmoTable declaration.");
        return objects[0];
    }

    public static Vector4 SampleGrid(LoadedSkyAtmoTableSources source,
        float heightCoordinate, float angleCoordinate, SkyAtmoRawTable table = SkyAtmoRawTable.Transmittance)
    {
        // Read/Load checks each declared complete raw table. The requested
        // sampling capability below rejects a genuinely absent table.
        ArgumentNullException.ThrowIfNull(source);
        if (!float.IsFinite(heightCoordinate) || !float.IsFinite(angleCoordinate) ||
            heightCoordinate is < 0 or > 1 || angleCoordinate is < 0 or > 1 ||
            table is not (SkyAtmoRawTable.Transmittance or SkyAtmoRawTable.Ambient2D))
            throw new InvalidDataException("Original half4 sampling requires explicit finite unit height/angle coordinates and an exact source table.");
        float x = Mul(heightCoordinate, source.Width), y = Mul(angleCoordinate, source.Height);
        int rawX = (int)x, rawY = (int)y;
        float fractionX = Sub(x, rawX), fractionY = Sub(y, rawY);
        int baseX = Math.Clamp(rawX, 0, source.Width - 1), baseY = Math.Clamp(rawY, 0, source.Height - 1);
        int nextX = Math.Clamp(rawX + (fractionX >= .5f ? 1 : -1), 0, source.Width - 1);
        int nextY = Math.Clamp(rawY + (fractionY >= .5f ? 1 : -1), 0, source.Height - 1);
        float distanceX = MathF.Abs(Sub(Add(rawX, .5f), x));
        float distanceY = MathF.Abs(Sub(Add(rawY, .5f), y));
        var data = table == SkyAtmoRawTable.Transmittance ? source.Transmittance : source.Ambient2D;
        float Channel(int column, int row, int lane) => DecodeHalfC99(
            data.ReadChannel(checked((row * source.Width + column) * 8 + lane * 2)));
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

    private static uint Key(DokObject owner,uint hash,ushort type=5)
        => BinaryPrimitives.ReadUInt32LittleEndian(Field(owner,hash,type,1).RawValue);
    private static DokProperty Field(DokObject owner,uint hash,ushort type,uint count)
    {
        var fields=owner.Properties.Where(value=>value.NameHash==hash).ToArray();
        if(fields.Length!=1)throw new InvalidDataException("Atmosphere declaration requires a unique named field.");
        var field=fields[0];
        if (field.TypeCode != type || field.ElementCount != count ||
            field.ElementSize != 4 || field.RawValue.Length != count * 4)
            throw new InvalidDataException("Atmosphere declaration field has an unsupported type or element layout.");
        return field;
    }
    private static void RequireTable(LoadedRdbAsset? source,uint fileId,int bytes)
    {
        if(fileId==0)
        {
            if(source is not null)throw new InvalidDataException("An absent atmosphere table declaration cannot accept supplied raw data.");
            return;
        }
        ArgumentNullException.ThrowIfNull(source);
        if (source.ResourceUri != GameResourceId.Rdb("system", fileId).ToString() || source.FileId != fileId ||
            source.Bytes.Length != bytes)
            throw new InvalidDataException("Atmosphere raw resource must match its declared key and complete half4 dimensions.");
    }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)] private static float Sub(float a, float b) => a - b;
}
