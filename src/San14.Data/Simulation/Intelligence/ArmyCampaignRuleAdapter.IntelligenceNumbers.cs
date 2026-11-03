using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // The normal CE command choices and costs, extracted offline from the
    // ordinary intelligence source table. Runtime saved parameters may override.
    internal static int IntelligenceParameter(GameState state,string name) => StrategicParameter(state,
        "intelligence."+name,name switch
        {
            "home-book" or "away-book" or "inspect-book" => 1,
            //Cached TC source491C0/491B8/49204. The same named rule bank
            //as the existing intelligence costs accepts saved overrides.
            "minimum-targets" or "inspect-minimum-executors" or "inspect-minimum-cities" => 1,
            "away-gold-5"=>600,"away-gold-10"=>800,"away-gold-15"=>1000,
            "away-gain-5"=>6,"away-gain-10"=>10,"away-gain-15"=>20,
            "away-gain-cap"=>15000,"away-activity"=>2,
            "inspect-gold-10"=>50,"inspect-gold-20"=>100,"inspect-gold-30"=>150,
            "inspect-military-10"=>100,"inspect-military-20"=>200,"inspect-military-30"=>300,
            _=>throw IntelligenceMissing("parameter."+name)
        });

    internal int ReadIntelligenceAwayGainCurrent(GameState state,SourceWorldContext current,int ownForce,
        int targetForce,int personPhysical,int duration,bool uncapped=false)
    {
        int basis=ReadHireAssociationGain(state,current,new(ownForce),new(targetForce),personPhysical,
            IntelligenceParameter(state,"away-activity"),includePanel:false,capToRemaining:false);
        int gain=unchecked(basis*IntelligenceParameter(state,"away-gain-"+duration));
        return uncapped?gain:Math.Min(gain,IntelligenceParameter(state,"away-gain-cap"));
    }

    internal static T IntelligenceNeed<T>(T? value,string name) where T:struct=>value??throw IntelligenceMissing(name);
    internal static InvalidDataException IntelligenceMissing(string name)=>new("intelligence."+name);
}
