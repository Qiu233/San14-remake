using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonCaptureForceDepartmentRemoval(int DepartmentRow, int Ordinal,
    ushort PersonBefore54, ushort? FilterBefore62);
public sealed record OriginalPersonCaptureForceDepartmentsReceipt(int ForcePhysical, int PersonPhysical,
    ushort PersonId, ushort? Advisor52, OriginalPersonCaptureForceDepartmentRemoval[] Removals,
    bool PolicyRefreshFlag16A8Written, string Branch);
public sealed record OriginalPersonCaptureForceDepartmentsResult(GameState? State,
    OriginalPersonCaptureForceDepartmentsReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2E8160's current native Force52/dept54/filter62 owner is the
/// existing OriginalBattleRemakeCurrentForcePolicies saved parameter bank.
/// Its actual CE329 stream is already installed once by the owned package;
/// Core Governance slot IDs are not this five-by-seven native matrix.</summary>
public static class OriginalPersonCaptureForceDepartments
{
    public static OriginalPersonCaptureForceDepartmentsResult ApplyCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalForceReference originalForce, int personPhysical)
    {
        try
        {
            if (!Known(current.ReadForceNativeValidity(originalForce), "current.2E8160_Force_validity") ||
                !Known(current.ReadPersonNativeValidity(personPhysical), "current.2E8160_Person_validity"))
                return new(state, null, null);
            int force = Known(current.ReadForcePhysicalSlot(originalForce), "current.2E8160_Force_physical");
            ushort id = Known(current.ReadPersonIdentity(personPhysical), "current.2E8160_Person10");
            var simulation = state.NativeArmySimulation ?? throw Missing("current.Force_department_parameter_owner");
            ushort advisor = Word(OriginalBattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x52));
            if (advisor == id) throw Missing("branch.2E8160_advisor52_2E9020_27BE40_27AA40_2FF040");
            var removed = new List<OriginalPersonCaptureForceDepartmentRemoval>();
            for (int row = 0; row < 5; row++)
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    ushort person = Word(OriginalBattleRemakeCurrentForcePolicies.DepartmentPersonKey(force, row, ordinal));
                    if (person != id) continue;
                    if (row == 1) throw Missing("branch.2E8160_department1_27BE40_current_Base_Army_morale_clamp");
                    if (row == 3) throw Missing("branch.2E8160_department3_2FF040_current_City_durability_and66");
                    if (row == 4) throw Missing("branch.2E8160_department4_41D8D0_effect14_Force15E");
                    removed.Add(new(row, ordinal, person,
                        OptionalWord(OriginalBattleRemakeCurrentForcePolicies.DepartmentFilterKey(force, row, ordinal))));
                }
            //2338E0 is called even if no slot matched. Only a changed player
            //Force reaches the actual World16A8 OR0x80000 writer.
            var player = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("current.2E8160_PlayerForce32EFA0");
            int playerPhysical = Known(current.ReadForcePhysicalSlot(player), "current.2338E0_PlayerForce_ordinal60");
            bool isPlayer = playerPhysical != 0 && playerPhysical == force;
            bool writesFlag = isPlayer && removed.Count != 0;
            uint? beforeFlag = writesFlag ? Known(OriginalOwnedArmyReturnServiceInputs.ReadCurrent(state), "current.World16A8") : null;
            if (removed.Count == 0)
                return new(state, new(force, personPhysical, id, advisor, [], false, "source.no_matching_native_department"), null);
            var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
            foreach (var row in removed)
            {
                //2415B0(force,row,column,0), followed by the raw companion
                //word62 clear. This does not require an old filter value.
                values[OriginalBattleRemakeCurrentForcePolicies.DepartmentPersonKey(force, row.DepartmentRow, row.Ordinal)] = 0;
                values[OriginalBattleRemakeCurrentForcePolicies.DepartmentFilterKey(force, row.DepartmentRow, row.Ordinal)] = 0;
            }
            if (writesFlag)
            {
                uint after = beforeFlag!.Value | 0x80000;
                values[OriginalOwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)after);
                current.RawTransferServiceFlags16A8 = after;
            }
            var updated = state with { NativeArmySimulation = simulation with
                { Parameters = simulation.Parameters with { Int32Values = values } } };
            //Later source queries must consume the changed saved native rows,
            //rather than the closure over the entry state installed at capture.
            current.BaseForcePolicyEffectReader = (target, effect) => target.PhysicalSlot is int physical &&
                updated.Forces.TryGetValue(physical, out var owner)
                ? OriginalBattleRemakeCurrentForcePolicies.ReadForceEffect(updated, owner, effect) : null;
            return new(updated, new(force, personPhysical, id, advisor, removed.ToArray(), writesFlag,
                "source.department0_or2_person_and_filter_clear"), null);

            ushort Word(string key) => OptionalWord(key) ?? throw Missing("current.Force_department_word:" + key);
            ushort? OptionalWord(string key) => simulation.Parameters.Int32Values.TryGetValue(key, out int raw) &&
                raw is >= 0 and <= ushort.MaxValue ? (ushort)raw : null;
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}
