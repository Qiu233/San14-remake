using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalPersonRelationFields(ushort B2, ushort B4, ushort B6,
    ushort BA, ushort BC, byte BE, ushort[] C0, ushort[] D0);
public sealed record OriginalPersonRelationCalculation(int Scalar, byte Tier);

/// <summary>Current TC2A6EE0/2A6FA0 inputs. Static marker66 fields are seeded
/// once; every query uses the saved raw fields, current pointer tables and the
/// actual primary list allocator. No pair matrix or special self result.</summary>
public sealed class OriginalOwnedPersonRelationInputs
{
    private const string Prefix = "ce-person-relation-current:";
    public const string VersionKey = Prefix + "version";
    public static string PersonFieldKey(int physical, int tcField) => Prefix + $"person:{physical}:{tcField:X}";
    public static string Force178Key(int physical) => Prefix + $"force:{physical}:178";
    private readonly LoadedScenario _source;
    private readonly string _language, _ruleVersion;
    private readonly OriginalPersonRelationFields[] _people;

    private OriginalOwnedPersonRelationInputs(LoadedScenario source, string language,
        string hash, OriginalPersonRelationFields[] people)
        => (_source, _language, _ruleVersion, _people) = (source, language, hash, people);

    public static OriginalOwnedPersonRelationInputs Load(OriginalGameRepository repository,
        LoadedScenario source, string language)
    {
        string identity = SimulationRuleIdentity.ForLocale(language);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language)
            throw new InvalidDataException("Person relation inputs require coherent CE marker66 source.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath)
            : source.ScenarioSource.PhysicalPath;
        byte[] bytes = File.ReadAllBytes(path);
        return new(source, language, identity, ReadSourceRows(S14Reader.Read(bytes, true), language));
    }

    public static OriginalPersonRelationFields[] ReadSourceRows(S14Document document, string language)
    {
        if (document.FormatMarker != 0x66 || language is not ("SC" or "TC"))
            throw new InvalidDataException("Person relation serializer requires CE SC/TC.");
        var table = S14ScenarioTables.ReadCharacterNames(document, language);
        if (table.Names.Count != 1851) throw new InvalidDataException("Incomplete source Person relation table.");
        int delta = language == "SC" ? 56 : 0;
        var result = new OriginalPersonRelationFields[1851];
        for (int physical = 0; physical < result.Length; physical++)
        {
            ReadOnlySpan<byte> row = document.DecodedPayload.AsSpan(table.RecordOffset + physical * table.RecordStride,
                table.RecordStride);
            ushort[] c0 = new ushort[8], d0 = new ushort[8];
            for (int i = 0; i < 8; i++)
            {
                c0[i] = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(232 + delta + i * 2, 2));
                d0[i] = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(248 + delta + i * 2, 2));
            }
            result[physical] = new(BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(224 + delta, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(226 + delta, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(228 + delta, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(152 + delta, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(154 + delta, 2)), row[231 + delta], c0, d0);
        }
        return result;
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != _language ||
            simulation.Parameters.RuleVersion != _ruleVersion || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) != _source.ScenarioSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Any(k => k.StartsWith(Prefix, StringComparison.Ordinal)) ||
            _source.Snapshot.WorldParameters?.RawField48 == 0x13)
            throw new InvalidDataException("Person relation inputs require their unused ordinary fresh owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int p = 0; p < _people.Length; p++)
        {
            var row = _people[p];
            values.Add(PersonFieldKey(p, 0xB2), row.B2); values.Add(PersonFieldKey(p, 0xB4), row.B4);
            values.Add(PersonFieldKey(p, 0xB6), row.B6); values.Add(PersonFieldKey(p, 0xBA), row.BA);
            values.Add(PersonFieldKey(p, 0xBC), row.BC); values.Add(PersonFieldKey(p, 0xBE), row.BE);
            for (int i = 0; i < 8; i++)
            { values.Add(PersonFieldKey(p, 0xC0 + i * 2), row.C0[i]); values.Add(PersonFieldKey(p, 0xD0 + i * 2), row.D0[i]); }
        }
        //227070 writes ushort178=0. Static237CC0 skips this runtime-only
        //window; byte193 is serialized separately and is not an alias.
        for (int p = 0; p < 52; p++) values.Add(Force178Key(p), 0);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public void BindCurrent(GameState state, OriginalEventInitializationContext current)
        => current.DestructionRelationTier2A6EE0Reader = (target, a, b, mode) => ComputeCurrent(state, target, a, b, mode).Tier;

    public static OriginalPersonRelationFields? ReadCurrentPersonFields(GameState state, int physical)
    {
        if ((uint)physical >= 1851 || state.NativeArmySimulation?.Parameters.Int32Values is not { } values ||
            values.GetValueOrDefault(VersionKey) != 1) return null;
        ushort? W(int field) => values.TryGetValue(PersonFieldKey(physical, field), out int v) && v is >= 0 and <= 65535 ? (ushort)v : null;
        ushort[] c0 = new ushort[8], d0 = new ushort[8];
        for (int i = 0; i < 8; i++)
        {
            if (W(0xC0 + i * 2) is not ushort c || W(0xD0 + i * 2) is not ushort d) return null;
            c0[i] = c; d0[i] = d;
        }
        if (W(0xB2) is not ushort b2 || W(0xB4) is not ushort b4 || W(0xB6) is not ushort b6 ||
            W(0xBA) is not ushort ba || W(0xBC) is not ushort bc || W(0xBE) is not ushort be || be > 255) return null;
        return new(b2, b4, b6, ba, bc, (byte)be, c0, d0);
    }

    /// <summary>Mutates only the given operation-local real primary pool.
    /// Unknown inputs throw with the first missing owner, for caller discard.</summary>
    public static OriginalPersonRelationCalculation ComputeCurrent(GameState state,
        OriginalEventInitializationContext current, int firstPhysical, int secondPhysical, int mode)
    {
        bool validA = Known(current.ReadPersonNativeValidity(firstPhysical), "Person A validity");
        if (!validA) return new(0, 1);
        bool validB = Known(current.ReadPersonNativeValidity(secondPhysical), "Person B validity");
        if (!validB) return new(0, 1);
        var a = ReadCurrentPersonFields(state, firstPhysical) ?? throw Unknown("Person A saved relation fields");
        var b = ReadCurrentPersonFields(state, secondPhysical) ?? throw Unknown("Person B saved relation fields");
        ushort aid = Known(current.ReadPersonIdentity(firstPhysical), "Person A ID10");
        ushort bid = Known(current.ReadPersonIdentity(secondPhysical), "Person B ID10");
        if (current.ReadOwnedCurrentListPool() is not { } pool || !pool.OwnsArmyReturnProfile())
            throw Unknown("owned primary shared-list allocator");
        bool sameId = aid == bid;
        bool Father(OriginalPersonRelationFields x, ushort other) => !sameId && x.B4 != 0 && x.B4 == other;
        bool Mother(OriginalPersonRelationFields x, ushort other) => !sameId && x.B6 != 0 && x.B6 == other;
        bool spouse = !sameId && a.BA != 0 && a.BA == bid;
        bool parents = Father(a, bid) || Mother(a, bid) || Father(b, aid) || Mother(b, aid);
        bool siblings = !sameId && ((a.B4 != 0 && a.B4 == b.B4) || (a.B6 != 0 && a.B6 == b.B6));
        bool sworn = !sameId && a.BC != 0 && a.BC == b.BC;
        bool clan = a.B2 != 0 && a.B2 == b.B2;
        int multiplier = spouse || parents || siblings || clan ? P(SimulationParameterKeys.ArmyDestruction.RelationFamilyMultiplier) : 1;
        //2C8550's early exits occur only after the complete actual eight-entry
        //fill and wrapper destruction. The remaining wrappers are not run.
        bool isolated = !OneListHit(a.C0, secondPhysical) && !OneListHit(b.C0, firstPhysical) &&
            !OneListHit(a.D0, secondPhysical) && !OneListHit(b.D0, firstPhysical) && !sworn;
        var likes = TwoListHits(a.C0, secondPhysical, b.C0, firstPhysical);
        int likeTerm = unchecked(((likes.First ? P(SimulationParameterKeys.ArmyDestruction.RelationLikeBonus) : 0) + (likes.Second ? P(SimulationParameterKeys.ArmyDestruction.RelationLikeBonus) : 0)) * multiplier);
        var hates = TwoListHits(a.D0, secondPhysical, b.D0, firstPhysical);
        int hateTerm = unchecked(-((hates.First ? P(SimulationParameterKeys.ArmyDestruction.RelationHatePenalty) : 0) + (hates.Second ? P(SimulationParameterKeys.ArmyDestruction.RelationHatePenalty) : 0)) * multiplier);
        int swornTerm = sworn ? P(SimulationParameterKeys.ArmyDestruction.RelationSwornBonus) : 0;
        int spouseTerm = isolated && spouse ? P(SimulationParameterKeys.ArmyDestruction.RelationSpouseBonus) : 0;
        int parentTerm = isolated && parents ? P(SimulationParameterKeys.ArmyDestruction.RelationParentBonus) : 0;
        int siblingTerm = isolated && siblings ? P(SimulationParameterKeys.ArmyDestruction.RelationSiblingBonus) : 0;
        int clanTerm = isolated && !parents && !siblings && clan ? P(SimulationParameterKeys.ArmyDestruction.RelationClanBonus) : 0;
        int distance = Math.Abs((int)a.BE - b.BE);
        distance = Math.Min(Math.Min(distance, 150 - distance), 75);
        int affinityWeight = P(SimulationParameterKeys.ArmyDestruction.RelationAffinityWeight);
        float affinity = ((75f - (float)distance) * (float)affinityWeight) / 75f;
        int rounded = Cvtt(MathF.Round(affinity, MidpointRounding.AwayFromZero));
        int affinityTerm = Math.Min(rounded, affinityWeight);
        int traitTerm = 0;
        if (mode != 0 && !hates.First && !hates.Second &&
            Known(OriginalTacticRemakeCurrentTraits.CommanderHasEffect(current, secondPhysical, 0x6C), "Person B effect6C presence"))
            traitTerm = Known(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, secondPhysical, 0x6C), "Person B effect6C sum");
        var firstForce = current.ReadPersonForce(firstPhysical) ?? throw Unknown("Person A22CB40 Force");
        var secondForce = current.ReadPersonForce(secondPhysical) ?? throw Unknown("Person B22CB40 Force");
        int forceTerm = 0;
        if (firstForce == secondForce)
        {
            int force = Known(current.ReadForcePhysicalSlot(firstForce), "same Force physical");
            if (state.NativeArmySimulation?.Parameters.Int32Values is not { } values ||
                !values.TryGetValue(Force178Key(force), out int flag) || flag is < 0 or > 255) throw Unknown("Force byte178");
            //232B30 reads raw D0 IDs, not the allocator's successful payloads.
            if ((flag & 1) != 0 && !a.D0.Contains(bid) && !b.D0.Contains(aid)) forceTerm = P(SimulationParameterKeys.ArmyDestruction.RelationForceBonus);
        }
        int scalar = unchecked(traitTerm + forceTerm + affinityTerm + clanTerm + siblingTerm + parentTerm +
            spouseTerm + swornTerm + hateTerm + likeTerm);
        byte tier = scalar < P(SimulationParameterKeys.ArmyDestruction.RelationTierOneThreshold) ? (byte)1 : scalar < P(SimulationParameterKeys.ArmyDestruction.RelationTierTwoThreshold) ? (byte)2 :
            scalar < P(SimulationParameterKeys.ArmyDestruction.RelationTierThreeThreshold) ? (byte)3 : scalar < P(SimulationParameterKeys.ArmyDestruction.RelationTierFourThreshold) ? (byte)4 : (byte)5;
        return new(scalar, tier);

        int P(string key) => Known(OriginalArmyDestructionParameterSeed.ReadCurrent(state, key), $"relation parameter {key}");
        bool OneListHit(ushort[] ids, int sought)
        {
            var handle = current.CreateTemporaryHexList() ?? throw Unknown("temporary wrapper allocation outcome");
            try { Fill(handle, ids); return Contains(handle, sought); }
            finally { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
        }
        (bool First, bool Second) TwoListHits(ushort[] first, int firstSought, ushort[] second, int secondSought)
        {
            var handle = current.CreateTemporaryHexList() ?? throw Unknown("temporary wrapper allocation outcome");
            try
            {
                current.ClearTemporaryHexList(handle); Fill(handle, first); bool firstHit = Contains(handle, firstSought);
                current.ClearTemporaryHexList(handle); Fill(handle, second); bool secondHit = Contains(handle, secondSought);
                return (firstHit, secondHit);
            }
            finally { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
        }
        void Fill(OriginalHexListHandle handle, ushort[] ids)
        {
            current.ClearTemporaryHexList(handle);
            foreach (ushort id in ids)
            {
                var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                if (person is null) throw Unknown("list Person ID lookup");
                if (person.PhysicalSlot is not int physical) continue;
                if (!Known(current.ReadPersonNativeValidity(physical), "list Person validity")) continue;
                var node = current.AllocateHexListNode(handle);
                if (node.Identity.HasValue) current.SetPersonRelationListPayload(node, person);
            }
        }
        bool Contains(OriginalHexListHandle handle, int sought)
        {
            var node = current.ReadHexListHead(handle) ?? throw Unknown("temporary list head");
            while (node.Identity.HasValue)
            {
                var person = current.ReadPersonRelationListPayload(node) ?? throw Unknown("temporary Person payload");
                if (person.PhysicalSlot == sought) return true;
                node = current.ReadHexListNodeNext(node) ?? throw Unknown("temporary list next");
            }
            return false;
        }
    }

    private static int Cvtt(float f) => !float.IsFinite(f) || f < int.MinValue || f >= 2147483648f ? int.MinValue : (int)f;
    private static T Known<T>(T? value, string name) where T : struct => value ?? throw Unknown(name);
    private static InvalidDataException Unknown(string name) => new("current.Relation2A6EE0: " + name);
}

public sealed partial class OriginalEventInitializationContext
{
    internal void SetPersonRelationListPayload(OriginalHexListNodeReference node, OriginalTransferPersonReference person)
        => WriteCurrentListPayload(NodeIdentity(node), person);
    internal OriginalTransferPersonReference? ReadPersonRelationListPayload(OriginalHexListNodeReference node)
        => ReadNativeListPayload<OriginalTransferPersonReference>(NodeIdentity(node));
}
