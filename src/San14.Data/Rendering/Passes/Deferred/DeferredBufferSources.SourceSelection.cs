using San14.Data.Formats;

namespace San14.Data;

public static partial class DeferredBufferSources
{
    /// <summary>Read only the populated typed-buffer roles declared by the
    /// exact selected original program. Absent groups require no upload; the
    /// existing current record/count/membership validators remain unchanged.</summary>
    public static LoadedDeferredBufferLayout ReadSelectedLayout(DeferredF103SelectedSource selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var shader = new LoadedShaderContainer(G2ShaderContainerReader.Read(selected.SourcePlan.Shader.Source.Bytes),
            selected.SourcePlan.Shader.Source);
        var definitions = shader.ReadResourceDefinitions(selected.PixelProgramIndex);
        var configurations = G1ShaderConfigurationReader.Read(shader.Source.Bytes, shader.Container);
        var rows = new List<DeferredBufferDeclaration>();
        foreach (var expected in Slots)
        {
            var bindings = definitions.Bindings.Where(b => b.InputType == 2 && b.Name.Text == expected.Name).ToArray();
            if (bindings.Length == 0) continue;
            var configuration = configurations.Records.Single(r => r.Name == expected.Configuration &&
                r.NameHash == (expected.Configuration == "ClusterLightList" ? 0x10BCD0CAu : 0xC1BD0501u));
            var slot = configuration.Find(expected.Hash);
            if (slot is null || slot.RawValue != expected.Register || bindings.Length != 1 ||
                bindings[0].BindPoint != slot.RawValue || bindings[0].BindCount != 1 || bindings[0].Dimension != 1 ||
                bindings[0].ReturnType != expected.ReturnType)
                throw Error("selected plugin slot differs from its exact named typed-buffer consumer");
            rows.Add(new(expected.Register, expected.Name, configuration, slot, bindings[0]));
        }
        return new(shader, selected.Combination.Index, rows.AsReadOnly());
    }
}
