namespace ServiceLib.Common;

public sealed record TunDelayStartupOption(bool IsSpecified, int DelaySeconds, string? ParseError)
{
    public static TunDelayStartupOption NotSpecified { get; } = new(false, 0, null);
}

public static class StartupArgumentHelper
{
    public const string TunDelayArgument = "-tundelay";

    public static TunDelayStartupOption ParseTunDelay(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.Equals(TunDelayArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count)
                {
                    return Invalid("The -tundelay option requires a delay in seconds.");
                }

                return ParseSeconds(args[index + 1]);
            }

            var prefix = TunDelayArgument + "=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return ParseSeconds(argument[prefix.Length..]);
            }
        }

        return TunDelayStartupOption.NotSpecified;
    }

    private static TunDelayStartupOption ParseSeconds(string value)
    {
        if (int.TryParse(value.Trim(), out var delaySeconds) && delaySeconds >= 0)
        {
            return new TunDelayStartupOption(true, delaySeconds, null);
        }

        return Invalid($"Invalid -tundelay value '{value}'. A non-negative number of seconds is required.");
    }

    private static TunDelayStartupOption Invalid(string error)
    {
        // The presence of -tundelay always suppresses TUN during startup.
        // Invalid or missing values are treated as zero: manual TUN enable only.
        return new TunDelayStartupOption(true, 0, error);
    }
}
