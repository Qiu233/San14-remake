using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //TC2A4980 / SC2A47D0: source priority identities, followed only if necessary by
    //the live Person100 order. These are source rules, not scenario patches.
    private static readonly int[] ForceEmergencePriorityPeople =
        [521,952,551,557,35,32,736,987,607,553,262,166,239,266,925,666,771,893,526,601,625,147,656];

    internal GameState ApplyNormalTurnMonthlyFixedEmergenceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner,current)) throw ForceEmergenceMissing("shared_pool");
        var parameters = state.NativeArmySimulation?.Parameters ?? throw ForceEmergenceMissing("parameter_owner");
        if (Seed.Language is not ("TC" or "SC") || parameters.Language != Seed.Language ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw ForceEmergenceMissing("rule_owner");
        var season = state.NativeMapSeason ?? throw ForceEmergenceMissing("calendar");
        int elapsed = unchecked((state.CampaignEventWorld ?? throw ForceEmergenceMissing("World454_owner")).ElapsedPeriodOffset + (int)season.AdvancedPeriods);
        //Each edition reads its own file-backed elapsed/ambition rules.
        if (elapsed <= NormalTurnForceEmergenceParameters.Read(state,"normal-force-emergence.minimum-elapsed-periods",18)) return state;
        if ((current.ReadRegistryReferences(RegistryKind.Force) ?? throw ForceEmergenceMissing("Force_C0")).Count() > 50) return state;
        bool neutral = false;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw ForceEmergenceMissing("City90"))
        {
            int city = E(entry?.PhysicalSlot,"City90_payload");
            var pointer = current.ReadCityTableReference(city) ?? throw ForceEmergenceMissing("City_pointer");
            if (E(current.ReadBaseForceSlot(pointer),"City_Force") == 0) { neutral = true; break; }
        }
        if (!neutral) return state;
        var effects = new List<CampaignEffectRequest>();
        var exclusions = current.CreateNearbyTemporary();
        HexListHandle? followers = null, scratch = null;
        try
        {
            var commandQueries = new PersonReturnGovernorQueries(state,_governorInputs,current);
            foreach (var task in current.ReadCurrentPersonLinkedEntities() ?? throw ForceEmergenceMissing("associated_registry18"))
            {
                if (task is null) throw ForceEmergenceMissing("associated_receiver");
                if (E(commandQueries.ReadCurrentAssociatedVirtual98(task),"associated_virtual98") != 16) continue;
                var members = current.ReadLinkedEntityMemberIds(task) ?? throw ForceEmergenceMissing("associated_members");
                if (members.Count != 0)
                {
                    uint id = E(members[0],"associated_first_member");
                    Append(exclusions,Lookup(id < 6001 ? (int)id : 6001));
                }
            }
            followers = current.CreateNearbyTemporary(); scratch = current.CreateNearbyTemporary();
            int? selected = null;
            foreach (int id in ForceEmergencePriorityPeople)
            {
                int person = Lookup(id);
                if (Contains(exclusions,person)) continue;
                current.ClearNearbyTemporary(scratch,false);
                if (!TrySelectForceEmergenceFollowersCurrent(ref state,current,person,scratch)) continue;
                selected = person; Copy(scratch,followers); break;
            }
            if (!selected.HasValue || !E(current.ReadPersonNativeValidity(selected.Value),"priority_person_validity"))
            {
                selected = null; int best = 0;
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw ForceEmergenceMissing("Person100"))
                {
                    int person = E(entry,"Person100_payload");
                    if (Contains(exclusions,person)) continue;
                    int ambition = E(RulerCaptureReleaseInputs.ReadCurrentWord(state,person,0x17C),"Person17C");
                    if (ambition < NormalTurnForceEmergenceParameters.Read(state,"normal-force-emergence.minimum-ambition",10)) continue;
                    current.ClearNearbyTemporary(scratch,false);
                    //The trial's sampler/App reads still occur for a valid
                    //candidate whose ambition does not beat the retained one.
                    if (TrySelectForceEmergenceFollowersCurrent(ref state,current,person,scratch) && best < ambition)
                    { best = ambition; selected = person; current.ClearNearbyTemporary(followers,false); Copy(scratch,followers); }
                }
            }
            if (!selected.HasValue || !E(current.ReadPersonNativeValidity(selected.Value),"selected_validity")) return state;
            int leader = selected.Value;
            var home = ResolveForceEmergenceHomeCurrent(current,leader);
            if (!E(current.ReadBaseNativeValidity(home),"selected_Base_validity")) return state;
            var date = state.NativeMapSeason?.ThirdDate ?? throw ForceEmergenceMissing("source_date");
            var created = CreateStrategicIndependenceForceCurrent(state,current,pool,leader,home,date,effects);
            state = created.State;
            if (!E(current.ReadForceNativeValidity(created.Force),"created_Force_validity")) return state;
            int newForce = E(created.Force.PhysicalSlot,"created_Force_identity");
            int leaderId = E(current.ReadPersonIdentity(leader),"leader_identity");
            int homeCode = E(current.ReadBaseObjectLink(home),"Home4E");
            Sound(21);
            effects.Add(new(null,null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.CameraFocus,current.ReadBaseCurrentHexIndex(home)) });
            Present(0x5E1A,leaderId,new(Person0:leaderId));
            effects.Add(new(null,null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationEnter,null) { SceneKind = 40 } });
            var announcement = Format(0x5E28,new(Person0:leaderId,Base0:(ushort)homeCode,Force0:newForce));
            Sound(8);
            effects.Add(new(null,null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,null,null,null,null,announcement.Text) });
            effects.Add(new(null,null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.NarrationExit,null) });
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw ForceEmergenceMissing("player_Force");
            var playerPrimary = ForcePrimaryDistrictSource.Resolve(current,player);
            int district = E(current.ReadPersonDistrictSlot(leader),"leader_DistrictE0");
            if (E(current.ReadDistrictNativeValidity(playerPrimary),"player_primary_validity"))
            {
                var main = Format(0x5E29,new(Person0:leaderId,Base0:(ushort)homeCode));
                var detail = Format(0x5242,new(Person0:5012,Person1:PlayerRuler(),Person2:leaderId,Base0:(ushort)homeCode));
                History(main.Text,detail.Text,district,current.ReadBaseCurrentHexIndex(home));
            }
            foreach (int person in People(followers))
            {
                int id = E(current.ReadPersonIdentity(person),"follower_identity");
                if (E(current.ReadPersonStatus(person),"follower_status") is >= 1 and <= 4)
                {
                    var main = Format(0x5E2A,new(Person0:id,Force0:newForce));
                    var detail = Format(0x524A,new(Person0:5012,Person1:PlayerRuler(),Person2:id,Force0:newForce));
                    var oldHome = ResolveForceEmergenceHomeCurrent(current,person);
                    History(main.Text,detail.Text,district,current.ReadBaseCurrentHexIndex(oldHome));
                }
                var destination = current.ReadDistrictTableReference(E(current.ReadPersonDistrictSlot(leader),"join_District"))
                    ?? throw ForceEmergenceMissing("join_District_pointer");
                state = ApplyRecruitJoinCurrent(state,current,person,destination,home,date,Day,effects).State;
                state = ApplyStrategicIndependencePlacementCurrent(state,current,pool,person,home,date);
                byte loyalty = (byte)Math.Max(85,(int)E(ArmyDestructionLoyalty.ReadCurrentRawE8(state,current,person,_governorInputs),"follower_loyalty"));
                if ((E(current.ReadArmyReturnPersonFlags196(person),"follower_flags15E") & 0x2000) != 0) loyalty = 255;
                current.SetPersonDestructionLoyaltyE8(person,loyalty);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state,current,person,loyalty);
            }
            if (E(current.ReadDistrictNativeValidity(playerPrimary),"tail_player_primary_validity"))
                state = WriteForceEmergenceChronicleCurrent(state, current, new(newForce, leaderId, homeCode, Day()));
            effects.Add(new(null,null) { ForceCoalitionVisual = new(ForceCoalitionVisualOperation.CameraRestore,null) });
            state = OwnedArmyReturnCurrentFields.PublishCurrent(state,current);
            state = state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw ForceEmergenceMissing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });
            if (effects.Count != 0)
                state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw ForceEmergenceMissing("effect_queue"))
                    .Append(CampaignEffectSource.MonthlyMaintenance,state.NativeClock?.UpdateOrdinal ?? throw ForceEmergenceMissing("effect_revision"),effects) };
            return state;

            int PlayerRuler()
            {
                int force = E(current.ReadDistrictForceId(E(playerPrimary.PhysicalSlot,"player_primary_pointer")),"player_primary_Force");
                return E(current.ReadMoraleWaveRuler22EAC0(current.ReadForceTableReference(force) ?? throw ForceEmergenceMissing("player_primary_Force_pointer")),"player_ruler");
            }
        }
        finally
        {
            if (scratch is not null) current.ClearNearbyTemporary(scratch,true);
            if (followers is not null) current.ClearNearbyTemporary(followers,true);
            current.ClearNearbyTemporary(exclusions,true);
        }

        int Lookup(int id) => E((current.ReadPersonLookupReference(id <= 6000 ? id : 0))?.PhysicalSlot,"Person_lookup");
        int Day() => (ReadArmyDestructionSampler(state,current) ?? throw ForceEmergenceMissing("Day450")).Day450;
        HistoryTextResult Format(int root,HistoryMessageOperands operands)
        { var text=FormatNormalTurnReport(state,current,root,operands);state=text.State;return text; }
        void Present(int root,int speaker,HistoryMessageOperands operands)
        {
            var source=state;var text=Format(root,operands);
            effects.Add(CreateStrategicCommandPresentation(current,source,speaker,text));
        }
        void Sound(int value) => effects.Add(new(null,null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue,value,null,null,null,null) });
        void History(string text,string detail,int district,int hex)
            => state=WriteHistory(state,current,new(1,district,hex,Day(),text,detail,5012,MarkBit0:1,MarkBit4:0),effects);
        void Append(HexListHandle list,int person)
        {
            bool locked=E(current.ReadRegistryLockEnabled(),"append_lock");
            if(locked)current.EnterRegistryLock();
            try
            {
                var node=current.AllocateRegistryNode(new(list.Identity)) ?? throw ForceEmergenceMissing("append_node");
                if(node.Identity.HasValue)current.WriteRegistryNodePayload(node,new(RegistryKind.Person,person));
            }
            finally {if(locked)current.ExitRegistryLock();}
        }
        IEnumerable<int> People(HexListHandle list)
        {
            var node=current.ReadRegistryHead(new(list.Identity)) ?? throw ForceEmergenceMissing("list_head");
            while(node.Identity.HasValue)
            {
                var value=current.ReadRegistryNodePayload(node) ?? throw ForceEmergenceMissing("list_payload");
                if(value.Kind!=RegistryKind.Person)throw ForceEmergenceMissing("list_kind");
                yield return E(value.PhysicalSlot,"list_Person");
                node=current.ReadRegistryNodeNext(node) ?? throw ForceEmergenceMissing("list_next");
            }
        }
        bool Contains(HexListHandle list,int person) => People(list).Contains(person);
        void Copy(HexListHandle from,HexListHandle to) {foreach(int person in People(from))Append(to,person);}
        static T E<T>(T? value,string owner) where T:struct => value ?? throw ForceEmergenceMissing(owner);
    }
    private static InvalidDataException ForceEmergenceMissing(string owner)=>new("normal.turn.force-emergence."+owner);
}
