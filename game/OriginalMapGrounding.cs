using Godot;

/// <summary>Ground projected markers from adjacent source MHK samples.</summary>
public static class OriginalMapGrounding
{
    public static float SampleHeight(OriginalTerrainMeshResult terrain, double x, double z)
    {
        var height = terrain.Source.HeightField.HeightField;
        double value = height.InterpolateSurfaceHeight(x, z);
        if (!double.IsFinite(value) || !float.IsFinite((float)value))
            throw new InvalidDataException("Projected map marker height is not finite.");
        return (float)value;
    }
}
