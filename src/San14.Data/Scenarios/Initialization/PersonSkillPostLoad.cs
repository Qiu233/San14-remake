namespace San14.Data;

public sealed record TacticReference(int? PhysicalSlot);
public sealed record GemReference(int? PhysicalSlot);

public interface IPersonSkillPostLoadState
{
    ushort? ReadPersonIdentity(int receiver);
    uint? ReadPersonFormationMask164(int receiver);
    void WritePersonFormationMask164(int receiver, uint value);
    ushort? ReadPersonTraitSlot150(int receiver, int index);
    void WritePersonTraitSlot150(int receiver, int index, ushort value);
    ushort? ReadPersonTraitMask1BC(int receiver);
    void WritePersonTraitMask1BC(int receiver, ushort value);
    void WritePersonTraitExtra1BE(int receiver, int index, ushort value);
    byte? ReadPersonTacticSlot168(int receiver, int index);
    void WritePersonTacticSlot168(int receiver, int index, byte value);
    PersonalityReference? ReadPersonalityTableReference(int slot);
    bool? ReadSkillPersonalityValidity(PersonalityReference reference);
    byte? ReadSkillPersonalityFlags(PersonalityReference reference);
    TacticReference? ReadSkillTacticReference(int slot);
    bool? ReadSkillTacticValidity(TacticReference reference);
    byte? ReadSkillTacticFlags(TacticReference reference);
    int? ReadSkillTacticPhysicalSlot(TacticReference reference);
    GemReference? ReadSkillGemReference(int slot);
    bool? ReadSkillGemValidity(GemReference reference);
    byte? ReadSkillGemTactic52(GemReference reference);
    HexListHandle? CreateTemporaryHexList();
    HexListNodeReference? ReadSkillTemporaryHead(HexListHandle handle);
    PersonalityReference? ReadSkillTemporaryPayload(HexListNodeReference node);
    HexListNodeReference? ReadHexListNodeNext(HexListNodeReference node);
    bool AppendSkillTemporaryPersonality(HexListHandle handle, PersonalityReference reference);
    bool? ReadRegistryLockEnabled();
    void EnterRegistryLock();
    void ExitRegistryLock();
}

/// <summary>224800's ordered formation/trait/tactic block. The returned
/// descriptor stays alive through cost, abilities and position repair.</summary>
public static class PersonSkillPostLoad
{
    public static HexListHandle Apply(int receiver, IPersonSkillPostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int count = 0;
        for (int index = 0; index <= 20; index++)
        {
            uint bit = 1u << index;
            if ((Known(state.ReadPersonFormationMask164(receiver), "current formation164") & bit) == 0) continue;
            if (index == 14 && !Bandit(Known(state.ReadPersonIdentity(receiver), "formation bit14 person ID"))) continue;
            if (++count > 5)
                state.WritePersonFormationMask164(receiver, Known(state.ReadPersonFormationMask164(receiver), "formation removal164") & ~bit);
            ushort id = Known(state.ReadPersonIdentity(receiver), "participating formation person ID");
            if (index == 14 && !Bandit(id))
                state.WritePersonFormationMask164(receiver, Known(state.ReadPersonFormationMask164(receiver), "changed formation bit14") & ~bit);
        }
        Enter(state);
        HexListHandle temporary = state.CreateTemporaryHexList()
            ?? throw new InvalidDataException("Current personality temporary descriptor allocation is unknown.");
        Exit(state);
        count = 0;
        for (int index = 0; index < 9; index++)
        {
            ushort requested = Known(state.ReadPersonTraitSlot150(receiver, index), "person trait150");
            PersonalityReference personality = state.ReadPersonalityTableReference(requested <= 355 ? requested : 0)
                ?? throw new InvalidDataException("Current personality pointer is unknown.");
            if (!Known(state.ReadSkillPersonalityValidity(personality), "current personality validity")) continue;
            if (++count > 5 || Contains(temporary, personality, state))
            { ClearTraits(receiver, state); break; }
            Enter(state);
            _ = state.AppendSkillTemporaryPersonality(temporary, personality);
            Exit(state);
            ushort id = Known(state.ReadPersonIdentity(receiver), "post-append person ID");
            if (Custom(id)) continue;
            if ((Known(state.ReadSkillPersonalityFlags(personality), "current personality BF") & 0x10) == 0)
            { ClearTraits(receiver, state); break; }
        }
        ushort tacticId = Known(state.ReadPersonIdentity(receiver), "tactic person ID");
        if (Custom(tacticId) || Bandit(tacticId)) return temporary;
        for (int index = 0; index < 10; index++)
        {
            byte requested = Known(state.ReadPersonTacticSlot168(receiver, index), "person tactic168");
            TacticReference tactic = state.ReadSkillTacticReference(requested <= 200 ? requested : 0)
                ?? throw new InvalidDataException("Current tactic pointer is unknown.");
            if (!Known(state.ReadSkillTacticValidity(tactic), "current tactic validity")) continue;
            if ((Known(state.ReadSkillTacticFlags(tactic), "current tactic flag44") & 2) != 0) continue;
            bool accepted = false;
            for (int slot = 0; slot < 31; slot++)
            {
                GemReference gem = state.ReadSkillGemReference(slot)
                    ?? throw new InvalidDataException("Current gem pointer is unknown.");
                if (!Known(state.ReadSkillGemValidity(gem), "current gem validity")) continue;
                int physical = Known(state.ReadSkillTacticPhysicalSlot(tactic), "fresh tactic physical slot");
                if (Known(state.ReadSkillGemTactic52(gem), "gem tactic52") == physical) { accepted = true; break; }
            }
            if (accepted) continue;
            for (int clear = 0; clear < 10; clear++) state.WritePersonTacticSlot168(receiver, clear, 1);
            break;
        }
        return temporary;
    }
    private static bool Contains(HexListHandle handle, PersonalityReference personality, IPersonSkillPostLoadState state)
    {
        HexListNodeReference node = state.ReadSkillTemporaryHead(handle)
            ?? throw new InvalidDataException("Current personality temporary head is unknown.");
        while (node.Identity.HasValue)
        {
            PersonalityReference payload = state.ReadSkillTemporaryPayload(node)
                ?? throw new InvalidDataException("Current personality temporary payload is unknown.");
            if (payload == personality) return true;
            node = state.ReadHexListNodeNext(node) ?? throw new InvalidDataException("Current personality temporary next is unknown.");
        }
        return false;
    }
    private static void ClearTraits(int receiver, IPersonSkillPostLoadState state)
    {
        for (int index = 0; index < 9; index++)
        {
            ushort old = Known(state.ReadPersonTraitSlot150(receiver, index), "trait before clear150");
            state.WritePersonTraitSlot150(receiver, index, 0);
            if (old == 0) continue;
            ushort mask = Known(state.ReadPersonTraitMask1BC(receiver), "current trait mask1BC");
            state.WritePersonTraitMask1BC(receiver, (ushort)(mask & ~(1 << index)));
            state.WritePersonTraitExtra1BE(receiver, index, 0);
        }
    }
    private static bool Custom(ushort id) => unchecked((uint)(id - 5101)) <= 99;
    private static bool Bandit(ushort id) => unchecked((uint)(id - 4101)) <= 99;
    private static void Enter(IPersonSkillPostLoadState state)
    { if (Known(state.ReadRegistryLockEnabled(), "skill lock flag")) state.EnterRegistryLock(); }
    private static void Exit(IPersonSkillPostLoadState state)
    { if (Known(state.ReadRegistryLockEnabled(), "fresh skill unlock flag")) state.ExitRegistryLock(); }
    private static T Known<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current {field} is unknown.");
}
