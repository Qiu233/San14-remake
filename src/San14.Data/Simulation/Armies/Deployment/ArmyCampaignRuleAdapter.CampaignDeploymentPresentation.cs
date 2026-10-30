using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CampaignPageSource
    {
        private readonly Dictionary<(CampaignDeploymentMapStage Stage, int? Province, int? District), CampaignMapPresentation>
            mapPresentations = [];
        private readonly Dictionary<(int Province, int District, int Location), CampaignDeploymentHint> footholdHints = [];

        public CampaignMapPresentation MapPresentation(CampaignDeploymentMapStage stage, int? province, int? district)
        {
            Require();
            var key = (stage, province, district);
            if (mapPresentations.TryGetValue(key, out var saved)) return saved;
            IReadOnlyList<CampaignProvinceChoice>? provinces = null;
            IReadOnlyList<CampaignDistrictChoice>? districtRows = null;
            IReadOnlyList<CampaignFootholdChoice>? sourceRows = null;
            switch (stage)
            {
                case CampaignDeploymentMapStage.TargetProvince: provinces = Targets(); break;
                case CampaignDeploymentMapStage.District:
                    districtRows = Districts(province ?? throw CampaignPageMissing("presentation_target_province")); break;
                case CampaignDeploymentMapStage.Foothold:
                    sourceRows = Footholds(province ?? throw CampaignPageMissing("presentation_target_province"),
                        district ?? throw CampaignPageMissing("presentation_source_District")); break;
                default: throw CampaignPageMissing("presentation_stage");
            }

            var rows = ImmutableArray.CreateBuilder<CampaignMapMarkerPresentation>();
            foreach (var receiver in bases)
            {
                if (!Valid(receiver)) continue;
                //71D230 publishes the player's own Force population through
                //the District button owner.1B5200's other two stages instead
                //keep existing Footholds and change enabled membership.
                if (stage == CampaignDeploymentMapStage.District && !IsOwnBase(receiver)) continue;
                int physical = K(receiver.PhysicalSlot, "presentation_Base_receiver");
                int location = Location(receiver);
                int hex = C.ReadBaseCurrentHexIndex(receiver);
                if ((uint)hex >= 48400) throw CampaignPageMissing("presentation_Base_Hex_range");
                bool enabled; int? number = null; byte? colour = null;
                CampaignDistrictLeaderFace? leaderFace = null;
                if (provinces is not null)
                {
                    var choice = provinces.FirstOrDefault(row => row.Id == Province(receiver));
                    if (choice?.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
                    enabled = choice?.Available == true;
                }
                else if (districtRows is not null)
                {
                    int at = receiver.Kind == BaseKind.Gate ? CurrentGateMapQueries.ReadDistrictSlot(receiver, C)
                        : K(C.ReadDistrictPhysicalSlot(C.ReadCityDistrictReference(receiver)
                            ?? throw CampaignPageMissing("presentation_Base_virtual80")), "presentation_District_receiver");
                    number = K(C.ReadDistrictNumber(at), "presentation_District_Number11");
                    leaderFace = ReadDistrictLeaderFace(receiver, at);
                    var choice = districtRows.FirstOrDefault(row => row.Id == at);
                    if (choice?.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
                    enabled = choice?.Available == true;
                    colour = K(C.ReadForceReportColor(new(ForceId)), "presentation_player_Force49");
                }
                else
                {
                    var choice = sourceRows!.FirstOrDefault(row => row.Id == location);
                    if (choice?.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
                    enabled = choice?.Available == true;
                }
                rows.Add(new(receiver.Kind, physical, location, hex, enabled, number, colour) { DistrictLeaderFace = leaderFace });
            }
            return mapPresentations[key] = new(stage, rows.ToImmutable());
        }

        private CampaignDistrictLeaderFace? ReadDistrictLeaderFace(BaseReference receiver, int district)
        {
            //198970: the District leader's home Base -> parent City must be
            //this exact Foothold receiver. It is not the City's governor.
            if (!K(C.ReadDistrictNativeValidity(district), "presentation_face_District_validity")) return null;
            int raw = K(C.ReadDistrictLeader(district), "presentation_face_District12");
            var pointer = raw <= 6000 ? C.ReadPersonLookupReference(raw) : C.ReadTransferPersonTableReference(0);
            int person = K(pointer?.PhysicalSlot, "presentation_face_Person_pointer");
            int homeId = K(C.ReadPersonFirstLocation(person), "presentation_face_PersonE2");
            var home = BaseLookup.Find(homeId, C);
            if (!Valid(home)) return null;
            var city = CurrentGateMapQueries.FindCity(home, C);
            if (!Valid(city) || city != receiver) return null;
            //195FF0/195F80 apply this final validity gate to198970's result.
            if (!K(C.ReadPersonNativeValidity(person), "presentation_face_Person_validity")) return null;
            int id = K(C.ReadPersonIdentity(person), "presentation_face_Person_identity");
            ushort appearance = K(C.ReadPersonPortraitAppearance(person), "presentation_face_PersonA6");
            if (!portraits.TryGetValue(id, out var route))
                portraits[id] = route = ReadOfficerPortraitAppearance(state, C, owner._definitions, id,
                    appearance, ArmyMapUiSources.Create());
            if (route.Kind == OfficerPortraitRouteKind.Unresolved)
                throw new InvalidDataException(route.UnresolvedInput ?? "campaign.deployment.presentation_face_route");
            return new(id, appearance, route);
        }

        public CampaignDeploymentHint HoverHint(CampaignDeploymentMapStage stage, int? province,
            int? district, int? identity)
        {
            try
            {
                Require();
                if (identity is not int id) return new(null, null);
                switch (stage)
                {
                    case CampaignDeploymentMapStage.TargetProvince:
                    {
                        var choice = Targets().FirstOrDefault(row => row.Id == id);
                        return new(choice?.HintMessageId, choice?.UnresolvedOwner);
                    }
                    case CampaignDeploymentMapStage.District:
                    {
                        var choice = Districts(province ?? throw CampaignPageMissing("hint_target_province"))
                            .FirstOrDefault(row => row.Id == id);
                        return new(choice?.HintMessageId, choice?.UnresolvedOwner);
                    }
                    case CampaignDeploymentMapStage.Foothold:
                    {
                        int target = province ?? throw CampaignPageMissing("hint_target_province");
                        int from = district ?? throw CampaignPageMissing("hint_source_District");
                        var key = (target, from, id);
                        if (footholdHints.TryGetValue(key, out var cached)) return cached;
                        //6E2DF0: invalid Base has no override. A valid Base
                        //outside the three actual source groups gets580D,
                        //even when its button is disabled in this stage.
                        if (id is < 1 or > 61 || !Valid(BaseLookup.Find(id, C)))
                            return footholdHints[key] = new(null, null);
                        var choice = Footholds(target, from).FirstOrDefault(row => row.Id == id);
                        return footholdHints[key] = choice is null ? new(0x580D, null)
                            : new(choice.HintMessageId, choice.UnresolvedOwner);
                    }
                    default: throw CampaignPageMissing("hint_stage");
                }
            }
            catch (InvalidDataException error) { return new(null, error.Message); }
        }
    }
}
