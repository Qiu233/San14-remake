using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public static partial class Sky2NullTextureAdapter
{
    public const string FiniteMoonAtmosphere5SourceSha = "E0F26D96BC871BA23979AE4DE3996C7638AAD2DFE526CDDD907D0FBBBA296719";
    public const string FiniteMoonAtmosphere11SourceSha = "B0A176C05E2476DAA787BB75028185CDB59CCC7F344AFC3C782F2CDCECE6C4FF";
    public const string FiniteMoonContribution6SourceSha = "0B6E8D02E2CB98706826E7935AC9249429D758FFCE2BDD502821F469F0A3DBF2";
    public const string FiniteMoonAtmosphere14SourceSha = "4A875CA4C6C2398BE10A7365C2FA1703E621BE3736E3DF3E22735642C9CD2EE2";
    public const string FiniteMoonContribution15SourceSha = "9956328CDE2DCEF6C2D4F0E56059010C332442B070BC328481DA2035C2C9D8BF";
    private const string FiniteMoonCompilerSha = "37885422AD210365C8FFCDA9F40A7F321842139698C6E1E6134DB20BA5D65780";
    private sealed record FiniteMoonProfile(int Index, string SourceSha, string TranslationSha, string RdefSha, int DescriptorCount);

    private static FiniteMoonProfile? FiniteMoonProfileFor(string sourceSha) => sourceSha switch
    {
        FiniteMoonAtmosphere5SourceSha => new(5, sourceSha,
            "18B49CFED166894B26EFA7B0455380B5075F52D947688F8F625825F399967A5B",
            "38B751CB991636ABF34C9601A3E3D17A7302FB13361B9F11E10546D49A721E94", 13),
        FiniteMoonAtmosphere11SourceSha => new(11, sourceSha,
            "1D91112710E7F7B633A8531F704DBA66EE67B71F12864EF322DAD84D5A4C2141",
            "C75AFBBD8111BA6AD879DBDEBEABC93084A4187CA4E24F00ADAABD0097555B8B", 13),
        FiniteMoonContribution6SourceSha => new(6, sourceSha,
            "AF84E7D69D33413C94BB59F77E92D6A98BECB1E9BFA41C0BF0DDA57E217CC203",
            "5FCC1A1ABE0E26D38F798EF51213BEEDEEF65836F646674DA805100CA3108036", 7),
        FiniteMoonAtmosphere14SourceSha => new(14, sourceSha,
            "D3D3A516E4A764764F858509D08912CFCDA35B1348089A661D0E1EB5C43C5B6A",
            "30FDE2291D3BFF47CBA845E785A5E961011296EDA08BB257AFAFAD5CD4EE7548", 13),
        FiniteMoonContribution15SourceSha => new(15, sourceSha,
            "9902A4462E5CCC0A3193A321DA0035E385868C1E715A72FED1873AE121AF53A3",
            "B281017C3176F7A469183717D4919AAA25B27B8DAF5A02D4ACECEC5E80BA2123", 7),
        _ => null
    };

    private static FiniteMoonProfile? RequireFiniteMoonProfileIfSelected(ShaderSpirv pixel,
        TranslatedShaderBindings bindings)
    {
        var profile = FiniteMoonProfileFor(pixel.SourceProgramSha256);
        if (profile is null) return null;
        if (pixel.SpirvSha256 != profile.TranslationSha || HashFiniteMoon(pixel.Bytes) != profile.TranslationSha ||
            pixel.CompilerLibrarySha256 != FiniteMoonCompilerSha || pixel.ExplicitDescriptorSet is not null ||
            pixel.BufferUavsAsStorageBuffers || bindings.Bindings.Count != profile.DescriptorCount ||
            FingerprintFiniteMoonRdef(bindings.SourceDefinitions) != profile.RdefSha)
            throw new InvalidDataException("Finite Moon null2D requires its exact original PS5/11/6/14/15, implicit production compiler and complete original RDEF fingerprint.");
        // Re-read the pinned binary, rather than trusting an interface object
        // whose fields or descriptor/source pairs could have been edited.
        var canonical = TranslatedShaderBindingReader.Read(bindings.SourceDefinitions, pixel.Bytes);
        if (!JsonSerializer.SerializeToUtf8Bytes(canonical).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(bindings)))
            throw new InvalidDataException("Finite Moon null2D interface or source binding metadata differs from the complete pinned translation.");
        return profile;
    }

    /// <summary>Validate the same null bindings retained at program creation
    /// and the complete current CB. This checks texture presence, not light
    /// setter absence: MoonRGB remains finite and is still read by PS5/11/6/14/15.
    /// SourceZeroMoon's unused-RGB exemption cannot authorize this family.</summary>
    public static void ValidateNullTexturePacket(Sky2NullTextureSpirv adapter,
        IReadOnlyList<Sky2NullTextureBinding> actualProgramProofs,
        DxbcResourceDefinitions originalDefinitions, ReadOnlySpan<byte> completePixelPacket)
        => ReadLoadedPacketInterface(adapter, originalDefinitions)
            .Validate(actualProgramProofs, originalDefinitions, completePixelPacket);

    /// <summary>Import the fixed compiled program/interface once. Current
    /// packet layout and finite Moon inputs are checked at draw construction.</summary>
    public static Sky2LoadedNullTexturePacketInterface ReadLoadedPacketInterface(
        Sky2NullTextureSpirv adapter, DxbcResourceDefinitions originalDefinitions)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(originalDefinitions);
        if (HashFiniteMoon(adapter.Bytes) != adapter.Sha256 ||
            adapter.NullTextureBindings.Count == 0 ||
            !adapter.NullTextureBindings.Select(p => p.PixelRegister).Order().SequenceEqual(adapter.NullPixelRegisters))
            throw new InvalidDataException("Loaded Sky2 null interface requires its exact compiled adapter and texture proof roster.");
        var profile = FiniteMoonProfileFor(adapter.SourcePixelSha256);
        if (profile is not null)
        {
            if (adapter.SourcePhysicalPixelProgramIndex != profile.Index ||
                adapter.UnspecializedTranslationSha256 != profile.TranslationSha ||
                FingerprintFiniteMoonRdef(originalDefinitions) != profile.RdefSha ||
                adapter.NullPixelRegisters.Any(s => s is not (4 or 5)))
                throw new InvalidDataException("Loaded finite Moon interface differs from its exact source program.");
        }
        else if (!((adapter.SourcePhysicalPixelProgramIndex == 3 && adapter.SourcePixelSha256 == DefaultPixelSourceSha) ||
              (adapter.SourcePhysicalPixelProgramIndex == 4 && adapter.SourcePixelSha256 == ContributionPixelSourceSha)))
            throw new InvalidDataException("Loaded Sky2 null interface lacks an admitted original program identity.");
        var buffer = originalDefinitions.Buffers.Single();
        var presence = buffer.Variables.Single(v => v.Name.Text == "g_vSkyInfo1");
        int packetLength = profile is null ? 256 : 336;
        if (buffer.ByteSize != packetLength || presence.ByteSize != 16)
            throw new InvalidDataException("Loaded Sky2 null interface has a different complete source CB/presence field.");
        var lanes = new int[adapter.NullTextureBindings.Count];
        for (int index = 0; index < lanes.Length; index++)
        {
            var proof = adapter.NullTextureBindings[index];
            proof.Validate();
            lanes[index] = proof.Role switch
            {
                SkySamplingRole.Earth when profile is null && proof.PixelRegister == 3 => 0,
                SkySamplingRole.MilkyWay when proof.PixelRegister == 4 => 2,
                SkySamplingRole.Moon when profile is not null && proof.PixelRegister == 5 => 1,
                _ => throw new InvalidDataException("Sky2 current null packet's typed role differs from its admitted source shader.")
            };
        }
        return new(adapter.NullTextureBindings, originalDefinitions, packetLength,
            presence.StartOffset, lanes, profile is not null);
    }

    private static string HashFiniteMoon(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    // Fingerprint every exposed original RDEF record/range and extension, not
    // only CB byte length or a subset of fields. Rows are also emitted by the
    // independent Python DXBC reader in the source report for auditability.
    private static string FingerprintFiniteMoonRdef(DxbcResourceDefinitions d)
    {
        static string Name(DxbcRdefString? name) => name is null ? "-" : $"{name.Text}|{name.EncodedRange.Offset}|{name.EncodedRange.Length}";
        static string Range(DxbcRange? range) => range is null ? "-" : $"{range.Offset}|{range.Length}";
        var rows = new List<string>
        {
            $"H|{d.ProgramByteLength}|{Range(d.PayloadRangeInProgram)}|{d.MajorVersion}|{d.MinorVersion}|{d.RawShaderType}|{d.CompileFlags}|{d.InterfaceSlotCount}|{Convert.ToHexString(d.RawHeader)}",
            "C|" + Name(d.Creator), $"R|{Range(d.BufferTableRange)}|{Range(d.BindingTableRange)}"
        };
        foreach (var b in d.Bindings)
            rows.Add($"B|{b.Index}|{Range(b.RowRange)}|{Name(b.Name)}|{b.InputType}|{b.ReturnType}|{b.Dimension}|{b.NumSamples}|{b.BindPoint}|{b.BindCount}|{b.Flags}");
        foreach (var b in d.Buffers)
        {
            rows.Add($"U|{b.Index}|{Range(b.RowRange)}|{Name(b.Name)}|{b.ByteSize}|{b.Flags}|{b.BufferType}|{Range(b.VariableTableRange)}");
            foreach (var v in b.Variables)
                rows.Add($"V|{v.Index}|{Range(v.RowRange)}|{Name(v.Name)}|{v.StartOffset}|{v.ByteSize}|{v.Flags}|{v.TypeOffset}|{Range(v.DefaultValueRange)}|{v.StartTexture}|{v.TextureSize}|{v.StartSampler}|{v.SamplerSize}");
        }
        foreach (var t in d.Types.OrderBy(t => t.Offset))
        {
            rows.Add($"T|{t.Offset}|{Range(t.RowRange)}|{t.Class}|{t.BaseType}|{t.Rows}|{t.Columns}|{t.Elements}|{Range(t.MemberTableRange)}|{Convert.ToHexString(t.RawTypeExtension)}|{Name(t.Name)}");
            foreach (var m in t.Members)
                rows.Add($"M|{m.Index}|{Range(m.RowRange)}|{Name(m.Name)}|{m.TypeOffset}|{m.ByteOffset}");
        }
        return HashFiniteMoon(Encoding.UTF8.GetBytes(string.Join('\n', rows)));
    }
}
