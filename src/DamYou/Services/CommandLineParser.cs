namespace DamYou.Services;

/// <summary>
/// Simple command-line argument parser for the MAUI app.
/// Handles patterns like --log filename, --debug, etc.
/// </summary>
public static class CommandLineParser
{
    public static ParsedArgs Parse(string[] args)
    {
        var result = new ParsedArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--log" && i + 1 < args.Length)
            {
                result.LogFilePath = args[i + 1];
                i++;
            }
        }

        return result;
    }

    public class ParsedArgs
    {
        public string? LogFilePath { get; set; }
    }
}
