using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

internal sealed partial class NewGameOfficerListInputs :
    IOfficerCommandCapacityInputs, IOfficerSalaryInputs, IPersonForceAdvantageInputs
{
    private readonly LoadedGrowthModifierDefinitions growth;
    private readonly int? growthMode;

    private static int? ReadGrowthMode(LoadedScenario scenario) =>
        scenario.Snapshot.WorldParameters?.RawBytes is {Length:>=51} bytes
            ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(47,4)) : null;

    int? IOfficerCommandCapacityInputs.ReadForceRank(int force) =>
        state.Forces.TryGetValue(force,out var owner) ? owner.RankId :
            (uint)force<scenario.Snapshot.Forces.Count&&scenario.Snapshot.Forces[force].Slot==force
                ? scenario.Snapshot.Forces[force].RankId : null;
    int? IOfficerCommandCapacityInputs.ReadOffice(int person) => Office(person);
    int? IOfficerSalaryInputs.ReadOffice(int person) => Office(person);
    int? IOfficerCommandCapacityInputs.ReadOfficeCapacity(int office) =>
        ReadOfficeDefinition(office)?.CommandLimit;
    int? IOfficerSalaryInputs.ReadOfficeGuard(int office) => ReadOfficeDefinition(office)?.Raw38;
    int? IOfficerCommandCapacityInputs.ReadParameter(string key) => Parameter(key);
    int? IOfficerSalaryInputs.ReadParameter(string key) => Parameter(key);
    bool? IOfficerSalaryInputs.OfficeValid(int office) => OfficeValid(office);
    bool? IOfficerCommandCapacityInputs.ReadPolicyEnabled(int force,int effect) => PolicyEnabled(force,effect);
    int? IOfficerCommandCapacityInputs.ReadPolicyEffect(ForceReference force,int effect) =>
        current.BaseForceModifierReader?.Invoke(force,effect);
    OfficerCommandBonusResult IOfficerCommandCapacityInputs.ReadCreditBonus(int physical)
    {
        try{return new(CreditBonus(physical,1),null);}
        catch(InvalidDataException error){return new(null,error.Message);}
    }
    private Formats.S14OfficeDefinition? ReadOfficeDefinition(int office) =>
        scenario.Snapshot.RankParameters?.Offices is {} rows && (uint)office<rows.Count && rows[office].Id==office
            ? rows[office] : null;

    int? IOfficerSalaryInputs.ReadForceAdvantage(ForceReference force,int effect)
    {
        if(!current.HasNormalLoadRegistryObservation(RegistryKind.City))
        {
            var pool=current.ReadOwnedCurrentListPool() ?? new WorldIndexContext(current);
            current.SetRegistryDescriptor(RegistryKind.City,new(pool.AllocateDescriptor().Identity));
            for(int slot=0;slot<52;slot++)
                _=ObjectRegistration.Register(current.ReadRegistryTableReference(RegistryKind.City,slot),current);
        }
        var result=PersonReturnPathSource.ReadForceAdvantage(state,current,force,effect,this);
        return result.Value ?? throw new InvalidDataException(result.UnresolvedOwner??Missing("force-advantage").Message);
    }
    int? IPersonForceAdvantageInputs.Mode => growthMode;
    byte? IPersonForceAdvantageInputs.StateAdvantage(int province) =>
        (uint)province<growth.Catalog.States.Count&&growth.Catalog.States[province].Slot==province
            ? growth.Catalog.States[province].RawAdvantage30 : null;
    byte? IPersonForceAdvantageInputs.RequiredCities(int advantage) => Advantage(advantage)?.RequiredCitiesB3;
    byte? IPersonForceAdvantageInputs.Effect(int advantage,int index) =>
        Advantage(advantage) is {} row && (uint)index<row.EffectSlotsB4.Length ? row.EffectSlotsB4[index] : null;
    byte? IPersonForceAdvantageInputs.Magnitude(int advantage,int index) =>
        Advantage(advantage) is {} row && (uint)index<row.MagnitudesB6.Length ? row.MagnitudesB6[index] : null;
    byte? IPersonForceAdvantageInputs.CityProvince(int city) =>
        (uint)city<scenario.Snapshot.Cities.Count&&scenario.Snapshot.Cities[city].Slot==city
            ? scenario.Snapshot.Cities[city].GroupCode : null;
    private Formats.S14GrowthAdvantageDefinition? Advantage(int index) =>
        (uint)index<growth.Catalog.Advantages.Count&&growth.Catalog.Advantages[index].Slot==index
            ? growth.Catalog.Advantages[index] : null;
}
