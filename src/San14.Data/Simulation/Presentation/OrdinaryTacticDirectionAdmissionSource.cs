using System.Numerics;
using San14.Core;
using SourceFloat = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>Immutable facts from the actual attached Main camera and caster
/// view. Null is unknown. A known absent caster view supplies the source
/// initialized position (0,0,0,1), never a reconstructed Hex position.
/// CameraPresent describes the actual renderer-camera lookup, independently
/// of the controller's zoom band and the global clip-test bypass.</summary>
public sealed record OrdinaryTacticDirectionHostSnapshot(int? CameraZoomBand = null,
    bool? CameraPresent = null, bool? ClipTestBypassed = null,
    Matrix4x4? WorldToClip = null, Vector4? CasterViewPosition = null);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>369580's request gate, reached after the caller's special
    /// tactic test. It neither runs a Direction, selects a voice, changes
    /// random state nor acknowledges playback. Rejected requests use the
    /// ordinary-effect route; unknown inputs do not become rejection.</summary>
    public AreaAdministrationQuery<bool> QueryUniqueTacticDirectionAdmission(GameState state,
        UnitState actor, int tacticId, OrdinaryTacticDirectionHostSnapshot? host)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.NativeArmyOrigin?.NativeSlotIndex is int physical
            ? QueryUniqueTacticDirectionAdmission(state, physical, tacticId, host)
            : AreaAdministrationQuery<bool>.Unknown(DirectionAdmissionMissing("caster_Army_slot").Message);
    }

    /// <summary>369580 takes the actual Army receiver pointer, including a
    /// receiver reset by an earlier Full call. No Unit image is required.</summary>
    public AreaAdministrationQuery<bool> QueryUniqueTacticDirectionAdmission(GameState state,
        int armyPhysical, int tacticId, OrdinaryTacticDirectionHostSnapshot? host)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            //3EB070 -> 830350 returns primary+8; its member4C is the
            //existing saved CEnvironment member54. No mode/skip surrogate.
            int enabled = Volatile.Read(ref _applicationPresentationOptions) is { } application
                ? application.TacticDirectionsEnabled ? 1 : 0
                : BattleRemakeCurrentSettings.ReadInt32(state, 0x54)
                    ?? throw DirectionAdmissionMissing("current_settings54");
            if (enabled == 0) return Known(false);
            //168C40 passes MainCameraCtrl+50, not a nullable controller.
            var facts = host ?? throw DirectionAdmissionMissing("host_snapshot");
            int band = facts.CameraZoomBand ?? throw DirectionAdmissionMissing("Main_camera_zoom_band");
            if (band >= 4) return Known(false);

            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int physical = armyPhysical;
            if ((uint)physical >= 501) throw DirectionAdmissionMissing("caster_Army_slot_range");
            var army = current.ReadArmyUnitTableReference(physical)
                ?? throw DirectionAdmissionMissing("caster_Army_pointer");
            //22BC70 reads Army12 -> valid Person -> E0. There is no
            //additional Army-validity gate in this helper or369580.
            int commander = current.ReadArmyUnitCommanderId(army)
                ?? throw DirectionAdmissionMissing("caster_Commander12");
            var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                ?? throw DirectionAdmissionMissing("caster_Person_pointer");
            int districtId = 0;
            if (person.PhysicalSlot is int personPhysical &&
                (current.ReadPersonNativeValidity(personPhysical)
                    ?? throw DirectionAdmissionMissing("caster_Person_validity")))
                districtId = current.ReadPersonDistrictSlot(personPhysical)
                    ?? throw DirectionAdmissionMissing("caster_Person_DistrictE0");
            var district = current.ReadDistrictTableReference(districtId < 52 ? districtId : 0)
                ?? throw DirectionAdmissionMissing("caster_District_pointer");
            if (!(current.ReadDistrictNativeValidity(district)
                ?? throw DirectionAdmissionMissing("caster_District_validity"))) return Known(false);
            if (!BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, DirectionAdmissionMissing))
                return Known(false);

            //14CB20 requires both renderer-camera ownership and its actual
            //camera object. A prepared numerical matrix alone is not proof.
            if (!(facts.CameraPresent ?? throw DirectionAdmissionMissing("renderer_camera_presence")))
                return Known(false);
            //011470's independent debug/render clip bypass precedes the
            //transform. Its absence cannot be inferred from an unknown flag.
            if (facts.ClipTestBypassed ?? throw DirectionAdmissionMissing("clip_test_bypass")) return Known(true);
            Matrix4x4 matrix = facts.WorldToClip ?? throw DirectionAdmissionMissing("renderer_camera_WorldToClip");
            Vector4 position = facts.CasterViewPosition ?? throw DirectionAdmissionMissing("caster_current_view_position");
            return Known(DirectionClipContains(matrix, position));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<bool>.Unknown(error.Message); }

        static AreaAdministrationQuery<bool> Known(bool value) => AreaAdministrationQuery<bool>.Known(value);
    }

    //011470 ->016900, with the actual float32 transform order: X sums
    //Y then X; Y/Z/W sum X then Y. Preserve edge/unordered rejection
    //without adding a W>0 test or a different projection helper.
    private static bool DirectionClipContains(Matrix4x4 matrix, Vector4 position)
    {
        float inverse = SourceFloat.Divide(1f, Column(matrix.M14, matrix.M24, matrix.M34, matrix.M44, false));
        float x = SourceFloat.Multiply(Column(matrix.M11, matrix.M21, matrix.M31, matrix.M41, true), inverse);
        float y = SourceFloat.Multiply(Column(matrix.M12, matrix.M22, matrix.M32, matrix.M42, false), inverse);
        float z = SourceFloat.Multiply(Column(matrix.M13, matrix.M23, matrix.M33, matrix.M43, false), inverse);
        //Actual COMISS/JBE rejects unordered coordinates as well as edges.
        return x > -1f && x < 1f && y > -1f && y < 1f && z > 0f && z < 1f;

        float Column(float a, float b, float c, float d, bool yFirst)
        {
            float x = SourceFloat.Multiply(position.X, a), y = SourceFloat.Multiply(position.Y, b);
            return SourceFloat.Add(SourceFloat.Add(yFirst ? SourceFloat.Add(y, x) : SourceFloat.Add(x, y), SourceFloat.Multiply(position.Z, c)),
                SourceFloat.Multiply(position.W, d));
        }
    }

    private static InvalidDataException DirectionAdmissionMissing(string owner)
        => new("tactic.direction-admission." + owner);
}
