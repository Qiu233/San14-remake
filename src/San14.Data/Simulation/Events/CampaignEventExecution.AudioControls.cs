using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>An ordered ordinary-event sound control. The semantic step is
/// executed by the actual invocation's playback owner, after prior sounds.
/// No player slot, device handle or staging-time activity snapshot is saved.</summary>
public sealed record CampaignEventAudioControl(int OperationIndex, int SourceOffset, int Code,
    StoryAudio Step) : CampaignEventCommand(OperationIndex, SourceOffset, Code);

/// <summary>The four native ordinary-event SE control wrappers. The existing
/// immutable SE map remains owned by CampaignNormalEventAudioInputs. BGM,
/// voice, command sound, global user volume and resource decoding are not
/// reimplemented here.</summary>
public static class CampaignStorySoundControlSource
{
    /// <summary>Only these mapped effect identities participate in opcode
    /// 325. The playback owner tests them against its actual active slots;
    /// the collection is not a list of raw one-based event SE operands.</summary>
    public static ImmutableArray<int> VolumeEffectIds { get; } =
    [
        85, 86, 87, 88, 95, 96, 98, 99, 101, 102, 103, 105,
        108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118,
        119, 125, 126, 127, 128, 130, 135, 137, 138
    ];

    public static CampaignEventAudioControl Create(S14ConditionInstruction operation,
        IS14AudioEventExecutionState? audioInputs)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.RawArguments.Count != 8)
            throw new InvalidDataException("story.audio-control.operands");
        var arguments = operation.RawArguments;
        StoryAudio step;
        switch (operation.Code)
        {
            case 324:
                //Signed raw<=0 always stops all; raw>96 still sends the
                //actual invalid mapped identity -1 to the sound manager.
                step = arguments[0] <= 0 ? StopAll() : new(StoryAudioKind.FadeInSoundIfNotSkipped,
                    Map(arguments[0]), 0, (float)arguments[1], 0);
                break;
            case 325:
                step = new(StoryAudioKind.ChangeSelectedSoundVolumes, 0, 0, (float)arguments[0], 0);
                break;
            case 328:
            case 329:
                //The original subtract/range comparison is unsigned.
                //Every raw operand outside1..96, including negatives and
                //int.MinValue, selects the complete normal SE stop route.
                if (unchecked((uint)arguments[0] - 1u) >= 96u) step = StopAll();
                else step = new(operation.Code == 328 ? StoryAudioKind.FadeOutMatchingSounds
                    : StoryAudioKind.StopMatchingSounds, Map(arguments[0]), 0,
                    operation.Code == 328 ? (float)arguments[1] : 0, 0);
                break;
            default: throw new InvalidDataException("story.audio-control.opcode");
        }
        return new(operation.Index, operation.Offset, operation.Code, step);

        StoryAudio StopAll() => new(StoryAudioKind.StopAllSounds, 0, 0, 0, 0);
        int Map(int raw)
        {
            int index = raw - 1;
            if ((uint)index >= 96u) return -1;
            return audioInputs?.ReadSeMapEntry(index)
                ?? throw new InvalidDataException("story.audio-control.SE_mapping");
        }
    }
}

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        private void StageAudioControl(S14ConditionInstruction operation) =>
            Commands.Add(CampaignStorySoundControlSource.Create(operation, audioInputs));
    }
}
