using San14.Data.Formats;

namespace San14.Data;

/// <summary>Carry the already restored native named-field values into the
/// exact original selected layout. Complete caller bytes remain authoritative
/// for padding and unwritten variables; absent declarations receive no bytes.</summary>
public static class DeferredSelectedSceneConstants
{
    public static DeferredSceneConstantPacket Apply(DeferredF103SelectedSource selected,
        ReadOnlySpan<byte> completeSelectedTemplate, DeferredSceneInputs input)
        =>Apply(selected,completeSelectedTemplate,input,null);

    /// <summary>Only PS767's exact source-owned Hatching IEEE outputs may pass
    /// the selected-layout bridge. The general canonical finite guard stays intact.</summary>
    public static DeferredSceneConstantPacket Apply(DeferredF103SelectedSource selected,
        ReadOnlySpan<byte> completeSelectedTemplate, DeferredSceneInputs input,
        DeferredSelected767HatchingIeeeProof? hatchingIeeeProof)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(input);
        selected.Validate();
        if ((selected.Combination.Index, selected.PixelProgramIndex, selected.PixelProgramSha256) is not
            (128, 97, "FF1CE5BFD5F4A8B3397E789C59045BCC72186644AA6C7D22EE523D633CBB384F") and not
            (510, 383, "91C2A580A1D05CA6144FD7CDE4F0AC24CD7ED425F2CFB4BCAAA4E9A4F62A3AF1") and not
            (640, 481, "1FD258CD6212012BAF24E7C407FC01771C93755FDC836C2F4E050238638DAE1B") and not
            (1022, 767, "62CEB4427347D0D7E37CA096138B2F39CAEB9342134356177CFE83027E879A4C"))
            throw new NotSupportedException("Selected scene writes require one of the four independently verified original source layouts.");
        var canonical = selected.CanonicalPixelDefinitions;
        var current = selected.PixelDefinitions;
        var canonicalGlobals = canonical.Buffers.Single(b => b.Name.Text == "$Globals");
        var selectedGlobals = current.Buffers.Single(b => b.Name.Text == "$Globals");
        var binding = current.Bindings.Where(b => b.InputType == 0 && b.Name.Text == "$Globals").ToArray();
        if (completeSelectedTemplate.Length != selectedGlobals.ByteSize || binding.Length != 1 ||
            binding[0].BindPoint != 0 || binding[0].BindCount != 1 ||
            selectedGlobals.Variables.Select(v => v.Name.Text).Distinct().Count() != selectedGlobals.Variables.Count)
            throw new InvalidDataException("Selected scene writer requires the complete exact original cb0 extent and unique named fields.");
        // This scratch is only the existing named writer's evaluation space.
        // None of its holes or absent variables are copied into the output.
        byte[] evaluation = new byte[DeferredSceneConstants.ByteSize];
        foreach (var variable in selectedGlobals.Variables)
        {
            var originals = canonicalGlobals.Variables.Where(v => v.Name.Text == variable.Name.Text).ToArray();
            if (originals.Length != 1 || originals[0].ByteSize != variable.ByteSize ||
                (long)variable.StartOffset + variable.ByteSize > completeSelectedTemplate.Length ||
                !SameType(canonical.Types.Single(t => t.Offset == originals[0].TypeOffset),
                    current.Types.Single(t => t.Offset == variable.TypeOffset)))
                throw new InvalidDataException("Selected named field differs from the restored native semantic shape: " + variable.Name.Text);
            completeSelectedTemplate.Slice((int)variable.StartOffset, (int)variable.ByteSize)
                .CopyTo(evaluation.AsSpan((int)originals[0].StartOffset, (int)originals[0].ByteSize));
        }
        var evaluationInput=input;
        if(hatchingIeeeProof is not null)
        {
            if(selected.Combination.Index!=1022||selected.PixelProgramIndex!=767||
                selected.PixelProgramSha256!=DeferredSelected767Constants.PixelProgramSha256)
                throw new InvalidDataException("Hatching IEEE authority is limited to its actual selected PS767 layout.");
            hatchingIeeeProof.ValidateCurrent(selected);hatchingIeeeProof.Validate(input.Hatching);
            // This writer copies these words directly. Evaluate the unrelated
            // finite named writes, then restore the exact proved source words;
            // no substitute value reaches the submitted selected CB.
            float Evaluation(float value)=>float.IsFinite(value)?value:0;
            System.Numerics.Vector4 V4(System.Numerics.Vector4 v)=>new(Evaluation(v.X),Evaluation(v.Y),Evaluation(v.Z),Evaluation(v.W));
            System.Numerics.Vector3 V3(System.Numerics.Vector3 v)=>new(Evaluation(v.X),Evaluation(v.Y),Evaluation(v.Z));
            var h=input.Hatching!;
            evaluationInput=input with{Hatching=new(V4(h.Ramp),V4(h.AmbientOcclusion),V3(h.UvRow0),V3(h.UvRow1))};
        }
        var prepared = DeferredSceneConstants.Apply(canonical, evaluation, evaluationInput);
        byte[] result = completeSelectedTemplate.ToArray();
        var writes = new List<DeferredSceneWrite>();
        foreach (var write in prepared.Writes)
        {
            var variables = selectedGlobals.Variables.Where(v => v.Name.Text == write.Name).ToArray();
            if (variables.Length == 0) continue;
            var variable = variables.Single();
            int target = checked((int)variable.StartOffset);
            if (write.ElementCount < 1 || write.ElementBytes < 1 || write.Stride < write.ElementBytes ||
                (long)(write.ElementCount - 1) * write.Stride + write.ElementBytes != variable.ByteSize)
                throw new InvalidDataException("Selected native field write differs from its complete source element shape.");
            for (int i = 0; i < write.ElementCount; i++)
                prepared.Bytes.AsSpan(write.Offset + i * write.Stride, write.ElementBytes)
                    .CopyTo(result.AsSpan(target + i * write.Stride, write.ElementBytes));
            writes.Add(write with { Offset = target });
        }
        hatchingIeeeProof?.RestoreSelectedWords(result,input.Hatching);
        var written = writes.Select(w => w.Name).ToHashSet(StringComparer.Ordinal);
        return new(result, writes.AsReadOnly(), Array.AsReadOnly(selectedGlobals.Variables
            .Where(v => !written.Contains(v.Name.Text)).Select(v => v.Name.Text).ToArray()));
    }

    private static bool SameType(DxbcRdefType original, DxbcRdefType selected) =>
        original.Class == selected.Class && original.BaseType == selected.BaseType &&
        original.Rows == selected.Rows && original.Columns == selected.Columns &&
        original.Elements == selected.Elements && original.Members.Count == 0 && selected.Members.Count == 0;
}
