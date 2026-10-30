namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //Complete normal TC/SC reference closure of the four actual source roots.
    //The resource program owns its person/voice predicates and App choices.
    private static bool ArmyCommandSpeechRoot(int root)=>root is 0xC609 or 0xC5F1 or 0xC688 or 0xC67E;
    private static bool ArmyCommandSpeechMessage(int id)=>id is >=0xC5F1 and <=0xC612 or >=0xC67E and <=0xC691;
}
