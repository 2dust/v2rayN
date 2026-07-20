namespace ServiceLib.Common;

internal sealed record TunStartupSettings(int ObservationSeconds)
{
    private const int DefaultObservationSeconds = 20;
    private const int MinObservationSeconds = 20;
    private const int MaxObservationSeconds = 300;
    private const string FileName = "finetunes.ini";
    private const string ObservationKey = "TunStartObservationSeconds";

    public static TunStartupSettings LoadOrCreate()
    {
        return LoadOrCreate(AppContext.BaseDirectory);
    }

    internal static TunStartupSettings LoadOrCreate(string baseDirectory)
    {
        var filePath = Path.Combine(baseDirectory, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                WriteDefaultFile(filePath);
                Logging.SaveLog($"TUN startup settings: created {filePath} with {ObservationKey}={DefaultObservationSeconds}.");
                return new TunStartupSettings(DefaultObservationSeconds);
            }

            var lines = File.ReadAllLines(filePath);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex].Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                if (!key.Equals(ObservationKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[(separator + 1)..].Trim();
                if (int.TryParse(value, out var seconds)
                    && seconds >= MinObservationSeconds
                    && seconds <= MaxObservationSeconds)
                {
                    Logging.SaveLog($"TUN startup settings: {ObservationKey}={seconds} from {filePath}.");
                    return new TunStartupSettings(seconds);
                }

                lines[lineIndex] = $"{ObservationKey}={DefaultObservationSeconds}";
                File.WriteAllLines(filePath, lines, new UTF8Encoding(false));

                if (int.TryParse(value, out seconds) && seconds < MinObservationSeconds)
                {
                    Logging.SaveLog(
                        $"TUN startup settings: {ObservationKey}={seconds} is below the minimum; " +
                        $"changed it to {DefaultObservationSeconds} in {filePath}.");
                }
                else
                {
                    Logging.SaveLog(
                        $"TUN startup settings: invalid {ObservationKey}='{value}'; " +
                        $"changed it to {DefaultObservationSeconds} in {filePath}.");
                }

                return new TunStartupSettings(DefaultObservationSeconds);
            }

            File.AppendAllText(
                filePath,
                $"{Environment.NewLine}{ObservationKey}={DefaultObservationSeconds}{Environment.NewLine}",
                new UTF8Encoding(false));
            Logging.SaveLog(
                $"TUN startup settings: added missing {ObservationKey}={DefaultObservationSeconds} to {filePath}.");
            return new TunStartupSettings(DefaultObservationSeconds);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("TunStartupSettings", ex);
            Logging.SaveLog(
                $"TUN startup settings: using {DefaultObservationSeconds} seconds because {filePath} could not be read or created.");
            return new TunStartupSettings(DefaultObservationSeconds);
        }
    }

    private static void WriteDefaultFile(string filePath)
    {
        var content =
            "; v2rayN fine tuning settings" + Environment.NewLine +
            "; Time used to verify that a newly started TUN core remains alive." + Environment.NewLine +
            $"{ObservationKey}={DefaultObservationSeconds}" + Environment.NewLine;

        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }
}
