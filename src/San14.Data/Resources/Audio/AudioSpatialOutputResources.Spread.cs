using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Master-ordered spread control and its actual output-node trigger keys.
/// The live mixer owns elapsed samples, detector booleans and current gain.</summary>
public sealed record AudioSpatialSpreadControl(uint Key, float AttackSeconds,
    float ReleaseSeconds, float MaximumGain, IReadOnlyList<uint> TriggerOutputKeys);

public static partial class AudioSpatialOutputResources
{
    private static IReadOnlyList<AudioSpatialSpreadControl> ReadSpreadControls(
        AudioMusicOutputGraph graph, out IReadOnlyList<uint> selectedKeys,
        out IReadOnlyList<int> selectedIndices)
    {
        byte[] bytes = graph.Source.Bytes;
        int master = -1, masterSize = 0, classification = -1, classificationSize = 0;
        uint U(int at)
        {
            if (at < 0 || at > bytes.Length - 4)
                throw new InvalidDataException("Spatial spread field is outside its resource.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.UInt32BitsToSingle(U(at));
            return float.IsFinite(value) ? value :
                throw new InvalidDataException("Spatial spread parameter is nonfinite.");
        }
        void Range(int relative, int count, int size)
        {
            if (relative < 0 || count < 0 || relative > size || count > size - relative)
                throw new InvalidDataException("Spatial spread table is outside its source section.");
        }
        for (int at = 80; at < bytes.Length;)
        {
            int size = checked((int)U(at + 4));
            if (size < 8 || size > bytes.Length - at)
                throw new InvalidDataException("Spatial spread section is incomplete.");
            uint type = U(at);
            if (type == 0xE0736214)
            {
                if (master >= 0) throw new InvalidDataException("Spatial spread master is ambiguous.");
                master = at; masterSize = size;
            }
            else if (type == 0xF96787ED && size >= 12 && U(at + 8) == graph.Classification.Id)
            {
                if (classification >= 0) throw new InvalidDataException("Spatial spread classification is ambiguous.");
                classification = at; classificationSize = size;
            }
            at = checked(at + size);
        }
        if (master < 0 || masterSize < 0x60 || classification < 0 || classificationSize < 0x34)
            throw new InvalidDataException("Spatial spread master or classification header is incomplete.");
        int count = checked((int)U(master + 0x58));
        int table = checked((int)U(master + 0x5C));
        if (count > 32) throw new InvalidDataException("Spatial spread controls exceed their source bit mask.");
        if (count != 0) Range(table, checked(count * 4), masterSize);
        var controls = new AudioSpatialSpreadControl[count];
        for (int i = 0; i < count; i++)
        {
            int record = checked((int)U(master + table + i * 4));
            Range(record, 0x18, masterSize);
            int triggerCount = checked((int)U(master + record + 0x10));
            int triggerOffset = checked((int)U(master + record + 0x14));
            if (triggerCount > 32) throw new InvalidDataException("Spatial spread trigger set exceeds its source bit mask.");
            if (triggerCount != 0)
                Range(checked(record + triggerOffset), checked(triggerCount * 4), masterSize);
            var triggers = new List<uint>();
            for (int key = 0; key < triggerCount; key++)
            {
                uint target = U(master + record + triggerOffset + key * 4);
                //Native construction ignores references absent from its actual
                //master output table, rather than inventing a receiver.
                if (graph.Outputs.Any(output => output.Key == target)) triggers.Add(target);
            }
            float attack = F(master + record + 4), release = F(master + record + 8);
            if (attack < 0 || release < 0)
                throw new InvalidDataException("Spatial spread has a negative source duration.");
            controls[i] = new(U(master + record), attack, release,
                F(master + record + 0xC), triggers.AsReadOnly());
        }
        int keyCount = checked((int)U(classification + 0x2C));
        int keyOffset = checked((int)U(classification + 0x30));
        if (keyCount > 32) throw new InvalidDataException("Spatial classification spread keys exceed their source bit mask.");
        if (keyCount != 0) Range(keyOffset, checked(keyCount * 4), classificationSize);
        var keys = new uint[keyCount];
        for (int i = 0; i < keys.Length; i++) keys[i] = U(classification + keyOffset + i * 4);
        selectedKeys = Array.AsReadOnly(keys);
        //14122C379 loads only controls[0].Key; its search loop increments an
        //index without changing/reloading that key. Preserve the actual native
        //admission rather than silently repairing the resource author's intent.
        selectedIndices = count != 0 && keys.Contains(controls[0].Key)
            ? Array.AsReadOnly(new[] { 0 }) : Array.Empty<int>();
        return Array.AsReadOnly(controls);
    }
}
