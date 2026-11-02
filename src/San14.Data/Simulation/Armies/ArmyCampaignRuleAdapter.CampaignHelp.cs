using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignHelpAdviser(int PersonId, string Name, string Body,
    DeploymentNarrationPortraitRoute StandingPortrait, int MessageId);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Capture once on actual Help entry, from an owned World or a
    /// stopped playback checkpoint. The caller owns the actual App32 draws;
    /// neither a paint nor a worker-owned candidate may call this query.</summary>
    public AreaAdministrationQuery<CampaignHelpAdviser> QueryCampaignHelpAdviser(
        GameState ownedOrBorrowed, Func<int, int> drawApp32)
    {
        ArgumentNullException.ThrowIfNull(ownedOrBorrowed);
        ArgumentNullException.ThrowIfNull(drawApp32);
        try
        {
            var current = CaptureCurrentDefinitions(ownedOrBorrowed)
                ?? throw Missing("current_definitions");
            //The adviser counts the actual registered Person chain and uses
            //the current list pool. Definitions alone do not own those lists.
            var lifecycle = ownedOrBorrowed.ReadArmyLifecycle() ?? throw Missing("saved_indexes");
            _ = WorldIndexContext.ImportOwnedState(current, lifecycle.Indexes, ownedOrBorrowed);
            //The welcome expressions and fallback ability comparison also
            //read the actual saved rank/office and office-bonus owners.
            _moraleWaveInputs.BindCurrent(ownedOrBorrowed, current);
            //The existing scalar binder owns Force52 for both the selection
            //and welcome expression. Person-area vector writes are unrelated.
            _personAssignmentPrecallInputs.BindAreaAdministrationScalars(ownedOrBorrowed, current);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(ownedOrBorrowed, current)
                ?? throw Missing("World3A_player_Force");
            int physicalForce = Need(player.PhysicalSlot, "player_Force_pointer");
            ushort rulerId = (current.ReadForceRecord(physicalForce)
                ?? throw Missing("Force_ruler10")).RawRuler10;
            var ruler = Lookup(rulerId);
            int rulerIdentity = Need(current.ReadPersonIdentity(Need(ruler.PhysicalSlot,
                "ruler_pointer")), "ruler_identity");
            var speaker = Lookup(5005);
            var abilities = SearchAbilities(current);
            if (Need(current.ReadForceNativeValidity(player), "player_Force_validity"))
            {
                int count = 0;
                foreach (int? row in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("Person_registered_order"))
                {
                    int physical = Need(row, "Person_payload");
                    //230570(Force,0,0) tests ownership before filter-zero status.
                    if (Need(current.ReadPersonOldForceSlot(physical), "Person_Force") != physicalForce) continue;
                    if (Need(current.ReadPersonStatus(physical), "Person_status") is >= 1 and <= 4) count++;
                }
                if (count > 1)
                {
                    var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("advisor_current_pool");
                    var selection = new PersonDeathHistorySelection(ownedOrBorrowed, current, pool,
                        abilities, _governorInputs, (force, effect) =>
                            _baseCapacityInputs.ReadPanelModifier(ownedOrBorrowed, force, effect));
                    speaker = selection.Select(ForcePrimaryDistrictSource.Resolve(current, player));
                }
            }
            int speakerPhysical = Need(speaker.PhysicalSlot, "speaker_pointer");
            if (!Need(current.ReadPersonNativeValidity(speakerPhysical), "speaker_validity"))
                return AreaAdministrationQuery<CampaignHelpAdviser>.Known(null!);
            int speakerId = Need(current.ReadPersonIdentity(speakerPhysical), "speaker_identity");
            if (!ownedOrBorrowed.Officers.TryGetValue(speakerId, out var person))
                throw Missing("speaker_owned_person");
            ushort appearance = Need(current.ReadPersonPortraitAppearance(speakerPhysical), "speaker_appearance");
            var route = ReadNarrationPortrait(ownedOrBorrowed, current, _definitions, speakerId, appearance);
            var people = new DeploymentNarrationCurrentPeople(ownedOrBorrowed, current, abilities,
                speakerId, rulerIdentity, panelReader: (force, effect) =>
                    _baseCapacityInputs.ReadPanelModifier(ownedOrBorrowed, force, effect),
                appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language);
            var formatter = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            var text = formatter.ResolveCampaignHelpAdviser(people, drawApp32);
            return AreaAdministrationQuery<CampaignHelpAdviser>.Known(new(speakerId, person.Name,
                text.Text, route, text.MessageId));

            TransferPersonReference Lookup(int raw) =>
                (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("Person_lookup");
        }
        catch (InvalidDataException error)
        {
            return AreaAdministrationQuery<CampaignHelpAdviser>.Unknown(error.Message);
        }
        static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        static InvalidDataException Missing(string name) => new("campaign.help.adviser." + name);
    }
}
