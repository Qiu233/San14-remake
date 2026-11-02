using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    //Constructor publication fills absent observations only. Dictionary presence
    //preserves an explicit unknown separately from an unbound normal-load field.
    internal void LendNormalLoadArmyCommander(int physical) => _armyCommanderIds.TryAdd(physical,0);
    internal void LendNormalLoadCityAssignments(int city,ushort? recruiter,ushort? trainer)
    { _assignmentCityCA.TryAdd(city,recruiter);_assignmentCityCC.TryAdd(city,trainer); }
    internal void LendNormalLoadAreaAssignment(int area,ushort? person) => _assignmentArea36.TryAdd(area,person);
    internal void LendNormalLoadForceAssociation(int force,int index) => _associationPersons.TryAdd((force,index),0);
    internal int? ReadNormalLoadAssignmentCount(int person) =>
        _assignmentPersonAreas.TryGetValue(person,out var vector) ? vector?.ActiveCount1E8 : 0;
    internal void LendNormalLoadItem(int item,ItemStatusKnowledge? status,ushort? holder)
    { _registryItemStatuses.TryAdd(item,status);_captureItemOwners4A.TryAdd(item,holder); }
    internal bool HasNormalLoadRegistryObservation(RegistryKind kind) => RegistryConfigured(kind);
}
