using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    private sealed class PersonalityVector
    {
        public List<int> Values = [];
        public int Capacity;
    }
    private readonly Dictionary<int,PersonalityVector> _personalityVectors = [];
    // Optional current allocation result. Null means the independent managed
    // allocator owns reservation; a supplied unknown/failure is not success.
    public Func<int,int,bool?>? PersonalityVectorReservation { get; set; }

    /// <summary>Explicit successful normal construction of356 trait objects.
    /// Pointer allocation remains a separate operation.</summary>
    public void InitializePersonalityStorage()
    {
        for (int physical=0;physical<S14PersonalityReader.Count;physical++)
        {
            for (int offset=8;offset<12;offset++)SetPersonalityByte(physical,offset,0);
            ResetPersonality(physical);
        }
    }
    public void ResetPersonality(int physical)
    {
        Check(physical,S14PersonalityReader.Count,"personality reset receiver");
        foreach (S14PersonalityField field in S14PersonalityReader.FixedFields)
            for (int index=0;index<field.Bytes;index++)SetPersonalityByte(physical,field.ObjectOffset+index,0);
        //226CE0 clears the length, releases its old buffer and reserves1.
        //These are explicit owned storage effects, not inferred source values.
        var vector=new PersonalityVector();
        _personalityVectors[physical]=vector;
        ReservePersonalityVector(physical,vector,1);
    }
    public IReadOnlyList<int>? ReadPersonalitySourceCodes(int physical)
    {
        Check(physical,S14PersonalityReader.Count,"personality vector receiver");
        return _personalityVectors.TryGetValue(physical,out PersonalityVector? vector) ? vector.Values.AsReadOnly() : null;
    }
    public int? ReadPersonalityVectorCapacity(int physical)
    {
        Check(physical,S14PersonalityReader.Count,"personality vector receiver");
        return _personalityVectors.TryGetValue(physical,out PersonalityVector? vector) ? vector.Capacity : null;
    }
    private void AppendPersonalitySourceCode(int physical,byte sourceValue)
    {
        PersonalityVector vector=_personalityVectors.GetValueOrDefault(physical)
            ?? throw new InvalidDataException("Current personality C8 vector is unknown; construction/reset must precede fixed loading.");
        if (vector.Values.Count==vector.Capacity)
        {
            int required=checked(vector.Values.Count+1);
            int grown=checked(vector.Capacity+vector.Capacity/2);
            ReservePersonalityVector(physical,vector,Math.Max(required,grown));
        }
        //DA30 sourceDX, shiftR8W=1, physical-indexR9W. Its caller retains low16.
        ushort mixed=(ushort)(sourceValue^(ushort)physical);
        int encoded=(ushort)((mixed<<15)|(mixed>>1));
        vector.Values.Add(encoded);
    }
    private void ReservePersonalityVector(int physical,PersonalityVector vector,int capacity)
    {
        if (PersonalityVectorReservation is not null)
        {
            bool result=PersonalityVectorReservation(physical,capacity)
                ?? throw new InvalidDataException("Current personality vector reservation result is unknown.");
            if (!result)throw new InvalidDataException("Current personality vector reservation failed.");
        }
        //Allocation/copy finishes before metadata changes. Existing entries and
        //the old logical capacity survive an allocation failure.
        var replacement=new List<int>(capacity);
        replacement.AddRange(vector.Values);
        vector.Capacity=capacity;
        vector.Values=replacement;
    }
}
