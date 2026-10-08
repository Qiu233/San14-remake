namespace San14.Data.Formats;

public sealed record G1DrawMaterialBinding(G1DrawRecord Draw,
    G1Section10001Record MaterialRecord, G1ParameterBlock ParameterBlock);
public sealed record G1ModelDrawMaterials(G1ModelDrawBindings DrawBindings,
    G1ModelMaterialRecords Materials, IReadOnlyList<G1DrawMaterialBinding> Draws);

/// <summary>Original F5AB90 reads disk draw +12 as the 64-byte 10001 material
/// record index and +20 as the 10003 parameter-block index. Values inside both
/// source records remain raw; this reader does not invent shader constants.</summary>
public static class G1ModelDrawMaterialReader
{
    public static G1ModelDrawMaterials Read(G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        G1ModelDrawBindings bindings = G1ModelDrawReader.Read(geometry);
        G1ModelMaterialRecords materials = G1ModelMaterialReader.Read(geometry);
        var draws = new List<G1DrawMaterialBinding>(bindings.Draws.Count);
        foreach (G1DrawRecord draw in bindings.Draws)
        {
            if (draw.RawIndex12 >= materials.Section10001Records.Count)
                throw new InvalidDataException($"Original G1M draw {draw.Index} +12 material record {draw.RawIndex12} exceeds 10001 records.");
            if (draw.RawIndex20 >= materials.ParameterBlocks.Count)
                throw new InvalidDataException($"Original G1M draw {draw.Index} +20 parameter block {draw.RawIndex20} exceeds 10003 records.");
            draws.Add(new(draw, materials.Section10001Records[(int)draw.RawIndex12],
                materials.ParameterBlocks[(int)draw.RawIndex20]));
        }
        return new(bindings, materials, draws.AsReadOnly());
    }
}
