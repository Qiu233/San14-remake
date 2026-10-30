namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //Actual1B16D0 facility/trap actions7/8/9/32/41/44/45. Both locales use these finite
    //root/reference records and the shared person-address family. Ordinary
    //parsing retains source predicates, choices and Function5 Voice5/6/7.
    private static bool FacilityArmySpeechRoot(int root)=>root is 0xC472 or 0xC7EA or 0xC503 or 0xC504 or 0xC473 or 0xC7EB or
        0xC613 or 0xC61D or 0xC639 or 0xC66E;
    private static bool FacilityArmySpeechMessage(int id)=>id is >=0xC472 and <=0xC47E or 0xC503 or 0xC504 or
        >=0xC613 and <=0xC62A or >=0xC639 and <=0xC67D or >=0xC7EA and <=0xC7F6 or 0x009A or 0x009C;
}
