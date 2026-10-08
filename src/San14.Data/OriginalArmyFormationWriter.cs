using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmySortieFormationInputs(int LandFormationSlot, int NavalFormationSlot);
public sealed record OriginalArmyFormationBytes(byte? Byte1B, byte? Byte1C);
public sealed record OriginalArmyFormationWriteResult(OriginalArmyFormationBytes Bytes,
    bool LandWritten, bool NavalWritten, SourceProvenance FormationSource,
    string FormationDecodedSha256, OriginalArmyFormationWriterCodeSource CodeSource);

/// <summary>The inspected 042BD0 fallback's current source inputs. The person
/// is the already-resolved Army word12 receiver, and associated refers to
/// original 22CE20. Null means unknown; only reached inputs are required.</summary>
public sealed record OriginalArmyOrdinaryNavalFallback(bool? PersonValid,
    bool? PersonPredicate2334A0 = null, bool? AssociatedObjectValid22CE20 = null,
    byte? AssociatedByte15E = null);

/// <summary>Fixed Formation serializer projection used by both Army setters.</summary>
public sealed class OriginalArmyFormationWriterSource
{
    internal OriginalArmyFormationWriterSource(LoadedSkillDefinitions loaded,
        OriginalArmyFormationWriterCodeSource codeSource)
    {
        Source = loaded.Source;
        DecodedSha256 = loaded.Formations.DecodedSha256;
        NativeFields7E = Array.AsReadOnly(loaded.Formations.Formations.Select(row => row.RawBytes[110]).ToArray());
        CodeSource = codeSource;
    }
    public SourceProvenance Source { get; }
    public string DecodedSha256 { get; }
    public IReadOnlyList<byte> NativeFields7E { get; }
    public OriginalArmyFormationWriterCodeSource CodeSource { get; }
}

/// <summary>Ports ordinary native creation's formation writes. Inputs are
/// explicitly selected slots from the same 21-row source table preserved by
/// OriginalScenarioInitializer. A current Core unit's FormationId alone does
/// not establish its naval selection, current native bytes or context.</summary>
public static class OriginalArmyFormationWriter
{
    public static OriginalArmyFormationWriterSource ReadSource(LoadedSkillDefinitions loaded, string language)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        S14FormationCatalog catalog = loaded.Formations;
        if (catalog.FormatMarker != 0x66 || catalog.FormationRecordOffset != 0x2D419 ||
            catalog.FormationRecordStride != 152 || catalog.Formations.Count != 21 ||
            !Sha256(catalog.DecodedSha256) || !Sha256(loaded.Source.Sha256) ||
            string.IsNullOrWhiteSpace(loaded.Source.ResourceUri))
            throw new InvalidDataException("Army formation writer requires a complete sourced CE 21x152 formation table.");
        for (int slot = 0; slot < catalog.Formations.Count; slot++)
        {
            S14FormationDefinition row = catalog.Formations[slot];
            if (row is null || row.Slot != slot ||
                row.RawBytes is not { Length: 152 } || row.RawParameterBytes is not { Length: 42 } ||
                !row.RawBytes.AsSpan(110).SequenceEqual(row.RawParameterBytes))
                throw new InvalidDataException("Army formation fixed row or native byte7E projection differs.");
        }
        return new(loaded, OriginalArmyFormationWriterCodeSources.ForLanguage(language));
    }

    /// <summary>Native ordinary 2D2B20 uses input+C for land and input+10
    /// for naval, after writing Army.type10=1. Previous bytes must be supplied
    /// for a current receiver; CreateOrdinary applies the inspected reset.</summary>
    public static OriginalArmyFormationWriteResult WriteOrdinary(OriginalArmyFormationWriterSource source,
        OriginalArmySortieFormationInputs inputs, OriginalArmyFormationBytes previous,
        OriginalArmyOrdinaryNavalFallback? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(previous);
        bool landWritten = (uint)inputs.LandFormationSlot <= 20 && source.NativeFields7E[inputs.LandFormationSlot] != 2;
        byte? land = landWritten ? (byte)inputs.LandFormationSlot : previous.Byte1B;
        bool navalWritten = (uint)inputs.NavalFormationSlot <= 20 && source.NativeFields7E[inputs.NavalFormationSlot] == 2;
        byte? naval = navalWritten ? (byte)inputs.NavalFormationSlot : previous.Byte1C;
        if (!navalWritten)
        {
            bool valid = Known(fallback?.PersonValid, "naval fallback person validity");
            if (valid)
            {
                //042BD0 returns11 before looking up22CE20 when2334A0(person,0) is true.
                naval = Known(fallback!.PersonPredicate2334A0, "naval fallback predicate2334A0") ? (byte)11 :
                    Known(fallback.AssociatedObjectValid22CE20, "naval fallback associated object validity22CE20")
                        ? Known(fallback.AssociatedByte15E, "naval fallback associated byte15E") : (byte)11;
                navalWritten = true;
            }
        }
        return new(new(land, naval), landWritten, navalWritten, source.Source, source.DecodedSha256, source.CodeSource);
    }

    /// <summary>226C50 resets byte1B and byte1C together to zero. This is
    /// a creation projection, not a claim about a unit after subsequent updates.</summary>
    public static OriginalArmyFormationWriteResult CreateOrdinary(OriginalArmyFormationWriterSource source,
        OriginalArmySortieFormationInputs inputs, OriginalArmyOrdinaryNavalFallback? fallback = null) =>
        WriteOrdinary(source, inputs, new(0, 0), fallback);

    /// <summary>Projects only the inspected ordinary creation's reset and land
    /// setter. Naval byte is left unknown because its subsequent setter is not
    /// executed by this partial query. Useful when a non-water visual consumes
    /// only byte1B; this does not assert a complete/current Army creation state.</summary>
    public static OriginalArmyFormationWriteResult ProjectCreatedOrdinaryLand(
        OriginalArmyFormationWriterSource source,int explicitlySelectedLandSlot)
    {
        ArgumentNullException.ThrowIfNull(source);
        bool written=(uint)explicitlySelectedLandSlot<=20 && source.NativeFields7E[explicitlySelectedLandSlot]!=2;
        return new(new(written ? (byte)explicitlySelectedLandSlot : (byte)0,null),written,false,
            source.Source,source.DecodedSha256,source.CodeSource);
    }

    /// <summary>Reads the two formation arguments consumed by native ordinary
    /// creation from its parameter block, excluding the UI record's leading tag.</summary>
    public static OriginalArmySortieFormationInputs ReadSortieInputs(ReadOnlySpan<byte> nativeParameters)
    {
        if (nativeParameters.Length < 0x14)
            throw new InvalidDataException("Native ordinary sortie parameter prefix is truncated.");
        return new(BinaryPrimitives.ReadInt32LittleEndian(nativeParameters.Slice(0x0C, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(nativeParameters.Slice(0x10, 4)));
    }

    private static T Known<T>(T? value, string field) where T : struct => value ??
        throw new InvalidDataException($"Original Army {field} is unknown.");
    private static bool Sha256(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
}
