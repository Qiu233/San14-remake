namespace San14.Data;

/// <summary>One actually reached Base-primary activation's B8/C8 aggregation.
/// Keys identify actual receivers, not a descriptor ID, Hex or Object4E.
/// This temporary ledger owns no world state, raw address or presentation ACK.</summary>
internal sealed class AutomaticBaseTacticDamageLedger
{
    private readonly Dictionary<BaseReference, int> requestedByReceiver = [];
    private readonly Dictionary<ArmyUnitReference, int> requestedByArmy = [];

    internal void AccumulateArmy(ArmyUnitReference receiver, int requestedLoss)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        if (receiver.PhysicalSlot is not (>= 1 and <= 500))
            throw new InvalidDataException("Automatic Army damage needs an actual typed receiver.");
        requestedByArmy.TryGetValue(receiver, out int previous);
        requestedByArmy[receiver] = unchecked(previous + requestedLoss);
    }

    /// <summary>176C10 records the full signed request after its immediate
    /// application/defer and result marker. Repeated nodes add unchecked i32;
    /// they do not add applied casualty amounts or truncate each request.</summary>
    internal void Accumulate(BaseReference receiver, int requestedLoss)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        if (receiver.PhysicalSlot is null || !Enum.IsDefined(receiver.Kind))
            throw new InvalidDataException("Automatic Base damage needs an actual typed receiver.");
        requestedByReceiver.TryGetValue(receiver, out int previous);
        requestedByReceiver[receiver] = unchecked(previous + requestedLoss);
    }

    /// <summary>The complete16FE60 B8 traversal followed by C8 consumption.
    /// Native order matters only when multiple ID-matching receivers have
    /// differing lowwords. That missing order is explicit, never guessed from
    /// City/Gate kind, table slot or effect encounter order.</summary>
    internal NormalArmyCombatTacticFrame22Receipt ConsumeFrame22(SourceWorldContext current,
        int primaryKind, ushort primaryNativeId, ushort previousWord22 = 0)
    {
        ArgumentNullException.ThrowIfNull(current);
        ushort after = previousWord22;
        int matches = 0;
        foreach (var node in requestedByArmy)
        {
            bool valid = current.ReadArmyUnitNativeValidity(node.Key)
                ?? throw Missing("current_Army_native_validity");
            if (!valid || primaryKind != 27) continue;
            //22DB70 derives this ID from the actual registered receiver.
            //No Army is created/retired or receiver swapped inside the writer.
            if (primaryNativeId == node.Key.PhysicalSlot)
            {
                after = unchecked((ushort)node.Value);
                matches++;
            }
        }
        bool ambiguousOrder = false;
        foreach (var node in requestedByReceiver)
        {
            bool valid = current.ReadBaseNativeValidity(node.Key)
                ?? throw Missing("current_Base_native_validity");
            if (!valid) continue;
            ushort linked = current.ReadBaseObjectLink(node.Key)
                ?? throw Missing("current_Base4E");
            int decodedId = linked <= 51 ? linked : linked <= 61 ? linked - 51 : 0;
            //The actual source checks only primary kind5/6 plus decoded ID.
            //It deliberately does not recompare decoded City/Gate kind.
            if (primaryKind is not (5 or 6) || primaryNativeId != decodedId) continue;
            ushort word = unchecked((ushort)node.Value);
            if (matches == 0) after = word;
            else if (word != after) ambiguousOrder = true;
            matches++;
        }
        if (ambiguousOrder)
            throw Missing("C8_native_Base_receiver_order");
        return new(previousWord22, after, matches != 0, matches);
    }

    private static InvalidDataException Missing(string owner) =>
        new($"Automatic Base tactic damage ledger is unresolved: {owner}.");
}
