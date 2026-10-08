namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    //The source Home transfer uses the same current assignment owner as
    //capture. Values and arrays are detached, callbacks use the new context.
    internal void CopyAssignmentInputsTo(OriginalEventInitializationContext draft)
    {
        Copy(_assignmentAdvisors52, draft._assignmentAdvisors52);
        Copy(_assignmentCityCA, draft._assignmentCityCA); Copy(_assignmentCityCC, draft._assignmentCityCC);
        Copy(_assignmentArea36, draft._assignmentArea36); Copy(_assignmentArea72, draft._assignmentArea72);
        Copy(_assignmentArea6D, draft._assignmentArea6D);
        draft._assignmentPersonAreas.Clear();
        foreach (var row in _assignmentPersonAreas)
            draft._assignmentPersonAreas.Add(row.Key, row.Value is null ? null : row.Value with { Values = row.Value.Values.ToArray() });
        draft._assignmentAdvisorsWritten52.Clear(); draft._assignmentAdvisorsWritten52.UnionWith(_assignmentAdvisorsWritten52);
        Copy(_associationPersons, draft._associationPersons); Copy(_associationTargets, draft._associationTargets);
        Copy(_personResetWords, draft._personResetWords); Copy(_personResetBytes, draft._personResetBytes);
        draft.AssignmentAdvisorResetCallbacks = AssignmentAdvisorResetCallbacks;
        draft.AssignmentAreaOfficerRemoval = (area, oldId) =>
        {
            var result = OriginalPersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(draft, area, oldId);
            return result.NativeReturn.HasValue ? result.NativeReturn != 0 : throw new InvalidDataException(result.UnresolvedOwner);
        };
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { target.Clear(); foreach (var row in source) target.Add(row.Key, row.Value); }
    }
    internal void CommitAssignmentCurrentFields(OriginalEventInitializationContext draft) => draft.CopyAssignmentInputsTo(this);
}
