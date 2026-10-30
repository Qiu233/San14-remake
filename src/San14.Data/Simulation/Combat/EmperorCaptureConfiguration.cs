using San14.Core;

namespace San14.Data;

/// <summary>The four independent saved configuration integers. Fresh TC/SC
/// owner initialization is installed only at normal new-world construction;
/// saved/explicit values retain priority and a missing old value stays unknown.</summary>
internal static class EmperorCaptureConfiguration
{
    internal const string NpcReceptionMinimum = "capture-emperor:npc-reception-tendency-minimum";
    internal const string AbolitionWitnessMinimum = "capture-emperor:abolition-witness-tendency-minimum";
    internal const string LoyalOfficerMinimum = "capture-emperor:loyal-officer-tendency-minimum";
    internal const string UnfavorableRulerMaximum = "capture-emperor:unfavorable-ruler-tendency-maximum";

    internal static GameState InstallFresh(GameState state, string language)
    {
        if (language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native || native.Parameters.Language != language ||
            native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language))
            throw new InvalidDataException("Emperor configuration requires its matching fresh TC/SC owner.");

        // Each edition independently has PE-loader initialization of this
        // read-only scalar owner, qualified by its complete owner/noescape
        // evidence and actual CRT object boundaries. These are source fresh
        // initial values, not a claim about any arbitrary later configuration.
        (int Reception, int Witness, int Loyal, int Unfavorable) initial = language switch
        {
            "TC" => (0, 0, 0, 0),
            "SC" => (0, 0, 0, 0),
            _ => throw new InvalidOperationException("Unreachable emperor initializer language.")
        };
        Dictionary<string, int>? values = null;
        void Install(string key, int value)
        {
            if (native.Parameters.Float32Values.ContainsKey(key))
                throw new InvalidDataException("Emperor configuration requires an Int32 value: " + key);
            if (native.Parameters.Int32Values.ContainsKey(key)) return;
            (values ??= new Dictionary<string, int>(native.Parameters.Int32Values, StringComparer.Ordinal))
                .Add(key, value);
        }
        Install(NpcReceptionMinimum, initial.Reception);
        Install(AbolitionWitnessMinimum, initial.Witness);
        Install(LoyalOfficerMinimum, initial.Loyal);
        Install(UnfavorableRulerMaximum, initial.Unfavorable);
        return values is null ? state : state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    internal static AutomaticEmperorCaptureInputs Resolve(GameState state, AutomaticEmperorCaptureInputs? supplied)
        => new(supplied?.NpcReceptionTendencyMinimum ?? Read(state, NpcReceptionMinimum),
            supplied?.AbolitionWitnessTendencyMinimum ?? Read(state, AbolitionWitnessMinimum),
            ResolveForced(state, supplied?.Business));

    internal static StoryForcedEmperorCaptureInputs ResolveForced(GameState state, StoryForcedEmperorCaptureInputs? supplied)
        => new(supplied?.LoyalOfficerTendencyMinimum ?? Read(state, LoyalOfficerMinimum),
            supplied?.UnfavorableRulerTendencyMaximum ?? Read(state, UnfavorableRulerMaximum),
            //History suppression belongs to the actual caller, independently
            //of these numeric rules. Null does not become ordinary-history0.
            supplied?.ReceptionChronicleSuppression);

    private static int? Read(GameState state, string key)
    {
        if (state.NativeArmySimulation?.Parameters is not { Language: "TC" or "SC" } bank ||
            bank.RuleVersion != SimulationRuleIdentity.ForLocale(bank.Language) ||
            bank.Float32Values.ContainsKey(key)) return null;
        return bank.Int32Values.TryGetValue(key, out int value) ? value : null;
    }
}
