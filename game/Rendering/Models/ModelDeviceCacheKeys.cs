using San14.Data;

/// <summary>Content identity supplied by resource import. Provenance paths
/// describe where bytes came from and do not change a device allocation.</summary>
internal readonly record struct ModelResourceVersion(string ResourceUri, string ContentVersion)
{
    public static ModelResourceVersion From(LoadedRdbAsset source)
        => new(source.ResourceUri, source.DecodedSha256);
}

internal readonly record struct ModelProgramCacheKey(ModelResourceVersion Source,
    int Combination, ArmySkinVertexProfile? ArmyProfile);
internal readonly record struct ModelTextureCacheKey(ModelResourceVersion Source, int Image, uint View);
