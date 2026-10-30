namespace San14.Data;

// Semantic course data extracted offline from the eight actual initializers.
internal static class TutorialDefinitions
{
    internal static readonly int[] ButtonMountNodes = [3,6,9,12,15,18,21,24,27];
    internal static readonly int[][] Rewards =
    [
        [2004,2006,2018],
        [2031,2032,2041],
        [2014,2015,2028],
        [2003,2011,2045],
        [2073,2069,0],
        [2076,2075,0],
        [2083,2085,2086],
        [2084,2087,2088],
    ];
    internal static readonly string[][] Paragraphs =
    [
        ["Opening","SummaryCity","SelectDomestic","SelectDomesticArea","SelectDomesticPerson","SelectDomesticPri","DomesticDecide","Progress","ReportAction","ReportIncome1","CityDomesticIntro","CityDomesticSelectCommand","CityDomesticSetup","CityDomesticProgress","Proposal","ProposalExplain","ProposalOfficer","ProposalExit","ProposalProgress","ReportIncome","Ending","Exit"],
        ["Opening","SelectCity","UnitMakeRoot","UnitMakeSelectPerson","UnitMakeSelectPersonUpdate","UnitMakeSelectFormation","UnitMakeSelectEtc","UnitMakeDecide","UnitTarget","UnitTargetCapital","ConquerCapital","ConquerArea","Ending","Exit"],
        ["Opening","VSKoukinReady","Departure","DepartureUnitMake","DepartureUnitTargetKanu","DepartureUnitTargetChouhi","AreaSupport","CutSupply","AttackKangai","Tactics","VSKoukinFreePlay","Ending","Exit"],
        ["Opening","Government","GovernmentExplain","GovernmentSelectStrategist","GovernmentAutoPolicy","GovernmentDecide","Progress","GovernmentEffect","BuildExplain","Departure","BuildWait","AttackHeigen","Fire","Nanpi","Ending","Exit"],
        ["Opening","MakeAlliSelectCmd","MakeAlliSelectForce","MakeAlliSelectForceUpdate","MakeAlliSelectPerson","MakeAlliSelectPersonUpdate","MakeAlliSelectTerm","MakeAlliSelectTermUpdate","MakeAlliSelectArea","MakeAlliDecide","MakeAlliProgress","MakeAlliReport","FacillDestructionCmd","FacillDestructionTarget","FacillDestructionPerson","FacillDestructionPersonUpdate","FacillDestructionDecide","FacillDestructionProgress","FacillDestructionReport","ConstructionPre","Construction","ConstructionTarget00","ConstructionTarget01","ConstructionTarget02","BuildWait","NewTerrain","FakeNewsPre","FakeNewsToKangai","FakeNewsWait","VSKoukinFreePlay","Ending","Exit"],
        ["Opening","SelectCity","CityInfoExp","SelectGoodwill","GoodwillForce","GoodwillForceUpdate","GoodwillPerson","GoodwillPersonUpdate","GoodwillGold","GoodwillDecide","GoodwillProgress","GoodwillReport","AdvantageCmd","Advantage","CaravanSelectCmd","CaravanSelectCountry","CaravanSelectCountryUpdate","CaravanSelectPerson","CaravanSelectPersonUpdate","CaravanDecide","CaravanProgress","CaravanArival","CaravanReport","Ending","Exit"],
        ["Opening","SpyHomeSelectCmd","SpyHomeSelectForce","SpyHomeSelectPerson","SpyHomeSelectPersonUpdate","SpyHomeDecide","SpyHomeDecide2nd","SpyAwaySelectCmd","SpyAwaySelectForce","SpyAwaySelectForceUpdate","SpyAwaySelectPerson","SpyAwaySelectPersonUpdate","SpyAwayDecide","SpySystemProgress","SpySystemReport","MakeAlliSelectCmd","MakeAlliSelectForce","MakeAlliSelectForceUpdate","MakeAlliSelectPerson","MakeAlliSelectPersonUpdate","MakeAlliDecide","MakeAlliProgress","MakeAlliReport","PlacateSelectCmd","PlacateSelectArea","PlacateSelectAreaUpdate","PlacateSelectPerson","PlacateSelectPersonUpdate","PlacateDecide","PlacateProgress","PlacateReport","AttackKanKyoShi","AttackFreePlay","AttackReturn","DestinyReportAction","Ending","Exit"],
        ["Opening","CreditInfo","CreditRankup","SelectDomestic","SelectDomesticArea","SelectDomesticPerson","SelectDomesticPri","SelectDomesticArea","SelectDomesticPerson","SelectDomesticPri","DomesticDecide","SurrenderInfo","DoctrinePanelPrepare","DoctrinePanel","ScenerySearch","SceneryInvote","Ending","Exit"],
    ];
}
