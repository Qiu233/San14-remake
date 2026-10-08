using System.Collections.ObjectModel;
using San14.Data;

/// <summary>A remake owner for one constructed Game default HDRI, setter Camera
/// and Deferred shader row. CPU source bytes and bindings are immutable. Program,
/// device images, buffers and samplers are borrowed and retired by their owners.</summary>
public sealed class OriginalMapSourceCreatedLightingDeferredProvider : IDisposable
{
    private readonly object gate = new();
    private bool retired;
    public OriginalDeferredPixelProgram Program { get; }
    public OriginalMapSourceCreatedLightingSource Source { get; }
    public OriginalMapLightingBinding Binding { get; }
    public SourceResourceRegistrationDevice? Registration { get; }

    public OriginalMapSourceCreatedLightingDeferredProvider(OriginalDeferredPixelProgram program,
        OriginalMapSourceCreatedLightingSource source, OriginalMapLightingBinding binding,
        SourceResourceRegistrationDevice? registration = null)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.DeferredCombination != program.Combination.Index ||
            binding.DeferredShaderIdentity != program.Source.Source.ResourceUri + "@" + program.Source.Source.DecodedSha256 ||
            binding.BasePath != OriginalMapLightingBasePath.UnmodifiedOrdinary ||
            binding.DirectionalShaderCapacity is < 1 or > 4 ||
            new[] { binding.ViewIdentity, binding.CameraOwnerIdentity, binding.LightOwnerIdentity }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Source-created lighting owner requires its explicit remake binding and original Deferred shader row.");
        Program = program; Source = source; Binding = binding; Registration = registration;
        registration?.ValidateConstructionSource(source);
        ValidateDeviceContract();
    }

    /// <summary>Prepare before terrain/model submission. The returned Camera
    /// must be the Camera accepted by the NativeMapScene for this same frame.</summary>
    public OriginalMapSourceCreatedLightingPacket PrepareFrame(OriginalMapSourceCreatedLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            Registration?.ValidateConstructionSource(Source);
            if (input.Binding != Binding)
                throw new InvalidDataException("Source-created lighting frame belongs to another remake owner or shader selection.");
            return OriginalMapSourceCreatedLighting.Prepare(Source, input);
        }
    }

    /// <summary>Project that accepted frame into a complete Native Deferred
    /// packet. All three caller CBs and every effective source resource are mandatory;
    /// named writers retain unwritten CB0 bytes and both shadow CBs.</summary>
    public OriginalNativeDeferredPacket PrepareDeferred(OriginalNativeMapFrameContext frame,
        OriginalNativeDeferredPacket explicitTemplate, OriginalMapSourceCreatedLightingPacket lighting,
        out OriginalDeferredSceneConstantPacket writes,
        OriginalDeferredF103SelectedSource? currentSelection = null)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(explicitTemplate);
        ArgumentNullException.ThrowIfNull(lighting);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (lighting.SourceIdentity != Source.Identity || lighting.Binding != Binding ||
                frame.FrameNumber != lighting.FrameNumber || frame.PreparedCamera is null ||
                frame.PreparedCamera != lighting.SceneInputs.Camera)
                throw new InvalidDataException("Native Deferred lighting must use this constructed source, remake owner and the same accepted terrain/model Camera frame.");
            if (!Source.AllResourceBindingsReady)
                throw new NotSupportedException("Source-created Native Deferred lighting requires all five ready source resource lookups; null lookup fallback producers are unresolved.");
            if (Registration is null)
                throw new NotSupportedException("Native Deferred lighting requires the actual five-source device registration owner for this constructed handle generation.");
            Registration.ValidateConstructionSource(Source);
            ValidateCompleteTemplate(explicitTemplate);
            OriginalNativeDeferredPacket projected;
            if (Program.SourceSelection is { } selected)
            {
                var current = currentSelection ?? selected;
                Program.ValidateCompatibleCurrentSelection(current);
                var scene = lighting.SceneInputs;
                if (current.OwnedFeatureSource is { } feature)
                {
                    feature.ValidateFrame(current.Camera, lighting);
                    scene = scene with { Lighting = OriginalDeferredSceneConstants.ReadPreparedBaseLighting(
                        feature.EffectiveLocalLightData.Span[..0x1B0], checked((int)feature.Scratch.DirectionalCount)) };
                }
                writes = OriginalDeferredSelectedSceneConstants.Apply(current, explicitTemplate.Constants[0].Span, scene);
                var selectedConstants = explicitTemplate.Constants.ToDictionary(p => p.Key, p => (ReadOnlyMemory<byte>)p.Value.ToArray());
                selectedConstants[0] = writes.Bytes.ToArray();
                projected = explicitTemplate with { Constants = selectedConstants,
                    TemporaryTranslucencyViewport = lighting.SceneInputs.TranslucencyViewport ?? explicitTemplate.TemporaryTranslucencyViewport };
            }
            else projected = explicitTemplate.ApplySceneFields(Program, lighting.SceneInputs, out writes);
            var constants = new ReadOnlyDictionary<uint, ReadOnlyMemory<byte>>(
                projected.Constants.ToDictionary(p => p.Key, p => (ReadOnlyMemory<byte>)p.Value.ToArray()));
            return projected with { Constants = constants, Inputs = Array.AsReadOnly(projected.Inputs.ToArray()) };
        }
    }

    /// <summary>Validate the actual five source owners on the RenderingDevice
    /// before accepting their lighting into this frame's complete GPU packet.</summary>
    public async Task<OriginalNativeDeferredPacket> PrepareDeferredAsync(OriginalNativeMapFrameContext frame,
        OriginalNativeDeferredPacket explicitTemplate, OriginalMapSourceCreatedLightingPacket lighting,
        OriginalDeferredF103SelectedSource? currentSelection = null)
    {
        var registration = Registration ?? throw new NotSupportedException("Source-created Deferred submission requires its actual device registration owner.");
        await registration.ValidateAsync();
        return PrepareDeferred(frame, explicitTemplate, lighting, out _, currentSelection);
    }

    private void ValidateCompleteTemplate(OriginalNativeDeferredPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Constants); ArgumentNullException.ThrowIfNull(packet.Inputs);
        ArgumentNullException.ThrowIfNull(packet.Raster);
        var declarations = Program.DescriptorBindings.Bindings;
        var cbs = declarations.Where(d => d.DescriptorKind == "UniformBuffer").ToArray();
        var resources = declarations.Where(d => d.DescriptorKind != "UniformBuffer").ToArray();
        int removedPairs = Program.SourceNullTextureSamplerRegisters.Count;
        int removedLas = Program.LocalAmbientNullSpecialization is null ? 0 : 3;
        if ((Program.SourceSelection is null && (cbs.Length != 3 || resources.Length != 39 - removedPairs * 2 - removedLas)) ||
            packet.Constants.Count != cbs.Length ||
            cbs.Any(d => !packet.Constants.TryGetValue(d.Source.BindPoint, out var bytes) ||
                bytes.Length != d.ConstantBuffer!.ByteSize))
            throw new InvalidDataException("Source-created Deferred lighting requires the three complete caller constant buffers.");
        var keys = resources.Select(d => (d.DescriptorKind, d.Source.BindPoint)).ToHashSet();
        if (packet.Inputs.Any(i => i is null) || packet.Inputs.Count != keys.Count ||
            packet.Inputs.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != keys.Count ||
            packet.Inputs.Any(i => !keys.Contains((i.DescriptorKind, i.SourceBindPoint)) || !i.Resource.IsValid ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri) || (i.DescriptorKind == "Texture") != i.OwnerTexture.HasValue ||
                i.OwnerTexture is { } parent && !parent.IsValid))
            throw new InvalidDataException("Source-created Deferred lighting requires every original image, typed buffer and sampler binding with its live resource owner.");
    }

    private void ValidateDeviceContract()
    {
        if (Program.SourceSelection is not null)
        {
            if (Program.SelectedSourceNullSpecialization is { } nulls &&
                nulls.ProofPairs.Any(pair => !pair.SourceEvidence.StartsWith(Source.ConstructionProvenance + ";", StringComparison.Ordinal)))
                throw new InvalidDataException("Selected Hatching/Ramp proofs belong to another constructed light owner generation.");
            var selected = Program.ReadValidatedSelectedSourceDeviceBindings();
            if (selected.Interface.Sha256 != Program.Translation.SpirvSha256 ||
                !selected.Bindings.Select(DeclarationIdentity).SequenceEqual(Program.DescriptorBindings.Bindings.Select(DeclarationIdentity)))
                throw new InvalidDataException("Selected source-created lighting differs from its complete original/effective device interface.");
            return;
        }
        var original = Program.SourceDescriptorBindings.Bindings;
        if (original.Count != 42 || original.Count(d => d.DescriptorKind == "UniformBuffer") != 3 ||
            original.Count(d => d.DescriptorKind == "Texture") != 17 ||
            original.Count(d => d.DescriptorKind == "TextureBuffer") != 9 ||
            original.Count(d => d.DescriptorKind == "Sampler") != 13)
            throw new InvalidDataException("Source-created lighting requires the complete original42 Deferred declarations.");
        HashSet<uint> removed = [];
        OriginalTranslatedShaderBindings? verifiedDevice = null;
        if (Program.NullImageSpecialization is { } specialization)
        {
            var verified = OriginalDeferredNullImageAdapter.ReadDeviceBindings(Program.SourceDescriptorBindings, specialization);
            if (specialization.SourceEvidence.Count != specialization.NullPixelRegisters.Count ||
                specialization.SourceEvidence.Any(evidence => !evidence.StartsWith(Source.ConstructionProvenance + ";", StringComparison.Ordinal)) ||
                Program.LocalAmbientNullSpecialization is null && Program.IesSourceNull is null && (verified.Interface.Sha256 != Program.Translation.SpirvSha256 ||
                !verified.Bindings.Select(DeclarationIdentity).SequenceEqual(Program.DescriptorBindings.Bindings.Select(DeclarationIdentity))))
                throw new InvalidDataException("Deferred source-null shader specialization belongs to another constructed light owner or device interface.");
            removed = specialization.NullPixelRegisters.ToHashSet();
            verifiedDevice = verified;
        }
        if (Program.LocalAmbientNullSpecialization is { } las)
        {
            if (las.Source.CombinationIndex != Program.Combination.Index ||
                las.Source.Shader.Source.ResourceUri != Program.Source.Source.ResourceUri ||
                las.Source.Shader.Source.DecodedSha256 != Program.Source.Source.DecodedSha256 ||
                las.Preceding2D != Program.NullImageSpecialization)
                throw new InvalidDataException("Deferred LAS proof differs from this fresh Common/global generation or preceding source2D scope.");
            verifiedDevice = OriginalDeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(Program.SourceDescriptorBindings, las);
        }
        if (Program.IesSourceNull is { } ies)
        {
            ies.Validate();
            if (ies.CameraSource.Camera.ObjectId != Source.Identity.CameraObjectId ||
                ies.CameraSource.Resource.Source.ResourceUri != Source.Identity.CameraResourceUri ||
                ies.CameraSource.Resource.Source.DecodedSha256 != Source.Identity.CameraSha256)
                throw new InvalidDataException("Source IES null proof belongs to another selected Camera identity.");
            removed.Add(0);
        }
        verifiedDevice = Program.ReadValidatedSourceDeviceBindings();
        if (verifiedDevice is not null && (verifiedDevice.Interface.Sha256 != Program.Translation.SpirvSha256 ||
            !verifiedDevice.Bindings.Select(DeclarationIdentity).SequenceEqual(Program.DescriptorBindings.Bindings.Select(DeclarationIdentity))))
            throw new InvalidDataException("Deferred source-null proof differs from the final translated device interface.");
        if (!original.Where(d => !(d.DescriptorKind is "Texture" or "Sampler" && removed.Contains(d.Source.BindPoint)) &&
                !(Program.LocalAmbientNullSpecialization is not null && d.Source.InputType == 2 && d.Source.BindPoint is 22 or 23 or 24))
                .Select(DeclarationIdentity).SequenceEqual(Program.DescriptorBindings.Bindings.Select(DeclarationIdentity)))
            throw new InvalidDataException("Source-created lighting must retain every original Deferred declaration except proven source-null image/sampler pairs.");
    }

    private static (string Kind, uint Register, uint Variable, uint? Set, uint? Binding) DeclarationIdentity(OriginalTranslatedShaderBinding value)
        => (value.DescriptorKind, value.Source.BindPoint, value.Device.VariableId, value.Device.DescriptorSet, value.Device.Binding);

    /// <summary>Retire this CPU provider after its scene callbacks have retired.
    /// Borrowed program and device resource lifetimes remain with their owners.</summary>
    public void Dispose()
    {
        lock (gate) retired = true;
    }
}
