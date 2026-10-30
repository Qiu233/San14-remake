namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveStoryStateControl(INarrationCurrentContext context, Func<int, int> draw, int root)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(draw);
        if (root is not (0x328C or 0x3291)) throw Failure("story_state_control_root");
        _prefix = "story.state-control.text";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, draw, root, Get(root), _ => false, true, null,
                StoryStateControlMessages.Contains).Text;
        }
        finally { _report = previous; }
    }

    //Complete installed TC/SC J/C closure: both real source roots use only
    //Force0.P4 and String0, with no nested message, voice, random or escape.
    private static readonly HashSet<int> StoryStateControlMessages = [0x328C, 0x3291];
}
