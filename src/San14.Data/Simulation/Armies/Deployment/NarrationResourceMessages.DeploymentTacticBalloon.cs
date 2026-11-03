namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveDeploymentTacticDisabledReason(int message)
    {
        if (message is < 0x5688 or > 0x568E) throw Failure("deployment_tactic_balloon_reason_root");
        _prefix = "deployment.tactic_balloon.reason";
        bool previous = _report; _report = true;
        try
        {
            // Consume the complete actual no-argument Game message program.
            // LF survives; unexpected argument/random/voice branches fail.
            return ExecuteMessage(DeploymentTacticBalloonArguments.Instance,
                _ => throw Failure("random_argument"), message, Get(message, true),
                _ => false, true, null, _ => false,
                (group, code) => throw Failure($"unresolved_escape_{group:X2}_{code:X2}"), true).Text;
        }
        finally { _report = previous; }
    }

    private sealed class DeploymentTacticBalloonArguments : INarrationCurrentContext
    {
        internal static readonly DeploymentTacticBalloonArguments Instance = new();
        private static InvalidDataException Missing() => new("deployment.tactic_balloon.reason.argument");
        public int PersonValue(int argument, int field) => throw Missing();
        public string PersonText(int argument, int field) => throw Missing();
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing();
        public string AreaText(int argument, int field) => throw Missing();
    }
}
