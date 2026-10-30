namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //1B16D0 actions6/28. The reached resource program owns the
    //Personality53/54 special branches, Function5 Voice5 admission and
    //the actual Function3(3) choices; none is replaced by a fixed variant.
    private static bool FireArmySpeechRoot(int root)=>root is 0xC453 or 0xC461 or 0xC7AF;
    private static bool FireArmySpeechMessage(int id)=>id is >=0xC453 and <=0xC471 or 0xC7AF or
        >=0x0098 and <=0x009D or 0x009F or >=0x00A0 and <=0x00A7;
}
