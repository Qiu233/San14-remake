using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public static class RegisteredOfficerEditing
{
    /// <summary>The actual new-record producer. It does not register the
    /// draft or alter a campaign before the editor's confirmation.</summary>
    public static RegisteredOfficer CreateDraft(int id)
    {
        if (id is < 3001 or > 3150) throw new ArgumentOutOfRangeException(nameof(id));
        return new(id)
        {
            PortraitId = 2000, Sex = 0, BirthYear = 170, DeathYear = 239,
            FamilyId = 0, FatherId = 0, MotherId = 0, SpouseId = 0, SwornBrotherId = 0,
            CompatibilityPersonId = 0, Compatibility = 0,
            LikePersonIds = [0, 0, 0, 0, 0, 0, 0, 0], DislikePersonIds = [0, 0, 0, 0, 0, 0, 0, 0],
            Leadership = 50, Strength = 50, Intelligence = 50, Politics = 50, Charm = 50,
            TraitIds = [0, 0, 0, 0, 0, 0, 0, 0, 0], FormationIds = [1],
            TacticIds = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            DoctrineId = 1, PolicyId = 1, PolicyBaseCost = 1, VoiceType = 9
        };
    }
}
