using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class MonthlyPersonnelLoyaltySource
    {
        internal bool ReadSuccessionDefection(int person) => Defects(person);
    }

    internal sealed partial class PersonDeathSource
    {
        //File-backed TC1413A8C70,22 DWORDs, read offline as one bounded
        //88-byte table. No original executable is consulted by the game.
        private static readonly int[] RulerSuccessionPriority =
            [132, 607, 736, 521, 35, 32, 953, 551, 266, 557, 771, 987, 952, 553, 526, 262, 166, 925, 666, 769, 893, 97];

        //28C5D0 retains three independent Person lists until its final tail.
        private int? ApplyRulerSuccessorSelection(int deceased)
        {
            var force = deathOriginalForce ?? throw Missing("succession_captured_Force");
            if (CountRulerServingPeople(force) == 1 ||
                Known(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "succession_City_count") == 0)
                throw Missing("death_force_unregister293BD0_current_operation");
            var candidates = Temporary();
            try
            {
                foreach (int person in RulerForcePeople(force, servingOnly: true))
                    if (Identity(person) != Identity(deceased) && !BandPersonAtOrdinaryBase(person) &&
                        (Known(current.ReadOwnedReturnPersonFlags(person), "successor_flags15E") & 0x4000) == 0)
                        Append(candidates, new(RegistryKind.Person, person));
                int count = Known(current.ReadTemporaryHexListCount(candidates), "successor_candidate_count");
                int successor;
                if (count == 1) successor = ItemPerson(ItemHead(candidates));
                else
                {
                    if (IsPlayer(force)) return SuspendRulerSuccession(deceased, force, candidates);
                    int oldId = Identity(deceased);
                    //World50 affects only these six historical pairs. For
                    //other identities both source branches are equivalent.
                    if (oldId is 557 or 521 or 952 or 35 or 953 or 638)
                        throw Missing("historical_successor_World50");
                    var preferred = Temporary();
                    try
                    {
                        for (int family = 0; family < 8; family++)
                        {
                            current.ClearTemporaryHexList(preferred);
                            bool male = false;
                            for (var node = ItemHead(candidates); node.Identity.HasValue; node = ItemNext(node))
                            {
                                int person = ItemPerson(node);
                                if (!RulerSuccessorFamily(person, deceased, family)) continue;
                                male |= Owner(person).Sex switch
                                { OfficerSex.Male => true, OfficerSex.Female => false, _ => throw Missing("successor_sex_A9") };
                                Append(preferred, new(RegistryKind.Person, person));
                            }
                            if (male && ItemHead(preferred).Identity.HasValue) break;
                        }
                        if (!ItemHead(preferred).Identity.HasValue)
                        {
                            int maximum = 0;
                            int? chosen = null;
                            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario,
                                package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                            for (var node = ItemHead(candidates); node.Identity.HasValue; node = ItemNext(node))
                            {
                                int person = ItemPerson(node);
                                int leadership = Known(abilities.ResolveDimension(latest, Identity(person), 0,
                                    (f, effect) => package._baseCapacityInputs.ReadPanelModifier(latest, f, effect)), "successor_Leadership231B50");
                                if (maximum >= leadership) continue;
                                maximum = leadership; chosen = person;
                            }
                            successor = chosen ?? throw Missing("successor_empty_or_zero_Leadership_selection");
                        }
                        else
                        {
                            SortRulerPeople(preferred, (a, b) => RulerSuccessorBefore(a, b, deceased));
                            successor = ItemPerson(ItemHead(preferred));
                        }
                    }
                    finally { ClearRelease(preferred); }
                }
                return CompleteRulerSuccessorSelection(deceased, successor, force);
            }
            finally { ClearRelease(candidates); }
        }

        private int CompleteRulerSuccessorSelection(int deceased, int successor, ForceReference force)
        {
            ApplyRulerSuccessionReports(deceased, successor, force);
            var retained = Temporary();
            try
            {
                ApplyRulerSuccessionTransfer(deceased, successor, retained);
                var associated = current.ReadPersonLinkedEntity(successor) ?? throw Missing("successor_associated22AC00");
                if (Known(current.ReadLinkedEntityNativeValidity(associated), "successor_associated_validity") &&
                    ItemAssociatedKind(associated) is 17 or 18)
                    CancelRulerSuccessorCaravanCurrent(successor, force, associated);
                SortRulerPeople(retained, (a, b) => RulerPostOrder(a) != RulerPostOrder(b)
                    ? RulerPostOrder(a) < RulerPostOrder(b) : Identity(a) < Identity(b));

                var effects = new List<CampaignEffectRequest>();
                int forceId = Known(current.ReadForcePhysicalSlot(force), "succession_independence_Force");
                latest = package.ApplyNormalTurnMonthlyGovernorIndependenceCurrent(latest, current, pool,
                    onlyForce: forceId, excludedPerson: new(deceased), sharedEffects: effects);
                AppendRulerSuccessionEffects(effects);

                var deferred = Temporary();
                try
                {
                    for (var node = ItemHead(retained); node.Identity.HasValue; node = ItemNext(node))
                    {
                        int person = ItemPerson(node);
                        if (Identity(person) == Identity(deceased) ||
                            !new MonthlyPersonnelLoyaltySource(package, latest, current, pool).ReadSuccessionDefection(person)) continue;
                        if (!IsPlayer(Force(person))) RulerSuccessionFreedom(person, successor);
                        else Append(deferred, new(RegistryKind.Person, person));
                    }
                    for (var node = ItemHead(deferred); node.Identity.HasValue; node = ItemNext(node))
                    {
                        int person = ItemPerson(node);
                        ApplyRulerSuccessionDefectionReports(person, successor, force);
                        RulerSuccessionFreedom(person, successor);
                    }
                }
                finally { ClearRelease(deferred); }
                var districtEffects = new List<CampaignEffectRequest>();
                var date = latest.NativeMapSeason?.ThirdDate ?? throw Missing("succession_District_date");
                var operation = package.CreateCaptureDistrictOperationQueries(current, () => latest,
                    value => latest = value, date, districtEffects);
                foreach (var row in current.ReadRegistryReferences(RegistryKind.District) ?? throw Missing("succession_DistrictD0"))
                {
                    if (row is not { Kind: RegistryKind.District, PhysicalSlot: int physical }) throw Missing("succession_District_payload");
                    if (Known(current.ReadDistrictForceId(physical), "succession_District_Force") != forceId) continue;
                    var result = PersonAssignmentPrecallSource.Apply2E9140PrimaryForCapture(current, new(physical), operation);
                    if (result.UnresolvedOwner is not null) throw Missing(result.UnresolvedOwner);
                }
                latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, operation);
                latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current, operation.ChangedAreas, operation.ChangedGates);
                latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
                AppendRulerSuccessionEffects(districtEffects);
                ApplyRulerSuccessionForceTail(force, successor, deceased);
                return successor;
            }
            finally { ClearRelease(retained); }
        }

        private bool RulerSuccessorFamily(int person, int deceased, int family) => family switch
        {
            0 => ItemParent(person, deceased) && Relation(person, 0xB2) == Relation(deceased, 0xB2),
            1 => RulerSiblings(person, deceased),
            2 => OfficeRelatedCurrent(latest, current, person, deceased),
            3 => ItemSworn(person, deceased),
            4 => ItemRelative(person, deceased, 0xBA),
            //The bounded offline analysis image resolves2811E0: it walks
            //the same22 DWORD Person identities used by the next comparator.
            5 => Array.IndexOf(RulerSuccessionPriority, (int)Identity(person)) >= 0,
            6 => RulerSuccessorSeniority(person),
            7 => ItemRawHit(deceased, person, like: true),
            _ => throw Missing("successor_priority_family")
        };

        private bool RulerSuccessorSeniority(int person)
        {
            if (Known(RulerCaptureReleaseInputs.ReadCurrentWord(latest, person, 0x17C), "successor_Person17C") < 10) return false;
            int office = Known(current.ReadMoraleWavePersonPostE7(person), "successor_seniority_E7");
            if (Known(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(latest, office), "successor_Post_validity") &&
                Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest, office), "successor_Post37") >= 2) return false;
            int year = latest.NativeMapSeason?.ThirdDate.Year ?? throw Missing("successor_World34_year");
            return unchecked((short)year - Known(package._governorInputs.ReadCurrentPhysicalWord166(latest, person), "successor_Person166")) > 4;
        }

        private bool RulerSuccessorBefore(int a, int b, int deceased)
        {
            int first = Array.IndexOf(RulerSuccessionPriority, (int)Identity(a));
            int second = Array.IndexOf(RulerSuccessionPriority, (int)Identity(b));
            first = first < 0 ? int.MaxValue : first; second = second < 0 ? int.MaxValue : second;
            if (first != second) return first < second;
            bool maleA = Owner(a).Sex == OfficerSex.Male, maleB = Owner(b).Sex == OfficerSex.Male;
            if (Owner(a).Sex is not (OfficerSex.Male or OfficerSex.Female) || Owner(b).Sex is not (OfficerSex.Male or OfficerSex.Female))
                throw Missing("successor_sort_sex");
            if (maleA != maleB) return maleA;
            if (Age(a) != Age(b)) return Age(a) > Age(b);
            bool parentA = ItemParent(deceased, a), parentB = ItemParent(deceased, b);
            if (parentA != parentB) return parentA;
            bool grandchildA = RulerGrandchild(a, deceased), grandchildB = RulerGrandchild(b, deceased);
            if (grandchildA != grandchildB) return grandchildA;
            if (RulerPostOrder(a) != RulerPostOrder(b)) return RulerPostOrder(a) < RulerPostOrder(b);
            return Identity(a) < Identity(b);
        }

        private bool RulerGrandchild(int person, int ancestor) =>
            ItemParent(Physical(ItemLookup(Relation(person, 0xB4)), "successor_father"), ancestor) ||
            ItemParent(Physical(ItemLookup(Relation(person, 0xB6)), "successor_mother"), ancestor);
        private bool RulerSiblings(int a, int b) => ItemPair(a, b) &&
            (Relation(a, 0xB4) != 0 && Relation(a, 0xB4) == Relation(b, 0xB4) ||
             Relation(a, 0xB6) != 0 && Relation(a, 0xB6) == Relation(b, 0xB6));
        private byte RulerPostOrder(int person) => Known(OwnedArmyMoraleWaveInputs.ReadPost37(latest,
            Known(current.ReadMoraleWavePersonPostE7(person), "successor_sort_E7")), "successor_sort_Post37");
        private IEnumerable<int> RulerForcePeople(ForceReference force, bool servingOnly)
        {
            int id = Known(current.ReadForcePhysicalSlot(force), "successor_iterator_Force60");
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("successor_Person100"))
            {
                int person = Known(entry, "successor_Person_payload");
                if (id is < 1 or > 51 || Known(current.ReadPersonOldForceSlot(person), "successor_Person_Force") != id) continue;
                if (servingOnly && Status(person) is < 1 or > 4) continue;
                yield return person;
            }
        }
        private void SortRulerPeople(HexListHandle list, Func<int, int, bool> before)
        {
            var order = new List<HexListNodeReference>();
            for (var node = ItemHead(list); node.Identity.HasValue; node = ItemNext(node)) order.Add(node);
            for (int index = 1; index < order.Count; index++)
            {
                var value = order[index]; int at = index;
                while (at > 0 && before(ItemPerson(value), ItemPerson(order[at - 1]))) { order[at] = order[at - 1]; at--; }
                order[at] = value;
            }
            current.ReorderCurrentList(list, order);
        }

        private void ApplyRulerSuccessionReports(int deceased, int successor, ForceReference force)
        {
            bool playerSuccession = IsPlayer(force);
            var effects = new List<CampaignEffectRequest>();
            if (playerSuccession) effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.SoundCue, 5, null, null, null, null) });
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.FinalOrderSceneEnter, 31, null, null) });
            var speech = package.FormatNormalTurnReport(latest, current, 0x2A3E,
                new(Person0: Identity(successor), Person1: Identity(deceased))); latest = speech.State;
            effects.Add(CreateSingleOfficerPresentation(current, latest, Identity(successor), speech,
                TalentCommandPresentationOperation.ConditionalOfficerText, "ruler.succession"));
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.FinalOrderSceneExit, null, null, null) });
            var main = package.FormatNormalTurnReport(latest, current, 0x2A88,
                new(Person0: Identity(successor), Person1: Identity(deceased), Force0: Known(current.ReadForcePhysicalSlot(force), "succession_history_Force60")));
            latest = main.State;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current) ?? throw Missing("succession_history_player");
            string detailText = string.Empty;
            if (!playerSuccession)
            {
                var detail = package.FormatNormalTurnReport(latest, current, 0x533E,
                    new(Person0: 5012, Person1: package.TalentRulerCurrent(current, player), Person2: Identity(successor), Person3: Identity(deceased)));
                latest = detail.State; detailText = detail.Text;
            }
            else ApplyRulerSuccessionChronicle(force, successor);
            var home = BaseLookup.Find(Known(current.ReadPersonFirstLocation(successor), "succession_history_E2"), current);
            latest = package.WriteHistory(latest, current, new(1,
                Known(current.ReadPersonDistrictSlot(deceased), "succession_history_E0"), current.ReadBaseCurrentHexIndex(home),
                DeathHistoryDay(), main.Text, detailText, 5012, MarkBit0: 1, MarkBit3: 1), effects);
            AppendRulerSuccessionEffects(effects);
        }
        private void ApplyRulerSuccessionChronicle(ForceReference force, int successor)
        {
            if ((latest.CampaignChronicle?.RecordingSuppression ?? throw Missing("succession_chronicle_control")) != 0) return;
            string forceName = package.HistoryWriter.CustomFirstName(latest, current, force, KinFullName);
            string forceLabel = package.HistoryWriter.FallbackName(latest, current, force, KinFullName);
            string name = KinFullName(Owner(successor), true);
            int color = Known(current.ReadForceReportColor(force), "succession_chronicle_color");
            latest = new CampaignChronicleWriter(package.HistoryWriter).Write(latest, current,
                new(20, 1, DeathHistoryDay(), forceName, color, 9,
                    System.Collections.Immutable.ImmutableArray.Create(forceLabel, name, string.Empty),
                    System.Collections.Immutable.ImmutableArray.Create(48400, 48400, 48400, 48400)),
                package.ReadApplicationGameplay()?.UserSelectionServicePresent);
        }

        private void RulerSuccessionFreedom(int person, int successor)
        {
            var effects = new List<CampaignEffectRequest>();
            latest = package.ApplyCaptiveFreedomCurrent(latest, current, pool, person,
                latest.NativeMapSeason?.ThirdDate ?? throw Missing("succession_freedom_date"), DeathHistoryDay,
                () => package.ReadArmyDestructionSampler(latest, current) ?? throw Missing("succession_freedom_sampler"), effects,
                formerRuler: new(successor));
            AppendRulerSuccessionEffects(effects);
        }
        private void ApplyRulerSuccessionDefectionReports(int person, int successor, ForceReference force)
        {
            var effects = new List<CampaignEffectRequest>();
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.PersonnelSceneEnter, 9, null, null) });
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 7, null, null, null, null) });
            var speech = package.FormatNormalTurnReport(latest, current, 0x5E0D, new(Person0: Identity(person), Person1: Identity(successor))); latest = speech.State;
            effects.Add(CreateSingleOfficerPresentation(current, latest, Identity(person), speech,
                TalentCommandPresentationOperation.ConditionalOfficerText, "ruler.succession_defection"));
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.PersonnelSceneExit, null, null, null) });
            var main = package.FormatNormalTurnReport(latest, current, 0x5E19,
                new(Force0: Known(current.ReadForcePhysicalSlot(force), "succession_defection_Force60"), Person0: Identity(person))); latest = main.State;
            var detail = package.FormatNormalTurnReport(latest, current, 0x5239,
                new(Person0: 5012, Person1: Identity(successor), Person2: Identity(person))); latest = detail.State;
            int district = Known(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "succession_defection_District");
            latest = package.WriteHistory(latest, current, new(2, district,
                Position(person).PhysicalSlot ?? 48400, DeathHistoryDay(), main.Text, detail.Text, 5012, MarkBit0: 1, MarkBit4: 1), effects);
            AppendRulerSuccessionEffects(effects);
        }
    }
}
