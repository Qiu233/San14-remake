using San14.Data.Formats;

namespace San14.Data;

/// <summary>The source positive-speed repeat/once cursor, shared by the
/// texture-pattern and standing-pose consumers. It advances once per actual
/// host update; the inclusive repeat end is not a modulo-duration clock.</summary>
internal sealed class LcskPositivePlayback(LcskAnimationClip clip, bool repeat, float speed)
{
    internal LcskAnimationClip Clip { get; } = clip;
    internal float Frame { get; private set; } = clip.StartFrame;
    internal bool IsPlaying { get; private set; } = true;

    internal bool Advance(float seconds)
    {
        if (!IsPlaying) return false;
        float previous = Frame;
        float next = previous + seconds / LcskAnimationClip.SecondsPerFrame * speed;
        const float epsilon = 0.0011920929f;
        bool terminalWrite = false;
        if (repeat)
        {
            int range = (int)(Clip.EndFrame - Clip.StartFrame);
            if (range < 1) next = Clip.StartFrame;
            else if (range < 2) next = Clip.EndFrame;
            else
            {
                float boundary = Clip.EndFrame + 1;
                if (next >= boundary - epsilon)
                    next = (int)previous == (int)Clip.EndFrame && (int)next == (int)boundary
                        ? Clip.StartFrame : Clip.StartFrame + (next - boundary);
            }
        }
        else if (next < Clip.StartFrame || next > Clip.EndFrame)
        {
            next = Clip.EndFrame;
            // The first terminal crossing publishes the real endpoint. The
            // following update retires this request, as the source does.
            terminalWrite = previous < Clip.EndFrame;
            if (!terminalWrite) IsPlaying = false;
        }
        Frame = next;
        return previous < next - epsilon || next + epsilon < previous || terminalWrite;
    }
}
