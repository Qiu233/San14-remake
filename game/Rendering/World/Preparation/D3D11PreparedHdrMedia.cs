using System.Globalization;
using San14.Data;

public sealed record D3D11PreparedHdrPass(string Identity, HdrShaderDeclaration Declaration,
    HdrPreparedProgram Program, uint OuterMask, uint InnerMask,
    string ResourceDirectory, string CameraResourceDirectory);

/// <summary>Temporary selection bridge for an existing prepared native bank.
/// Only its pass roster, population gates and program pair identify the stored
/// selection. Programs, reflection and material bytes come from the installed
/// resources. This is not the missing live Native View/query selection bridge.
/// Remove this importer when that owner selects from HdrProgramBank directly.</summary>
public static class D3D11PreparedHdrMedia
{
    public static IReadOnlyList<D3D11PreparedHdrPass> Prepare(
        HdrShaderResources resources, string terrainDirectory)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(terrainDirectory))!;
        var result = new List<D3D11PreparedHdrPass>();
        void Import(string directory, string passList, bool accessory)
        {
            foreach (string row in File.ReadLines(Path.Combine(directory, passList)))
            {
                string identity = row.Trim();
                if (identity.Length == 0) continue;
                string pass = Path.Combine(directory, identity);
                int[] gate = File.ReadAllText(Path.Combine(pass, "population-gate.txt"))
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                if (gate.Length != 4)
                    throw new InvalidDataException("HDR population gate requires node, inner, geometry and outer fields.");
                var declaration = resources[gate[0]];
                var program = declaration.Programs.ResolvePreparedProgram(
                    File.ReadAllBytes(Path.Combine(pass, "vs.dxbc")),
                    File.ReadAllBytes(Path.Combine(pass, "ps.dxbc")));
                result.Add(new(identity, declaration, program, checked((uint)gate[3]), checked((uint)gate[1]),
                    pass, accessory ? Path.Combine(directory, "camera-source", identity)
                        : Path.Combine(parent, "deferred-camera-continuous-input", identity)));
            }
        }
        string early = Path.Combine(parent, "early-hdr-input");
        Import(early, "passes.txt", false);
        Import(early, "ocean-passes.txt", false);
        Import(early, "late-passes.txt", false);
        string accessory = Path.Combine(parent, "city-accessory-next-input", "hdr", "initial");
        Import(accessory, "passes.txt", true);
        Import(accessory, "late-passes.txt", true);
        if (result.Select(pass => pass.Identity).Distinct(StringComparer.Ordinal).Count() != result.Count)
            throw new InvalidDataException("HDR routes must have unique identities across their native banks.");
        // Resolve the whole bank before replacing any cached media.
        foreach (var pass in result) resources.WritePreparedProgram(pass.ResourceDirectory, pass.Declaration, pass.Program);
        return result.AsReadOnly();
    }
}
