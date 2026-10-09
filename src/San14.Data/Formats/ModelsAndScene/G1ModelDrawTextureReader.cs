namespace San14.Data.Formats;

public sealed record G1DrawTextureBinding(G1DrawRecord Draw, uint RawIndex24, G1TextureRecord TextureRecord);
public sealed record G1ModelDrawTextures(G1ModelDrawBindings Draws, G1ModelMaterialRecords Materials,
    IReadOnlyList<G1DrawTextureBinding> Bindings);

/// <summary>
/// Resolves the verified GM4400 draw +24 texture-record reference within one
/// geometry document. Both source tables are read from that document; callers
/// cannot supply unrelated draw/material lists. +12/+20, parameters, shader,
/// texture usage and material execution semantics remain uninterpreted.
/// </summary>
public static class G1ModelDrawTextureReader
{
    public static G1ModelDrawTextures Read(G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        G1ModelDrawBindings draws = G1ModelDrawReader.Read(geometry);
        G1ModelMaterialRecords materials = G1ModelMaterialReader.Read(geometry);
        var bindings = new List<G1DrawTextureBinding>(draws.Draws.Count);
        foreach (G1DrawRecord draw in draws.Draws)
        {
            if (draw.RawIndex24 >= materials.TextureRecords.Count)
                throw new InvalidDataException($"G1M draw texture binding draw {draw.Index}: raw +24 index {draw.RawIndex24} is outside 0x10002 texture record count {materials.TextureRecords.Count}.");
            bindings.Add(new(draw, draw.RawIndex24, materials.TextureRecords[(int)draw.RawIndex24]));
        }
        return new(draws, materials, bindings);
    }
}
