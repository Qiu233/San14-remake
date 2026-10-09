using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum DeferredShadowOwnerScope { FreshConstructionBeforeAssignments, CurrentPreparedOwner }
public enum DeferredShadowSelectionAction { RetainBinding, RequireDeclaredArray, RequireFallbackOwner, MissingDescriptor }

public sealed record DeferredShadowArraySource(uint PixelRegister, int SourceIndex,
    TaskGraphArgument SourceArgument, uint ObjectNameHash, LoadedObjects Source, DokObject Object);
public sealed record DeferredShadowDeclaration(uint PixelRegister, string Name, short EncodedParameter,
    uint Dimension, G1ShaderResourceBinding Mapping, DxbcRdefBinding PixelBinding,
    G1ShaderConfigurationRecord Plugin, G1ShaderConfigurationParameter Maximum,
    G1ShaderConfigurationParameter Filter, G1ShaderConfigurationParameter ConstantBuffer,
    G1ShaderConfigurationParameter SoftShadowType, DxbcRdefBinding ConstantBinding,
    DxbcRdefBuffer ConstantDefinition, IReadOnlyList<DeferredShadowArraySource> Arrays);
public sealed record LoadedDeferredSourceShadowBindings(LoadedDeferredTexturePlan Deferred,
    DeferredTargetImageSource RefractionDepth, DeferredShadowDeclaration Point,
    DeferredShadowDeclaration Spot);

/// <summary>Recovered F103 selection inputs. Resolution is current context+A8C/A90;
/// owner count is the current global+82C, before plugin MaxShadows clamping.
/// A declared object is a target declaration; successful creation/content belongs to its producer.</summary>
public readonly record struct DeferredShadowSelectionInputs(int EffectiveView,
    bool ShaderInterfacePresent, bool GlobalOwnerPresent, uint GlobalOwnerCount,
    int ContextResolution, bool DeclaredArrayDescriptorReady, bool FallbackDescriptorPresent);
public sealed record DeferredShadowSelection(DeferredShadowDeclaration Declaration,
    int NativeArrayIndex, DeferredShadowSelectionAction Action,
    DeferredShadowArraySource? DeclaredArray, string SourceEvidence);

/// <summary>Exact source shader, graph target identities and selection gates.
/// Zero count suppresses array selection; it does not prove a null shadow image.</summary>
public static partial class DeferredSourceShadowBindings
{
    public const int PointConstantBufferBytes = 12320;
    public const int SpotConstantBufferBytes = 20512;
    public const int SourceMaximumShadows = 256;

    public static LoadedDeferredSourceShadowBindings Read(LoadedDeferredTexturePlan deferred)
    {
        ArgumentNullException.ThrowIfNull(deferred);
        var images = DeferredImageSources.Read(deferred);
        if (deferred.Node.Index != 166 || deferred.ResourceBindings.Combination.Index != 0 ||
            deferred.Shader.Source.FileId != 0x9D8E811F)
            throw Error("requires exact original9D8E811F combination0/node166");
        var configurations = G1ShaderConfigurationReader.Read(deferred.Shader.Source.Bytes, deferred.Shader.Container);
        var definitions = deferred.Shader.ReadResourceDefinitions(1);
        var originalTextures = new LoadedObjects(DokReader.Read(deferred.TextureObjects.Source.Bytes), deferred.TextureObjects.Source);
        var graph = RenderGraphReader.Read(deferred.Graph.Source.Bytes);
        var node = graph.Nodes.Single(n => n.Index == deferred.Node.Index);
        DeferredShadowDeclaration Declaration(uint register, string name, short encoded, uint dimension,
            string pluginName, uint pluginHash, uint arrayFieldHash, uint arrayClass, uint cbRegister,
            string cbName, int cbBytes, (string Name, uint Offset, uint Size)[] variables)
        {
            var plugins = configurations.Records.Where(r => r.Name == pluginName && r.NameHash == pluginHash).ToArray();
            var mappings = deferred.ResourceBindings.Entries.Where(r => r.Name == name).ToArray();
            var bindings = definitions.Bindings.Where(r => r.InputType == 2 && r.Name.Text == name).ToArray();
            var constantBindings = definitions.Bindings.Where(r => r.InputType == 0 && r.Name.Text == cbName).ToArray();
            var buffers = definitions.Buffers.Where(r => r.Name.Text == cbName).ToArray();
            var arguments = node.Call.Arguments.Where(a => a.NameHash == arrayFieldHash).ToArray();
            if (plugins.Length != 1 || mappings.Length != 1 || bindings.Length != 1 || constantBindings.Length != 1 ||
                buffers.Length != 1 || arguments.Length != 1 || arguments[0].DescriptorWord != 0x05000020 ||
                arguments[0].RawValue.Length != 128 || mappings[0].PrimaryEncoded != encoded ||
                mappings[0].SlotIndex != (short)register || bindings[0].BindPoint != register ||
                bindings[0].Dimension != dimension || bindings[0].ReturnType != 5 || bindings[0].BindCount != 1 ||
                constantBindings[0].BindPoint != cbRegister || constantBindings[0].BindCount != 1 ||
                buffers[0].ByteSize != cbBytes || buffers[0].Variables.Count != variables.Length)
                throw Error("source shadow declarations differ from their original graph/configuration/PS1 consumer");
            foreach (var expected in variables)
            {
                var fields = buffers[0].Variables.Where(v => v.Name.Text == expected.Name).ToArray();
                if (fields.Length != 1 || fields[0].StartOffset != expected.Offset || fields[0].ByteSize != expected.Size)
                    throw Error("source shadow constant fields differ from their exact reflected spans");
            }
            var plugin = plugins[0];
            G1ShaderConfigurationParameter Field(uint hash, uint value)
            {
                var field = plugin.Find(hash);
                if (field is null || field.RawValue != value) throw Error("shadow plugin differs from locked source literals");
                return field;
            }
            var rows = new List<DeferredShadowArraySource>(32);
            for (int index = 0; index < 32; index++)
            {
                uint key = BinaryPrimitives.ReadUInt32LittleEndian(arguments[0].RawValue.AsSpan(index * 4, 4));
                var objects = originalTextures.Document.Objects.Where(o => o.ObjectId == key).ToArray();
                if (key == 0 || objects.Length != 1 || objects[0].Kind != DokObjectKind.Inline || objects[0].ClassId != arrayClass)
                    throw Error("source shadow array key lacks its exact original target object/class");
                rows.Add(new(register, index, arguments[0], key, originalTextures, objects[0]));
            }
            return new(register, name, encoded, dimension, mappings[0], bindings[0], plugin,
                Field(0x0F32BA9F, 256), Field(0x3F53844C, 2), Field(0xE8637B67, cbRegister),
                Field(0x0A705E44, 0x7FFFFFFF), constantBindings[0], buffers[0], rows.AsReadOnly());
        }
        return new(deferred, images.RefractionDepth,
            Declaration(3, "sSplShadow", 46, 10, "SubPointLightShadows", 0xE025C265, 0xB0603860, 0x04F7DD1F,
                1, "SplsCB", PointConstantBufferBytes,
                [("vSplsNumSS", 0, 12), ("vSubCubSz", 16, 16), ("vSubCbW2S", 32, 8192), ("fSplSize", 8224, 4084)]),
            Declaration(4, "sSslShadow", 51, 5, "SubSpotLightShadows", 0x62D98723, 0xDE198A68, 0xD8272294,
                2, "SslsCB", SpotConstantBufferBytes,
                [("vSslsNumSS", 0, 12), ("vSubSMSz", 16, 16), ("mSubSptW2S", 32, 16384), ("vSslSize", 16416, 4096)]));
    }

    /// <summary>TC C5B710/C997C0 with divisor16 and F103's finite source arrays.
    /// An absent selected descriptor still tries the supplied fallback owner.
    /// MissingDescriptor is not permission to allocate a substitute or bind RID0.</summary>
    public static DeferredShadowSelection Select(LoadedDeferredSourceShadowBindings source,
        uint pixelRegister, DeferredShadowSelectionInputs input)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declaration = pixelRegister switch { 3 => source.Point, 4 => source.Spot, _ => throw Error("shadow register must be3 or4") };
        int index = NativeArrayIndex(pixelRegister, input.EffectiveView, input.GlobalOwnerPresent,
            input.GlobalOwnerCount, input.ContextResolution);
        var array = index is >= 0 and < 32 ? declaration.Arrays[index] : null;
        var action = !input.ShaderInterfacePresent ? DeferredShadowSelectionAction.RetainBinding :
            array is not null && input.DeclaredArrayDescriptorReady ? DeferredShadowSelectionAction.RequireDeclaredArray :
            input.FallbackDescriptorPresent ? DeferredShadowSelectionAction.RequireFallbackOwner :
            DeferredShadowSelectionAction.MissingDescriptor;
        return new(declaration, index, action, array,
            $"9D8E811F:combo0;F103:EACF0B..EAD07A/EADCA4..EADD2F;" +
            $"t{pixelRegister};effectiveView={input.EffectiveView};globalCount={input.GlobalOwnerCount};" +
            $"resolution={input.ContextResolution};index={index};fallback={input.FallbackDescriptorPresent}");
    }

    public static int NativeArrayIndex(uint pixelRegister, int effectiveView, bool globalOwnerPresent,
        uint ownerCount, int contextResolution)
    {
        if (pixelRegister is not (3 or 4)) throw Error("shadow register must be3 or4");
        if (effectiveView != 0 || !globalOwnerPresent || ownerCount == 0 || contextResolution <= 0) return -1;
        int bin = pixelRegister == 3 ? contextResolution switch
        { < 256 => 0, < 512 => 1, < 1024 => 2, < 2048 => 3, < 4096 => 4, _ => 5 } :
            contextResolution switch { < 1024 => 0, < 2048 => 1, < 4096 => 2, _ => 3 };
        int index = checked(bin * 16 + (int)((ownerCount - 1) / 16));
        return index;
    }

    /// <summary>Declared array sampler after the selected owner exists. The
    /// original class loader supplies its full key; Filter2 applies comparison
    /// filtering with the actual MaxShadows-clamped count. Fallback owners need
    /// their own complete final descriptor key and cannot use this declaration.</summary>
    public static DeferredSamplerSourceInput ReadSelectedArraySampler(
        LoadedDeferredSamplerNativeSource native, DeferredShadowSelection selection,
        uint currentOwnerCount)
    {
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(selection);
        if (selection.Action != DeferredShadowSelectionAction.RequireDeclaredArray ||
            selection.DeclaredArray is not { } array || array.PixelRegister != selection.Declaration.PixelRegister ||
            array.SourceIndex != selection.NativeArrayIndex)
            throw Error("shadow sampler requires its selected declared array owner; fallback and missing descriptors require their actual source keys");
        uint count = Math.Min(currentOwnerCount, selection.Declaration.Maximum.RawValue);
        return array.PixelRegister == 3
            ? RenderSamplerSourceAdapters.FromSubPointCubeArray(native, array.Source, array.ObjectNameHash, count)
            : RenderSamplerSourceAdapters.FromSubSpotRenderArray(native,
                RenderTextureResources.Read(array.Source, array.ObjectNameHash), count);
    }

    internal static InvalidDataException Error(string message) => new("Original Deferred source shadows: " + message + ".");
}
