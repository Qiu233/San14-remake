using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedTerrainShaderResources(LoadedTerrainParameterResources Parameters,
    DokProperty ShaderReference, LoadedShaderContainer Shader);

/// <summary>
/// Follows Landscape's explicit PB2DSGbG1SFileResourceHash through the scene's
/// already resolved wrapper. This identifies the G-buffer shader source; it does
/// not select an active feature combination or a later lighting/shadow program.
/// </summary>
public static class TerrainShaderResources
{
    public const uint GBufferShaderPropertyHash = 0x3AF82377;

    public static LoadedTerrainShaderResources Load(GameResourceRepository repository,
        LoadedTerrainParameterResources parameters)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(parameters);
        string context = $"{parameters.Wrapper.Source.ResourceUri} Landscape {parameters.WrapperRoot.ObjectId:X8}";
        DokProperty[] matches = parameters.WrapperRoot.Properties
            .Where(property => property.NameHash == GBufferShaderPropertyHash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != 5 || matches[0].ElementCount != 1 || matches[0].RawValue.Length != 4)
            throw new InvalidDataException($"{context}: expected one PB2DSGbG1SFileResourceHash type5 scalar.");
        DokProperty reference = matches[0];
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue);
        if (id == 0) throw new InvalidDataException($"{context}: G-buffer shader resource reference is zero.");
        try { return new(parameters, reference, repository.LoadShaderContainer("system", id)); }
        catch (Exception error) when (error is InvalidDataException or KeyNotFoundException)
        {
            throw new InvalidDataException($"{context}: G-buffer shader san14://rdb/system/{id:X8}: {error.Message}", error);
        }
    }
}
