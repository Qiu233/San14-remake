using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class CityInformationCurrent
    {
        private readonly Dictionary<int,CityDetailEconomyPublication> economy = [];
        private PreparedCell EconomyCell(City city,int column) => column switch
        {
            19 or 21 => MakeCell(() => DutyName(city,column == 19),
                () => PersonKey(Person(Duty(city,column == 19)))),
            20 or >= 24 and <= 27 => MakeCell(() => Special(city) ? Data(1,0x353) : Number(Prediction(city,column)),
                () => Special(city) ? new NumberKey(0,true) : new NumberKey(Prediction(city,column))),
            28 => MakeCell(() => Special(city) ? Data(1,0x353) : Number(MerchantPrice(city)),
                () => Special(city) ? new NumberKey(0,true) : new NumberKey(MerchantPrice(city))),
            29 => MakeCell(() => Data(1,Merchant(city) ? 0x338 : 0x339), () => new NumberKey(Merchant(city) ? 1 : 0)),
            _ => throw Missing("economy_column."+column)
        };

        private CityDetailEconomyPublication Economy(City city)
        {
            if (economy.TryGetValue(city.Id,out var saved)) return saved;
            return economy[city.Id] = rules.ReadCityDetailEconomyCurrent(state,Current,city.PhysicalSlot,reportContext:false);
        }
        private int Prediction(City city,int column)
        {
            int node = column switch {24=>100,25=>105,26=>116,27=>121,20=>134,_=>throw Missing("prediction_column")};
            var values = Economy(city);
            if (values.NumericValues.TryGetValue(node,out int value)) return value;
            throw new InvalidDataException(values.UnresolvedFields.GetValueOrDefault(node) ?? Missing("prediction_current_value").Message);
        }
        private ushort Duty(City city,bool recruiter) => Need(recruiter
            ? Current.ReadAssignmentCityCA(city.PhysicalSlot) : Current.ReadAssignmentCityCC(city.PhysicalSlot),
            recruiter ? "RecruiterCA" : "TrainerCC");
        private string DutyName(City city,bool recruiter)
        {
            if (Need(Current.ReadBaseObjectLink(city.Receiver),"City4E") >= 52) return "";
            ushort raw = Duty(city,recruiter);
            return raw == 0 ? "" : PersonText(Person(raw));
        }
        private static int MerchantPrice(City city) => Need(city.Owner.MerchantFoodPerTenGold,"CityC8_merchant_price");
        private bool Merchant(City city)
        {
            if (Special(city)) return false;
            return (Need(state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(city.PhysicalSlot)?.C9,"CityC9_merchant")&1) != 0;
        }
    }
}
