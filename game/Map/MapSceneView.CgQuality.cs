using Godot;
using San14.Data;
using San14.Client.Gameplay;

public partial class MapSceneView
{
    private CgQuality _cgQuality = CgQuality.Normal;
    private bool _cgQualityApplied;
    private bool _cgQualityRebuilding;
    private Task _cgQualityPreparation = Task.CompletedTask;
    private CampaignPeriodPlayback? _cgQualityPeriodMenu;

    // Main lends this actual checkpoint through Config. Merely having a
    // period map does not authorize an asynchronous replacement of its owner.
    internal bool BeginPeriodCgQualityInspection(CampaignPeriodPlayback owner)
    {
        if (!UseD3D11Map || !_periodMapActive || !ReferenceEquals(_periodMapOwner, owner) ||
            !owner.MenuPresentationOpen || owner.Candidate is null) return false;
        _cgQualityPeriodMenu = owner;
        return true;
    }

    internal void EndPeriodCgQualityInspection(CampaignPeriodPlayback owner)
    {
        if (ReferenceEquals(_cgQualityPeriodMenu, owner)) _cgQualityPeriodMenu = null;
    }

    private bool IsPeriodCgQualityInspectionCurrent(CampaignPeriodPlayback owner) =>
        ReferenceEquals(_cgQualityPeriodMenu, owner) && ReferenceEquals(_periodMapOwner, owner) &&
        _periodMapActive && owner.MenuPresentationOpen && owner.Candidate is not null;

    internal async Task WaitForCgQualityPreparationAsync()
    {
        await _cgQualityPreparation;
        if (_nativeMapClosing) throw new OperationCanceledException("The map is retiring.");
        if (_loaded && _embedded && !(UseD3D11Map ? D3D11MapInteractive : HasNativeMapOutput))
            throw new InvalidOperationException("The requested CG map has no accepted output.");
    }
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
        if ((_periodMapOwner is not null || _periodMapActive) &&
            (_cgQualityPeriodMenu is not {} period || !IsPeriodCgQualityInspectionCurrent(period)))
            return "cg-quality.period-presentation-owner";
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
            _cgQualityPreparation = TrackMapInitialization(RebuildCgQualityAsync(_sceneLoadGeneration));
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
                    // A held period keeps its logical Army/connection owners;
                    // only physical publications and the graph are replaced.
                    // No borrowed World is sent to the preparation workers.
                    var period = _periodMapActive ? _cgQualityPeriodMenu : null;
                    if (period is not null && !IsPeriodCgQualityInspectionCurrent(period)) return;
                    ReleaseD3D11Map(preservePeriodPresentation: period is not null);
                    if (GodotObject.IsInstanceValid(_surface)) _surface.Visible = true;
                    await _d3d11Retirement;
                    if (_nativeMapClosing || sceneGeneration != _sceneLoadGeneration) return;
                    InitializeD3D11MapLocations();
                    await InitializeD3D11MapAsync(sceneGeneration, requested, period);
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
