using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyGraphicsAction(int Category, int RawActionId,
    LoadedArmyAnimationAction Animation, float Duration, float StartFrame, float DeclaredLength,
    float EndOffset, float Interpolation, float LoopDeclaration, uint ContinuationAction, uint ContinuationObject,
    uint PlayerFlags, int AdditionalAnimationCount);

/// <summary>All declared graphics actions for the selected category resources.
/// Raw action identities, declaration order and gameplay activities are separate.</summary>
public sealed class ArmyGraphicsActions
{
    public IReadOnlyDictionary<(int Category, int RawAction), ArmyGraphicsAction> Actions { get; }

    public ArmyGraphicsActions(GameResourceRepository repository) : this(repository, Enumerable.Range(0, 4)) { }

    public ArmyGraphicsActions(GameResourceRepository repository, IEnumerable<int> categories)
        : this(repository, CeArmyModels.LoadCatalog(repository), categories) { }

    public ArmyGraphicsActions(GameResourceRepository repository, LoadedArmyCatalog catalog,
        IEnumerable<int> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        var result = new Dictionary<(int, int), ArmyGraphicsAction>();
        foreach (int category in categories)
        {
            foreach (var animation in ArmyAnimationResources.LoadActions(repository, catalog, category))
            {
                if (animation.AnimationData.Count != 1)
                    throw new NotSupportedException("Army graphics action requires its separate multi-clip player consumer.");
                int raw = checked((int)animation.RawActionId);
                var source = animation.ActionObject;
                var clip = animation.AnimationData[0].Clip;
                var extra = source.Properties.Single(p => p.NameHash == 0xE1773A02);
                var action = new ArmyGraphicsAction(category, raw, animation, clip.FrameCount / clip.FrameRate,
                    Float(source, 0x750E63CB), Float(source, 0xF977F44D), Float(source, 0xD3F46C71),
                    Float(source, 0x4BEF4ECC), Float(source, 0xC2AFEDCF), Word(source, 0xA26BA507),
                    Word(source, 0xA87D31E6), animation.PlayerFlags, extra.ElementCount);
                if (!result.TryAdd((category, raw), action))
                    throw new InvalidDataException($"Army category {category} declares duplicate action identity {raw}.");
            }
            if (!result.ContainsKey((category, 0)))
                throw new InvalidDataException($"Army category {category} has no declared default action zero.");
        }
        Actions = new ReadOnlyDictionary<(int, int), ArmyGraphicsAction>(result);
    }

    private static uint Word(DokObject source, uint key)
    {
        var property = source.Properties.Single(p => p.NameHash == key);
        if (property.ElementSize != 4 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException($"Army action property {key:X8} is not a scalar word.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }

    private static float Float(DokObject source, uint key) => BitConverter.UInt32BitsToSingle(Word(source, key));
}
