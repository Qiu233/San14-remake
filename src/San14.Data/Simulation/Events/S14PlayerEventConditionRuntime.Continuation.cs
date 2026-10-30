namespace San14.Data.Formats;

public sealed partial class S14PlayerEventConditionRuntime
{
    internal (int[] People, int?[] Cities, int?[] Forces, bool Blocked) CaptureStorySelections()
        => (_selectedPersons.Select(person => person.PhysicalSlot).ToArray(),
            _selectedCities.Select(city => city.PhysicalSlot).ToArray(),
            _targetForces.Select(force => force?.PhysicalSlot).ToArray(), ConditionBlocked);

    internal void RestoreStorySelections(IReadOnlyList<int> people, IReadOnlyList<int?> cities,
        IReadOnlyList<int?> forces, bool blocked)
    {
        _selectedPersons.Clear();
        _selectedPersons.AddRange(people.Select(person => new S14EventConditionPersonReference(person)));
        _selectedCities.Clear();
        _selectedCities.AddRange(cities.Select(city => new S14EventConditionCityPointer(city)));
        for (int index = 0; index < _targetForces.Length; index++)
            _targetForces[index] = forces[index] is int force ? new(force) : null;
        //All three nested condition blockers are monotone during one invocation.
        //The shared outer latch preserves their exact combined result on resume.
        _blocked = blocked;
    }
}
