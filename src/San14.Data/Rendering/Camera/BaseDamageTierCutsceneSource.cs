using System.Numerics;
using San14.Core;

namespace San14.Data;

/// <summary>The ordinary City damage-tier presentation's actual movie and
/// camera operands. The model has already committed its tier. These queries
/// neither modify it nor acknowledge a pending presentation.</summary>
public static class BaseDamageTierCutsceneSource
{
    private const double UnchangedYaw = 62.83185308;

    public static int ReadMovieId(BaseDamageTierCutsceneRequest request)
    {
        RequireRequest(request);
        // 2E3320 -> 84F6A0 -> CCmnMovieCutscene -> 8AD280 passes this
        // event unchanged as the shared application movie ID.
        return request.DamageTier + 0x10;
    }

    public static bool? IsPlaybackEnabled(BaseDamageTierCutsceneRequest request,
        bool? applicationMoviePlaybackEnabled) =>
        MoviePlaybackSource.IsPlaybackEnabled(ReadMovieId(request), applicationMoviePlaybackEnabled);

    /// <summary>Borrow the existing normal-game movie route, independent of
    /// collection locks/subtitles. IDs18/19 select the actual common WMV files;
    /// resource overrides and file admission remain in the shared repository.</summary>
    public static CollectionMoviePlayback ReadPlayback(GameResourceRepository repository,
        BaseDamageTierCutsceneRequest request, string locale) =>
        MoviePlaybackSource.ReadPlayback(repository, ReadMovieId(request), locale);

    /// <summary>452B10's cutscene focus. Height is the already resolved1889A0
    /// owner (positive cache or actual ground with445 floor, or known0 when
    /// that scene owner is absent). Null is unknown, never a guessed zero.
    /// The original48400 operand is known no-focus; it is not a map cell.</summary>
    public static MainMapCameraControllerRequest? CreateFocusRequest(
        BaseDamageTierCutsceneRequest request, float? ownedHeight1889A0)
    {
        RequireRequest(request);
        if (request.HexId == 48400) return null;
        if (ownedHeight1889A0 is not float height || !float.IsFinite(height))
            throw new InvalidDataException("base-tier.cutscene.height1889A0_unavailable");
        var point = CeHexWorldCoordinates.Project(request.HexId);
        return new(2000, new Vector4((float)point.X, height, (float)point.Z, 1),
            100.0, UnchangedYaw, -1, 1);
    }

    /// <summary>861110 restores the retained focus/distance through the actual
    /// shared controller. Yaw/pitch retain the original unchanged sentinels;
    /// no newer World pose or request duration determines movie completion.</summary>
    public static MainMapCameraControllerRequest CreateRestoreRequest(MainMapCameraControllerState retained)
    {
        ArgumentNullException.ThrowIfNull(retained);
        var focus = new Vector4((float)retained.FocusX, (float)retained.FocusY,
            (float)retained.FocusZ, (float)retained.FocusW);
        if (!float.IsFinite(focus.X) || !float.IsFinite(focus.Y) ||
            !float.IsFinite(focus.Z) || !float.IsFinite(focus.W) || !double.IsFinite(retained.Distance90))
            throw new InvalidDataException("base-tier.cutscene.retained_camera_unavailable");
        return new(2000, focus, retained.Distance90, UnchangedYaw, -1, 1);
    }

    private static void RequireRequest(BaseDamageTierCutsceneRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BaseLocationId is < 1 or > 51 || request.DamageTier is not (2 or 3) ||
            request.HexId is < 0 or > 48400)
            throw new InvalidDataException("base-tier.cutscene.source_request");
    }
}
