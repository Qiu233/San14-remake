using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Once-imported default raw ActionID0 player/controller fields.
/// The caller supplies the actual category, group route, duration and rate.</summary>
public readonly record struct DefaultPlayerAnimationProfile(
    int Category, float Duration8, float DefaultLoop28, float ControllerRate28, bool Grouped);

/// <summary>Actual node24 and H24/H28 scalar owner. A new player starts at action−1/frame0.</summary>
public sealed class DefaultPlayerAnimationClock(float controllerRate28)
{
    public int SelectedRawAction24 { get; set; } = -1;
    public float ControllerFrame24 { get; set; }
    public float ControllerRate28 { get; } = controllerRate28;
    public float ReturnedPhase { get; internal set; }
}

public readonly record struct DefaultPlayerAnimationGroupState(
    bool Played3CF0, bool Wrapped3D0C, float NextFrame3D28, float ReturnedPhase);

/// <summary>One source manager's seven slots. Reset once at a reached 1CA6E0 update,
/// before walking units; it is independent of camera draws and individual actors.</summary>
public sealed class DefaultPlayerAnimationGroups
{
    internal readonly DefaultPlayerAnimationGroupState[] Slots = new DefaultPlayerAnimationGroupState[7];
    public DefaultPlayerAnimationGroupState GetSlot(int index) => Slots[index];

    public void ResetReachedManagerUpdate()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            Slots[i] = slot with { Played3CF0 = false, Wrapped3D0C = false,
                NextFrame3D28 = slot.Played3CF0 ? slot.NextFrame3D28 : 0 };
        }
    }
}

/// <summary>Reached/Eligible are actual caller gates, including node kind1/ready14
/// and controller/model presence. This leaf does not infer them from UI or visibility.</summary>
public readonly record struct DefaultPlayerAnimationCallback(
    bool Reached, bool Eligible, int RequestedRawAction = -1, float RequestedFrame = -1, uint FpsMode = 0);

public readonly record struct DefaultPlayerAnimationStep(
    bool Succeeded, bool Sampled, bool Cached, bool Wrapped,
    float RequestedSeconds, float ReturnedPhase, float NextFrame);

/// <summary>CE D953F0/D95330 returned phase and default raw0 1CEB90 scalar update.
/// No resource loading, animation sampling, skeletons or elapsed-time scheduler.</summary>
public static class DefaultPlayerAnimation
{
    public static float ReturnedPhase(DefaultPlayerAnimationProfile profile, float requestedSeconds)
    {
        uint loopCount = 0;
        return ReturnedPhase(profile, requestedSeconds, -1, ref loopCount);
    }

    /// <summary>A strictly negative override selects player28. Prefix/stop branches
    /// leave loopCount unchanged, as the original leaf does.</summary>
    public static float ReturnedPhase(DefaultPlayerAnimationProfile profile,
        float requestedSeconds, float loopOverride, ref uint loopCount)
    {
        float selected = loopOverride < 0 ? profile.DefaultLoop28 : loopOverride;
        float start = selected > 0 ? selected : 0;
        if (requestedSeconds < profile.Duration8) return requestedSeconds;
        if (start >= profile.Duration8)
            return Subtract(profile.Duration8, BitConverter.UInt32BitsToSingle(0x37480000));
        float delta = Subtract(requestedSeconds, start);
        delta = delta > 0 ? delta : 0;
        float period = Subtract(profile.Duration8, start);
        float quotient = MathF.Floor(Divide(delta, period));
        loopCount = unchecked((uint)Sse.X64.ConvertToInt64WithTruncation(Vector128.CreateScalar(quotient)));
        float remainder = Subtract(delta, Product((float)(ulong)loopCount, period));
        remainder = remainder > 0 ? remainder : 0;
        return Add(remainder, start);
    }

    public static int DefaultGroupSlot(int category) => category switch { 0 => 4, 1 => 0, _ => -1 };

    /// <summary>Consume one actual eligible callback. actualPlayerResponse may return
    /// null for an actual failed player request; otherwise the closed default player is used.
    /// A cached group shares its sampled phase without advancing this actor's H24.</summary>
    public static DefaultPlayerAnimationStep Consume(DefaultPlayerAnimationProfile profile,
        DefaultPlayerAnimationClock clock, DefaultPlayerAnimationGroups groups,
        DefaultPlayerAnimationCallback callback, ReadOnlySpan<uint> actualFpsTable,
        Func<float, float?>? actualPlayerResponse = null)
    {
        var result = new DefaultPlayerAnimationStep(false, false, false, false, 0,
            clock.ReturnedPhase, clock.ControllerFrame24);
        if (!callback.Reached || !callback.Eligible) return result;
        int raw = callback.RequestedRawAction < 0 ? 0 : callback.RequestedRawAction;
        if (raw != 0)
            throw new NotSupportedException("This animation leaf requires the separately imported source player for a nondefault raw ActionID.");
        float requestFrame = callback.RequestedFrame;
        if (raw != clock.SelectedRawAction24)
        {
            requestFrame = 0;
            clock.ControllerFrame24 = 0;
            result = result with { NextFrame = 0 };
        }
        clock.SelectedRawAction24 = raw;
        int slotIndex = -1;
        if (profile.Grouped)
        {
            slotIndex = DefaultGroupSlot(profile.Category);
            if (slotIndex < 0) return result;
            var slot = groups.Slots[slotIndex];
            requestFrame = slot.NextFrame3D28;
            if (slot.Played3CF0)
            {
                clock.ReturnedPhase = slot.ReturnedPhase;
                return result with { Succeeded = true, Cached = true, Wrapped = slot.Wrapped3D0C,
                    ReturnedPhase = slot.ReturnedPhase, NextFrame = slot.NextFrame3D28 };
            }
        }
        if (requestFrame < 0) requestFrame = clock.ControllerFrame24;
        float requestedSeconds = Divide(requestFrame, clock.ControllerRate28);
        result = result with { RequestedSeconds = requestedSeconds };
        float? returned = actualPlayerResponse is null ? ReturnedPhase(profile, requestedSeconds) : actualPlayerResponse(requestedSeconds);
        if (returned is not float phase) return result;
        float rounded = MathF.Floor(Add(Product(phase, clock.ControllerRate28), .5f));
        bool wrapped = clock.ControllerFrame24 > rounded;
        float next = wrapped ? rounded : Add(rounded, Divide(clock.ControllerRate28, actualFpsTable[(int)callback.FpsMode]));
        clock.ControllerFrame24 = next;
        clock.ReturnedPhase = phase;
        if (slotIndex >= 0) groups.Slots[slotIndex] = new(true, wrapped, next, phase);
        return new(true, true, false, wrapped, requestedSeconds, phase, next);
    }

    // Retain the separate float32 SSE operation order, including no FMA contraction.
    [MethodImpl(MethodImplOptions.NoInlining)] static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Divide(float a, float b) => a / b;
}
