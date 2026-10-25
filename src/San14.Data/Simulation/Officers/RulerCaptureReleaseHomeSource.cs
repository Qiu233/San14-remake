using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2F86F0 has completed the release relationship/fame writes. Its last
    //branch is precisely260310 for matching physical Base/Home, otherwise
    //2EA9A0. Both borrow this destruction's current pool and source date.
    internal RulerCaptureReleaseResult ApplyRulerCaptureReleaseCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, RulerCaptureDecisionResult decision,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
        bool playerValid = Need(current.ReadForceNativeValidity(player), "player_Force_validity");
        return RulerCaptureReleaseSource.Release(state, current, decision, date, new(playerValid), ReturnHome, Present);

        GameState Present(GameState image)
        {
            var receipt = decision.Receipt ?? throw Missing("decision_context");
            var playerDistrict = ForcePrimaryDistrictSource.Resolve(current, player);
            var selected = new PersonDeathHistorySelection(image, current, pool, SearchAbilities(current), _governorInputs,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(image, force, effect)).Select(playerDistrict);
            int reporter = selected.PhysicalSlot is int at && Need(current.ReadPersonNativeValidity(at), "reporter_validity")
                ? Need(current.ReadPersonIdentity(at), "reporter_Person10") : 5012;
            var before = image;
            var speech = FormatNormalTurnReport(image, current, 0x2C51,
                new(Person0: receipt.PersonId, Person1: receipt.WinnerRulerId));
            image = speech.State;
            effects.Add(CreateSingleOfficerPresentation(current, before, receipt.PersonId, speech,
                TalentCommandPresentationOperation.ConditionalOfficerText, "ruler.release"));
            if (Need(player.PhysicalSlot, "player_Force_ordinal") == receipt.WinnerForcePhysical) return image;
            var main = FormatNormalTurnReport(image, current, 0x2C63,
                new(Force0: receipt.WinnerForcePhysical, Person0: receipt.PersonId));
            image = main.State;
            int playerRuler = Need(current.ReadMoraleWaveRuler22EAC0(player), "player_ruler");
            var detail = FormatNormalTurnReport(image, current, 0x5303,
                new(Person0: reporter, Person1: playerRuler, Person2: receipt.PersonId, Force0: receipt.WinnerForcePhysical));
            image = detail.State;
            var victimForce = current.ReadPersonForce(receipt.PersonPhysical) ?? throw Missing("history_victim_Force");
            var district = ForcePrimaryDistrictSource.Resolve(current, victimForce);
            var position = PersonReturnCommands.ResolvePersonPosition(image, current, receipt.PersonPhysical, date);
            if (current.ReadOwnedPersonCommandManager() is { } positioned) image = image with { PersonnelCommands = positioned };
            int day = (ReadArmyDestructionSampler(image, current) ?? throw Missing("history_World450")).Day450;
            return WriteHistory(image, current, new(1, Need(district.PhysicalSlot, "history_District"),
                Need(position.PhysicalSlot, "history_Hex"), day, main.Text, detail.Text, reporter,
                MarkBit0: 1, MarkBit3: 1), effects);
        }

        PersonReturnCommandResult ReturnHome(GameState image, int person)
        {
            var working = image;
            ushort first = Need(current.ReadPersonFirstLocation(person), "PersonE2");
            ushort second = Need(current.ReadPersonSecondLocation(person), "PersonE4");
            ushort physicalBase = second is >= 1 and <= 61 ? second : (ushort)0;
            if (physicalBase == first)
            {
                int force = Need(current.ReadPersonOldForceSlot(person), "Person_Force");
                int id = Need(current.ReadPersonIdentity(person), "Person10");
                var operation = CreateCaptureDistrictOperationQueries(current, () => working,
                    next => working = next, date, effects,
                    decision.Receipt?.WinnerForcePhysical);
                var selected = CreateCaptureReturningPersonRules().TryApplyHomeCurrent(working, current,
                    first, force, id, date, operation, effects);
                working = selected.State ?? throw Missing(selected.UnresolvedOwner ?? "260310_Home");
                working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                    operation.ChangedAreas, operation.ChangedGates);
                var receipt = selected.Receipt ?? throw Missing("260310_receipt");
                return new(working, receipt.ReturnNative ?? 0, receipt.ReturnCommandId, null,
                    receipt.ReturnTravelDays, null);
            }

            current.WritePersonFirstLocation(person, Math.Min(first, (ushort)61));
            var forceReference = current.ReadPersonForce(person) ?? throw Missing("Force22CB40");
            bool validHome = Need(current.ReadForceNativeValidity(forceReference), "Force_validity");
            var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "clamped_E2"), current);
            if (validHome)
                validHome = Need(current.ReadBaseNativeValidity(home), "Home_validity") &&
                    Need(current.ReadBaseForceSlot(home), "Home_Force") ==
                    Need(current.ReadPersonOldForceSlot(person), "Person_Force");
            if (!validHome)
            {
                working = ApplyCaptiveFreedomCurrent(working, current, pool, person, date,
                    () => Sampler().Day450, Sampler, effects);
                return new(working, 0, null, null, null, null);
            }

            var start = PersonReturnCommands.ResolvePersonPosition(working, current, person, date);
            if (current.ReadOwnedPersonCommandManager() is { } positionedOwner)
                working = working with { PersonnelCommands = positionedOwner };
            PersonReturnCommandResult returned;
            if (Need(start.PhysicalSlot, "position_Hex") == current.ReadBaseCurrentHexIndex(home))
            {
                current.WritePersonSecondLocation(person,
                    Math.Min(Need(current.ReadBaseObjectLink(home), "Home4E"), (ushort)1061));
                returned = new(working, 0, null, null, null, null);
            }
            else
            {
                var prepared = PersonReturnCommands.Prepare(working, current, person, date);
                var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "2EA9A0_prepare");
                returned = PersonReturnCommands.Apply(working, current, packet);
                working = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "2EA9A0_apply");
            }
            int officerId = Need(current.ReadPersonIdentity(person), "published_Person10");
            var officer = working.Officers.GetValueOrDefault(officerId) ?? throw Missing("saved_Person");
            working = OfficerLocationSourceAdapter.PublishPosition(working with
            {
                Officers = new(working.Officers) { [officerId] = officer with
                {
                    LocationId = Need(current.ReadPersonFirstLocation(person), "published_PersonE2"),
                    Behavior = current.ReadPersonBehavior(person)
                } }
            }, officerId, current);
            working = working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw Missing("saved_lifecycle"))
                with { Indexes = pool.ProjectOwnedRoles() });
            return returned with { State = working };

            TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current)
                ?? throw Missing("World450");
        }

        static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
        static InvalidDataException Missing(string owner) => new("ruler-release.home." + owner);
    }
}
