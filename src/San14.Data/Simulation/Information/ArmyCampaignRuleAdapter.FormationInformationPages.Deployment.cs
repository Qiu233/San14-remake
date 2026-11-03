using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class FormationInformationCurrent
    {
        internal ImmutableDictionary<int, AreaAdministrationQuery<DeploymentFormationBalloon>> DeploymentBalloons(
            SourceWorldContext current, IEnumerable<DeploymentLandFormationChoice> choices)
        {
            var result = ImmutableDictionary.CreateBuilder<int, AreaAdministrationQuery<DeploymentFormationBalloon>>();
            byte? flags = null; string? forceMissing = null;
            try
            {
                // Reuse the existing current's actual Force pointer table.
                // Registry/list import does not supply this lookup channel.
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw Missing("deployment.current_player_Force");
                if (Need(current.ReadForceNativeValidity(player), "deployment.player_validity"))
                    flags = Need(PlaceholderWorldCurrentSource.ReadForceAdministrationFlags(state,
                        Need(player.PhysicalSlot, "deployment.player_receiver")), "deployment.player_flags");
            }
            catch (InvalidDataException error) { forceMissing = error.Message; }
            foreach (var choice in choices)
            {
                try
                {
                    int id = choice.FormationId;
                    // The complete current definition table has native-valid
                    // receivers1..20; zero is the authored invalid receiver.
                    if ((uint)(id - 1) >= 20) throw Missing("deployment.formation_receiver");
                    var name = Cell(id, 0).Display;
                    var description = Cell(id, 2).Display;
                    if (name.UnresolvedOwner is { } nameMissing) throw new InvalidDataException(nameMissing);
                    if (description.UnresolvedOwner is { } descriptionMissing) throw new InvalidDataException(descriptionMissing);
                    byte kind = Need(TacticRemakeCurrentFormations.ReadRawByte(state, id, 0x7E), "deployment.category");
                    byte range = Need(TacticRemakeCurrentFormations.ReadParameter(state, id, 0xA6), "deployment.range");
                    // The source reads Force validity/flags even for ordinary
                    // definitions. Missing current knowledge remains unknown.
                    if (forceMissing is not null) throw new InvalidDataException(forceMissing);
                    bool highlighted = flags is >= 0x80 && id == 10;
                    result.Add(id, AreaAdministrationQuery<DeploymentFormationBalloon>.Known(new(id,
                        name.Text!, description.Text!, kind, Data(kind == 2 ? 0x369 : 0x368), Data(0x36A),
                        Data(highlighted || (range & 1) != 0 ? 0x338 : 0x339), highlighted)));
                }
                catch (InvalidDataException error)
                { result.Add(choice.FormationId, AreaAdministrationQuery<DeploymentFormationBalloon>.Unknown(error.Message)); }
            }
            return result.ToImmutable();
        }
    }
}
