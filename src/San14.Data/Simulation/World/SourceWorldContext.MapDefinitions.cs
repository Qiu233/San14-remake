using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // Separate channels preserve the deliberately narrow knowledge copied
    // into old governor/home/transaction adapters. All definitions are immutable.
    private MapTopology? _hexIdentityDefinitions, _hexAreaDefinitions, _hexTerrainDefinitions;
    // Standalone source adapters retain the original allocation profile.
    // Campaign captures bind their existing immutable MapTopology first.
    internal const int OriginalMapWidth = 220, OriginalMapHeight = 220;
    internal int MapWidth => _hexIdentityDefinitions?.Width ?? OriginalMapWidth;
    internal int MapHeight => _hexIdentityDefinitions?.Height ?? OriginalMapHeight;
    internal int MapTileCount => _hexIdentityDefinitions?.TileCount ?? OriginalMapWidth * OriginalMapHeight;
    internal bool ContainsMapHex(int hex) => (uint)hex < (uint)MapTileCount;

    private bool _emptyHexObjectLinksKnown;
    private IReadOnlyDictionary<int, TerritoryState>? _currentTerritoryOwnership;

    internal bool BindCurrentTerritoryOwnership(IReadOnlyDictionary<int, TerritoryState> territory)
    {
        if (territory.Count != MapTileCount) return false;
        _hexForces.Clear();
        _currentTerritoryOwnership = territory;
        return true;
    }

    internal void BindMapIdentityDefinitions(MapTopology topology)
        => _hexIdentityDefinitions = topology;

    internal void BindMapDefinitions(MapTopology topology)
    {
        _hexIdentityDefinitions = _hexAreaDefinitions = _hexTerrainDefinitions = topology;
        _emptyHexObjectLinksKnown = true;
    }

    private void CopyHomeMapDefinitionsTo(SourceWorldContext draft)
    {
        draft._hexIdentityDefinitions = _hexIdentityDefinitions;
        draft._hexAreaDefinitions = _hexAreaDefinitions;
        draft._currentTerritoryOwnership = _currentTerritoryOwnership;
        //Home did not carry terrain or object-link inputs. Do not broaden it.
    }

    private void CopyTransactionTerrainDefinitionsTo(SourceWorldContext draft)
        => draft._hexTerrainDefinitions = _hexTerrainDefinitions;
}
