namespace San14.Data;

/// <summary>Actual current masks of the owned Scene/View preparation. The
/// successful mask is D15225's accumulated prepared slots, not only the
/// intermediate Reference-present OR at D13F49.</summary>
public sealed record OriginalDeferredEarlyShadowCurrentViewMaskFields(long FrameNumber,
    string ViewIdentity, uint RegisteredViewMask3330, uint SuccessfulViewMask3340,
    uint CallerFirstMask, uint CallerSecondMask, uint CallerShadowMask, string SourceEvidence);

/// <summary>Finite owned equivalent of the Scene mask writer. Construction
/// writes D01765's actual FFFF0001, reset clears3330..3340, registration and
/// successful preparation publish their real bits. This is a producer for the
/// remake owner, not an observation of a running original process.</summary>
public sealed class OriginalDeferredEarlyShadowOwnedViewMaskSource
{
    private readonly LoadedOriginalDefaultView0Source source;
    private long frame;
    private string view = "";
    private uint registered, successful;
    private uint inheritedFirst, inheritedSecond, inheritedShadow;
    private uint collectionMask3344 = 0xFFFF0001;
    public OriginalDeferredEarlyShadowOwnedViewMaskSource(LoadedOriginalDefaultView0Source actualSource)
        => source = actualSource ?? throw new ArgumentNullException(nameof(actualSource));

    public void BeginCurrent(long actualFrame, string actualViewIdentity)
    {
        if (actualFrame <= 0) throw new ArgumentOutOfRangeException(nameof(actualFrame));
        ArgumentException.ThrowIfNullOrWhiteSpace(actualViewIdentity);
        frame = actualFrame; view = actualViewIdentity;
        registered = successful = inheritedFirst = inheritedSecond = inheritedShadow = 0;
    }

    /// <summary>Install the actual owned default View0 pair and publish its
    /// successful preparation from the same accepted Main V468/V418 result.
    /// Other View slots need their own actual registration/preparation.</summary>
    public void PublishCurrentDefaultView0(OriginalDeferredEarlyShadowCurrentMainFields actualMain)
    {
        ArgumentNullException.ThrowIfNull(actualMain);
        OriginalDeferredEarlyShadowCurrentMainFields.RequireActualMain(actualMain.ActualMainInputs, actualMain.PreparedMain);
        if (frame != actualMain.PreparedMain.FrameNumber || source.DeclaredViewSlot != 0 ||
            (source.DeclaredViewMask & 1) == 0 || actualMain.PreparedMain.Source.Camera.ObjectId != source.MainCameraObjectId)
            throw new InvalidDataException("Default View0 mask publication needs this current accepted Main and loaded task9 View pair.");
        // D21680..D21700 occupies slot0 with its retained ViewParam/Reference.
        registered |= 1;
        // D13F29 success -> D13F36 accumulator -> D15225.
        successful |= 1;
    }

    /// <summary>Actual external Scene mask setters, if the owned caller has
    /// reached them. Reset-local3334/38/3C cannot be replaced with all ones.</summary>
    public void WriteCurrentInheritedMasks(uint actual3334, uint actual3338, uint actual333C)
    {
        RequireBegun(); (inheritedFirst, inheritedSecond, inheritedShadow) = (actual3334, actual3338, actual333C);
    }
    public void WriteCurrentCollectionMask3344(uint actualMask) => collectionMask3344 = actualMask;

    /// <summary>D4BD30's actual Common22A8 collection -> D68E40 callback.
    /// All three input masks are the retained3344; each family adds its own
    /// current3334/38/3C. D4B9E0's CB2F00/DC5420 preparation instead clears
    /// shadow and must not substitute for this reached collection caller.</summary>
    public OriginalDeferredEarlyShadowCurrentViewMaskFields CaptureCurrentWorldCollectionCaller()
    {
        RequireBegun();
        return new(frame, view, registered, successful,
            collectionMask3344 | inheritedFirst, collectionMask3344 | inheritedSecond,
            collectionMask3344 | inheritedShadow, source.SourceIdentity +
            ";TC:D01765:collection3344=FFFF0001;D50B9C/D50BA3/D50BAA:reset;" +
            "D21700:registered3330;D13F36/D15225:successful3340;" +
            "D4BEB0/D4BEB8/D4BEC3/D4BECB:D68E40-actual-world-collection-caller;" +
            "D68E78/D68E86/D68E90/D68E9B/D68EAB/D68EB5:family-masks;not-DC5B38-shadow0");
    }

    private void RequireBegun()
    {
        if (frame <= 0) throw new InvalidOperationException("The owned View mask writer has no current preparation.");
    }
}
