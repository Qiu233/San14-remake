namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Adopts the owned result of an actual tutorial paragraph writer.
    /// Text and control-only updates retain their separate overlay path.</summary>
    public bool TryAdoptTutorialBusinessWorld(GameState exactOwner, GameState preparedWorld)
    {
        if (HasPendingOperation || !ReferenceEquals(State, exactOwner) || preparedWorld is null ||
            State.TutorialLessonProgress is not { } previous || preparedWorld.TutorialLessonProgress is not { } next ||
            previous.CourseIndex != next.CourseIndex) return false;
        try { CheckAuxiliaryTransition(State, preparedWorld); }
        catch (InvalidDataException) { return false; }
        State = preparedWorld;
        return true;
    }
}
