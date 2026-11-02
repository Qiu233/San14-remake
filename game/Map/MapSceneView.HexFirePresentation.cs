using Godot;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;

public partial class MapSceneView
{
    private sealed class HexFireSlot(int hex)
    {
        internal readonly int HexId = hex;
        internal object? Ticket;
    }
    private readonly Dictionary<int, HexFireSlot> _hexFireSlots = [];
    private HexFirePresentationPublication? _hexFirePresentation;
    private CancellationTokenSource? _hexFireLifetime;
    private Task _hexFireRebinding = Task.CompletedTask;

    /// <summary>Called for the same immutable business publication carried by
    /// the accepted native map frame. Unknown never erases an existing fire.</summary>
    internal void BindHexFirePresentation(HexFirePresentationPublication? publication)
    {
        if (publication is null || ReferenceEquals(_hexFirePresentation, publication)) return;
        if (_hexFireLifetime is null)
        {
            EnsureCaptureFeedbackLifetime(); _hexFireLifetime = new();
            TreeExiting += RetireHexFirePresentation;
        }
        _hexFirePresentation = publication;
        var token = _hexFireLifetime.Token;
        var previous = _hexFireRebinding;
        _hexFireRebinding = Rebind();

        async Task Rebind()
        {
            try
            {
                await previous;
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(_hexFirePresentation, publication)) return;
                if (_captureEffects is null || !D3D11MapInteractive || _sourceMainCameraTerrain is null)
                    throw new InvalidDataException("fire.hex.native_scene_or_terrain");
                var present = publication.Fires.Select(row => row.HexId).ToHashSet();
                foreach (int removed in _hexFireSlots.Keys.Where(hex => !present.Contains(hex)).ToArray())
                {
                    if (!_hexFireSlots.Remove(removed, out var slot) || slot.Ticket is not { } ticket) continue;
                    await StopHexFireTicket(ticket, token);
                }
                foreach (var row in publication.Fires)
                {
                    token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(_hexFirePresentation, publication)) return;
                    if (!_hexFireSlots.TryGetValue(row.HexId, out var slot))
                        _hexFireSlots.Add(row.HexId, slot = new(row.HexId));
                    if (slot.Ticket is { } prior && _captureEffects.IsLive(prior)) continue;
                    if (slot.Ticket is { } finished) _captureEffects.Forget(finished);
                    var point = FacilityEffectGeometry.HexPosition(row.HexId, _sourceMainCameraTerrain);
                    var ticket = new object(); slot.Ticket = ticket;
                    var pending = _captureEffects.Enqueue(ticket, 15,
                        NMatrix.CreateScale(.6f) * NMatrix.CreateTranslation(point), null, token, slot);
                    ScheduleD3D11MapFrame();
                    var result = await pending;
                    if (!result.Completed) throw new InvalidDataException(result.UnresolvedInput ?? "fire.hex.effect_admission");
                }
                RemoveMeta("hex_fire_presentation_missing");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_hexFirePresentation, publication))
                { SetMeta("hex_fire_presentation_missing", error.Message); GD.PushWarning("Hex fire presentation: " + error.Message); }
            }
        }
    }

    private async Task StopHexFireTicket(object ticket, CancellationToken token)
    {
        if (_captureEffects is not { } effects) return;
        //18C430 unlinks the Hex and37E7C0 stops normal emission. The
        //shared manager retains draining particles until natural retirement.
        var stopped = effects.RequestStop(ticket); ScheduleD3D11MapFrame();
        await stopped.WaitAsync(token); effects.Forget(ticket);
    }

    internal void RetireHexFirePresentation()
    {
        if (_hexFireLifetime is { } lifetime)
        {
            TreeExiting -= RetireHexFirePresentation;
            _hexFireLifetime = null;
            lifetime.Cancel(); lifetime.Dispose();
        }
        foreach (var slot in _hexFireSlots.Values)
            if (slot.Ticket is { } ticket) { _captureEffects?.Retire(ticket); _captureEffects?.Forget(ticket); }
        _hexFireSlots.Clear(); _hexFirePresentation = null; _hexFireRebinding = Task.CompletedTask;
    }
}
