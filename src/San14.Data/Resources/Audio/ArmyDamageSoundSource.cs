using System.Buffers.Binary;

namespace San14.Data;

/// <summary>A validated declaration, before the admitted voice selects its
/// one sample and pitch. Resource preparation does not advance audio state.</summary>
public sealed class ArmyDamageSoundDeclaration
{
    private readonly AudioSelectionBank bank;
    private readonly TacticSoundPlayback[] variants;
    private readonly float[] leafRates;
    private readonly float pitchOrigin, pitchWidth;
    public int ManagerId { get; }
    public AudioSpatialOutputPlan OutputPlan { get; }

    internal ArmyDamageSoundDeclaration(AudioSelectionBank bank, int managerId,
        TacticSoundPlayback[] variants, float[] leafRates, float pitchOrigin, float pitchWidth,
        AudioSpatialOutputPlan outputPlan)
    {
        this.bank = bank; ManagerId = managerId; this.variants = variants; this.leafRates = leafRates;
        this.pitchOrigin = pitchOrigin; this.pitchWidth = pitchWidth; OutputPlan = outputPlan;
    }

    public SpatialEffectSoundPlayback Select(AudioCueSelectionRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        int index = random.SelectUniformChild(variants.Length);
        float pitch = random.ReadSingleContinuousRange(pitchOrigin, pitchWidth);
        var selected = variants[index];
        var layer = selected.Layers[0];
        float rate = MathF.Pow(2, pitch + leafRates[index]);
        if (!float.IsFinite(rate) || rate <= 0)
            throw new InvalidDataException("army_damage.sound.selected_rate");
        var pitched = layer with { Playback = layer.Playback with { PlaybackRate = rate } };
        var group = SpatialEffectSoundSource.ReadArmyDamageGroup(bank, selected.Cue.ExactCueName, pitch);
        return new(ManagerId, selected.Cue, Array.AsReadOnly(new[] { pitched }), group, OutputPlan);
    }
}

/// <summary>The reached facility/trap damage cues have a uniform three-child
/// selector and one continuous pitch range. Other selector grammars stay unsupported.</summary>
public static class ArmyDamageSoundSource
{
    public static ArmyDamageSoundDeclaration Prepare(AudioSelectionBank bank, LoadedRdbAsset categories,
        int managerId)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(categories);
        if (managerId is not (161 or 162)) throw new InvalidDataException("army_damage.sound.manager_index");
        var variants = new TacticSoundPlayback[3];
        var rates = new float[3];
        for (int index = 0; index < variants.Length; index++)
            variants[index] = TacticSoundSource.ReadArmyDamageVariant(bank, managerId, index, out rates[index]);
        var cue = bank.Cues.Single(cue => cue.Name == variants[0].Cue.ExactCueName);
        byte[] bytes = bank.Source.Bytes;
        int begin = cue.SectionOffset, end = checked(begin + cue.SectionBytes);
        uint U(int at)
        {
            if (begin < 0 || at < begin || at > end - 4 || end > bytes.Length)
                throw new InvalidDataException("army_damage.sound.section_bounds");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        float F(int at)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at)));
            return float.IsFinite(value) ? value : throw new InvalidDataException("army_damage.sound.nonfinite_pitch");
        }
        int roots = checked(begin + (int)U(begin + 36)), group = checked(begin + (int)U(roots));
        int expression = checked(group + (int)U(group + 40));
        if (U(expression) != 0 || U(expression + 4) != 1 || U(expression + 16) != 0)
            throw new NotSupportedException("army_damage.sound.pitch_range");
        float origin = F(expression + 8), width = F(expression + 12);
        if (categories.ResourceUri != GameResourceId.Rdb("RRPreview", NarrationVoiceSelection.OutputResourceId).ToString())
            throw new InvalidDataException("army_damage.sound.output_resource");
        var outputs = AudioMusicOutputResources.Read(categories, U(group + 24));
        // The actual routes are kind0. Their source main path does not use
        // the group's secondary gain, which belongs to side-input kinds1..3.
        var plan = AudioSpatialOutputResources.ReadFreshEffectsPlan(outputs);
        return new(bank, managerId, variants, rates, origin, width, plan);
    }
}
