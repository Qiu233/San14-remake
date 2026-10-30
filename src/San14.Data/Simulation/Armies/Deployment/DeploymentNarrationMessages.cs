using San14.Data.Formats;

namespace San14.Data;

internal sealed record DeploymentNarrationText(int MessageId, int VariantIndex,
    string Text, IReadOnlyList<string> Pages);

/// <summary>The established ordinary-deployment consumer of the shared finite
/// resource dialect. Its actual leaf Choice3 remains the public variant.</summary>
internal sealed class DeploymentNarrationMessages(MessageDocument messages)
{
    public DeploymentNarrationText Resolve(INarrationCurrentContext people,
        Func<int,int> presentationRandom, bool executeVoiceRequest = false)
    {
        var result = new NarrationResourceMessages(messages).Resolve(people, presentationRandom,
            NarrationResourceFamily.Deployment,executeVoiceRequest);
        return ReadResult(result);
    }

    public DeploymentNarrationText ResolveGeneralUnitTarget(INarrationCurrentContext people,
        Func<int,int> presentationRandom, bool executeVoiceRequest = false) => ReadResult(
            new NarrationResourceMessages(messages).ResolveGeneralUnitTarget(people,presentationRandom,executeVoiceRequest));

    private static DeploymentNarrationText ReadResult(NarrationResourceText result)
    {
        var selected = result.Choices.Where(choice => choice.MessageId == result.MessageId && choice.Arity == 3).ToArray();
        if (selected.Length != 1) throw new InvalidDataException("deployment.narration.empty_or_unproved_variant");
        return new(result.MessageId, selected[0].SelectedIndex, result.Text, result.Pages);
    }
}
