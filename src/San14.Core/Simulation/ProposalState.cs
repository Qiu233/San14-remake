namespace San14.Core;

public enum ProposalKind
{
    None = 0, DevelopmentUp = 1, SecurityUp = 2, Investment = 3,
    Conscription = 4, SpecialTraining = 5, Exploit = 6, Requisition = 7,
    RepairEndurance = 8, SecurityDown = 9, MoraleUp = 10, MoraleDown = 11,
    RecoverWoundedSoldiers = 12, BuyFood = 13, SellFood = 14,
    RecommendPerson = 15, Banquet = 16, CurePerson = 17, FreePrisoner = 18,
    BefriendAlly = 19, StunEnemy = 20, ConfuseEnemy = 21, BurnEnemy = 22,
    ExtinguishFriendlyFire = 23, FillTroops = 24
}

public enum ProposalStatus { Empty = 0, Offered = 1, Pending = 2, Executed = 3, Failed = 4 }

/// <summary>The five ordered arguments of a generated proposal. Each kind's
/// rules interpret these positions; identities and quantities must not be
/// inferred from a different handler's argument meanings.</summary>
public readonly record struct ProposalArguments(int Parameter0, int Parameter1,
    int Parameter2, int Parameter3, int Parameter4)
{
    public int this[int index] => index switch
    {
        0 => Parameter0, 1 => Parameter1, 2 => Parameter2,
        3 => Parameter3, 4 => Parameter4,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

/// <summary>A real generated offer and its subsequent adoption/execution state.
/// Browsing or selecting the offer does not change its status.</summary>
public sealed record ProposalEntry(int SlotId, ProposalKind Kind,
    ProposalStatus Status, int ProposerPersonId, ProposalArguments Arguments);

/// <summary>The world's proposal table. Slot zero is an unusable sentinel;
/// there are thirty usable slots. Null world ownership means unresolved data,
/// independently of a known empty board.</summary>
public sealed record ProposalBoard(ProposalEntry[] Slots)
{
    public const int SlotCount = 31;

    public static ProposalBoard Empty() => new(Enumerable.Range(0, SlotCount)
        .Select(slot => new ProposalEntry(slot, ProposalKind.None,
            ProposalStatus.Empty, 0, default)).ToArray());

    public ProposalEntry Read(int slotId)
    {
        if (slotId is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slotId));
        return Slots[slotId];
    }

    public ProposalBoard Copy() => new((ProposalEntry[])Slots.Clone());
}

public sealed partial record GameState
{
    public ProposalBoard? Proposals { get; init; }
}

/// <summary>Structural persistence checks, invoked with the ordinary offline
/// world/save validation rather than on rendering or parameter queries.</summary>
internal static class ProposalStateValidation
{
    internal static void Validate(ProposalBoard? board)
    {
        if (board is null) return;
        if (board.Slots is null || board.Slots.Length != ProposalBoard.SlotCount)
            throw new InvalidDataException("A proposal board must retain all thirty-one slots.");
        for (int slot = 0; slot < board.Slots.Length; slot++)
        {
            ProposalEntry entry = board.Slots[slot];
            if (entry is null || entry.SlotId != slot ||
                !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Status) ||
                entry.ProposerPersonId < 0)
                throw new InvalidDataException("A saved proposal slot is invalid.");
        }
        ProposalEntry sentinel = board.Slots[0];
        if (sentinel.Kind != ProposalKind.None || sentinel.Status != ProposalStatus.Empty)
            throw new InvalidDataException("The proposal sentinel cannot contain an active offer.");
    }
}
