using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    // Separate channels preserve the deliberately narrow knowledge copied
    // into old governor/home/transaction adapters. All definitions are immutable.
    private MapTopology? _hexIdentityDefinitions, _hexAreaDefinitions, _hexTerrainDefinitions;
    private bool _emptyHexObjectLinksKnown;

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
        //Home did not carry terrain or object-link inputs. Do not broaden it.
    }

    private void CopyTransactionTerrainDefinitionsTo(SourceWorldContext draft)
        => draft._hexTerrainDefinitions = _hexTerrainDefinitions;
}
