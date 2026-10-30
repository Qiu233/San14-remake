using Godot;
using San14.Data;

public partial class MapSceneView
{
    private CgQuality _cgQuality = CgQuality.Normal;
    private bool _cgQualityApplied;
    private bool _cgQualityRebuilding;
    private static readonly IReadOnlyList<CgQuality> AllCgQualities = Array.AsReadOnly(
        new[] { CgQuality.Normal, CgQuality.Low, CgQuality.Lowest });

    public CgQuality ReadCgQuality() => _cgQuality;
    public bool IsCgQualityRebuilding => _cgQualityRebuilding;
    public string? CgQualityChangeUnavailableReason => _cgQualityRebuilding
        ? "cg-quality.resource-replacement" : CgQualityBorrowerRestriction();
    public IReadOnlyList<CgQuality> SupportedCgQualities => CgQualityChangeUnavailableReason is null
        ? AllCgQualities : Array.AsReadOnly(new[] { _cgQuality });

    private string? CgQualityBorrowerRestriction()
    {
        if (_nativeMapClosing) return "cg-quality.map-retirement";
        if (!_loaded) return null;
        if (_periodMapOwner is not null || _periodMapActive) return "cg-quality.period-presentation-owner";
        if (storyPresentationOwner is not null || uniqueDirectionOwner is not null)
            return "cg-quality.event-presentation-owner";
        if (_captureEffects?.HasRetainedEffectOwners == true || _fireDispatches.Count != 0 ||
            _damageDispatches.Count != 0 || _facilityEffectDispatches.Count != 0 ||
            _hexFireSlots.Count != 0 || !_hexFireRebinding.IsCompleted)
            return "cg-quality.effect-resource-owner";
        if (!UseD3D11Map && _nativeWorldMapDependencyFactory is null) return "cg-quality.renderer-quality-consumer";
        if (_nativeMapInitializing || !_mapInitializations.IsCompleted || HasPendingNativeMapRetirement)
            return "cg-quality.render-resource-lifecycle";
        return null;
    }

    /// <summary>Accept the actual application option. The existing resource
    /// lifecycle produces its next complete image asynchronously. While this
    /// replacement is pending, later edits and Cancel's entry value replace
    /// the requested option; none can be lost to an earlier queued rebuild.</summary>
    public bool ApplyCgQuality(CgQuality quality)
    {
        if (!Enum.IsDefined(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
        if (_nativeMapClosing) return false;
        if (quality == _cgQuality) { _cgQualityApplied = true; return true; }
        if (!_cgQualityRebuilding && CgQualityBorrowerRestriction() is not null) return false;
        _cgQuality = quality; _cgQualityApplied = true;
        if (!_loaded || !_embedded) return true;
        if (!_cgQualityRebuilding)
        {
            _cgQualityRebuilding = true;
            _ = TrackMapInitialization(RebuildCgQualityAsync(_sceneLoadGeneration));
        }
        return true;
    }

    private async Task RebuildCgQualityAsync(uint sceneGeneration)
    {
        try
        {
            while (!_nativeMapClosing && _loaded && sceneGeneration == _sceneLoadGeneration)
            {
                CgQuality requested = _cgQuality;
                if (UseD3D11Map)
                {
                    // Cloud shader pairs and target dimensions are physical
                    // resources. Drain and retire their actual graph before
                    // rebuilding; no old graph consumes newly selected params.
                    // Admission above ensures no simulation presentation owner
                    // or effect ticket is discarded by this existing release.
                    ReleaseD3D11Map();
                    if (GodotObject.IsInstanceValid(_surface)) _surface.Visible = true;
                    await _d3d11Retirement;
                    if (_nativeMapClosing || sceneGeneration != _sceneLoadGeneration) return;
                    InitializeD3D11MapLocations();
                    await InitializeD3D11MapAsync(sceneGeneration, requested);
                    if (_d3d11Map is not { IsClosed: false, LastError: null, LastReceipt: not null }) return;
                }
                else
                {
                    // The independent World route already owns an atomic
                    // replacement: retain its accepted canvas until the new
                    // quality's complete first output, then retire the old graph.
                    await InitializeNativeMapAsync(sceneGeneration, _nativeMapOwner, CaptureNativeMapFrame());
                }
                if (requested == _cgQuality) break;
            }
        }
        catch (Exception error)
        {
            if (!_nativeMapClosing && sceneGeneration == _sceneLoadGeneration)
                RecordNativeMapUnavailable(error);
        }
        finally { _cgQualityRebuilding = false; }
    }
}
