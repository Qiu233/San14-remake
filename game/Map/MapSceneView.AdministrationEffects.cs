using Godot;
using San14.Data;
using NMatrix = System.Numerics.Matrix4x4;
using NVector = System.Numerics.Vector4;

public partial class MapSceneView
{
    private sealed record AdministrationEffectPair(int Area, int Kind, NMatrix Transform)
    {
        internal readonly object First = new(), Second = new();
    }

    private sealed class AdministrationEffects(D3D11CaptureEffects effects, AreaAdministrationDraft draft,
        IReadOnlyList<AdministrationEffectPair> pairs)
    {
        internal readonly D3D11CaptureEffects Effects = effects;
        internal readonly AreaAdministrationDraft Draft = draft;
        internal readonly IReadOnlyList<AdministrationEffectPair> Pairs = pairs;
        internal readonly object Owner = new();
        internal int? HoverArea;
    }

    private AdministrationEffects? _administrationEffects;

    private bool PrepareAdministrationEffects(AreaAdministrationDraft draft,
        Action<string> unavailable, out AdministrationEffects? prepared)
    {
        prepared = null;
        var query = draft.QueryMapEffectSources();
        if (!query.Available)
        { unavailable(query.UnresolvedInput ?? "area-map.effect_sources"); return false; }
        if (_captureEffects is not { } effects || _d3d11PresentedInput is not { } input ||
            !ReferenceEquals(input.WorldState, _logicalWorld))
        { unavailable("area-map.effect_presented_models"); return false; }

        var pairs = new List<AdministrationEffectPair>();
        float scale = 1;
        foreach (var row in query.Value!.Rows)
        {
            // The original held loop carries this scale into subsequent rows.
            if (row.UsesBaseView) scale = 1.28f;
            NVector position = new(0, 0, 0, 1);
            if (!row.UsesBaseView)
            {
                var owner = input.AreaCapitalWorld?.Source.Source.Owners
                    .FirstOrDefault(owner => owner.NativeAreaSlot == row.AreaPhysicalSlot);
                if (owner?.Primary.SourceWorldState is not { } pose)
                { unavailable($"area-map.effect_area_view[{row.AreaPhysicalSlot}]"); return false; }
                // The accepted model owns an unparented affine world pose.
                position = new(pose.Position18, 1);
            }
            else
            {
                if (!ReadBase(row.City, false) || !ReadBase(row.Gate, true)) return false;
                // Both known-invalid receivers retain the source's initialized
                // position. This is distinct from a missing published model.
            }
            position.Y += 150;
            pairs.Add(new(row.AreaPhysicalSlot, row.UsesBaseView ? 70 : 68,
                DomesticEffectGeometry.CreateTransform(position, scale)));

            bool ReadBase(AreaAdministrationMapEffectBase? receiver, bool gate)
            {
                if (receiver is null)
                { unavailable("area-map.effect_base_receiver"); return false; }
                if (!receiver.NativeValid) return true;
                var owner = input.CityGateWorld?.Source.Primary.Owners.FirstOrDefault(owner =>
                    owner.NativeSlot == receiver.NativeSlot &&
                    (gate ? owner.Gate is not null : owner.CitySelection is not null));
                if (owner?.Instance.SourceWorldState is not { } pose)
                { unavailable($"area-map.effect_{(gate ? "gate" : "city")}_view[{receiver.NativeSlot}]"); return false; }
                position = new(pose.Position18, 1);
                return true;
            }
        }
        prepared = new(effects, draft, pairs.AsReadOnly());
        return true;
    }

    private void BeginAdministrationEffects(AdministrationEffects prepared, Action<string> unavailable)
    {
        _administrationEffects = prepared;
        var pending = new List<Task<CaptureEffectAdmission>>(prepared.Pairs.Count * 2);
        foreach (var pair in prepared.Pairs)
        {
            pending.Add(prepared.Effects.Enqueue(pair.First, pair.Kind, pair.Transform,
                0xFF19FFFF, CancellationToken.None, prepared.Owner));
            pending.Add(prepared.Effects.Enqueue(pair.Second, pair.Kind + 1, pair.Transform,
                0xFF19FFFF, CancellationToken.None, prepared.Owner));
        }
        bool failureReported = false;
        // Held offscreen reservations can legitimately remain pending. Observe
        // each reached result so that one such reservation cannot hide another
        // ticket's actual resource/admission failure.
        foreach (var task in pending) _ = Observe(task);
        async Task Observe(Task<CaptureEffectAdmission> task)
        {
            try
            {
                var result = await task;
                if (!result.Completed) Report(result.UnresolvedInput ?? "area-map.effect_admission");
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Report("area-map.effect_admission: " + error.Message); }
        }
        void Report(string reason)
        {
            if (failureReported || !ReferenceEquals(_administrationEffects, prepared)) return;
            failureReported = true;
            unavailable(reason);
        }
    }

    private void UpdateAdministrationEffectHover(int hex)
    {
        if (_administrationEffects is not { } workspace) return;
        // The tint uses the raw Hex12 word; effect owners use its resolved
        // physical Area receiver. Held membership is not a hover filter.
        var target = workspace.Draft.QueryMapTarget(hex);
        if (!target.Available) return;
        int? area = target.Value!.AreaId;
        if (workspace.HoverArea == area) return;
        foreach (var pair in workspace.Pairs)
        {
            if (pair.Area != workspace.HoverArea && pair.Area != area) continue;
            uint colour = pair.Area == area ? 0xFF325AFFu : 0xFF19FFFFu;
            workspace.Effects.SetColour(pair.First, workspace.Owner, colour);
            workspace.Effects.SetColour(pair.Second, workspace.Owner, colour);
        }
        workspace.HoverArea = area;
    }

    private void EndAdministrationEffects()
    {
        if (_administrationEffects is not { } workspace) return;
        _administrationEffects = null;
        var stopping = new List<Task>(workspace.Pairs.Count * 2);
        foreach (var pair in workspace.Pairs)
        {
            stopping.Add(workspace.Effects.RequestStop(pair.First));
            stopping.Add(workspace.Effects.RequestStop(pair.Second));
        }
        _ = ReleaseAfterTail();
        async Task ReleaseAfterTail()
        {
            try
            {
                await Task.WhenAll(stopping);
                foreach (var pair in workspace.Pairs)
                {
                    await workspace.Effects.WaitForRetirement(pair.First);
                    workspace.Effects.Forget(pair.First);
                    await workspace.Effects.WaitForRetirement(pair.Second);
                    workspace.Effects.Forget(pair.Second);
                }
            }
            catch (Exception error) { GD.PushWarning("Domestic effect retirement: " + error.Message); }
        }
    }
}
