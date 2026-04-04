namespace VoxFlow.Cli;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

/// <summary>
/// Renders transcription progress updates to the console using a colored ANSI progress bar
/// with elapsed time, current language, and stage information.
/// Respects all <see cref="ConsoleProgressOptions"/> settings.
/// </summary>
internal sealed class CliProgressHandler : IProgress<ProgressUpdate>
{
    private const string StructuredProgressPrefix = "VOXFLOW_PROGRESS ";

    private readonly ConsoleProgressOptions _options;
    private readonly bool _useAnsi;
    private readonly Stopwatch _throttle = Stopwatch.StartNew();
    private long _lastRenderTick;

    public CliProgressHandler(ConsoleProgressOptions options)
    {
        _options = options;
        _useAnsi = options.UseColors && !Console.IsOutputRedirected;
    }

    public void Report(ProgressUpdate value)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("VOXFLOW_PROGRESS_STREAM"), "1", StringComparison.Ordinal))
        {
            ReportStructured(value);
            return;
        }

        if (!_options.Enabled)
            return;

        var isTerminal = value.Stage is ProgressStage.Complete or ProgressStage.Failed;

        // Throttle non-terminal updates to the configured refresh interval.
        if (!isTerminal)
        {
            var now = _throttle.ElapsedMilliseconds;
            if (now - _lastRenderTick < _options.RefreshIntervalMilliseconds)
                return;
            _lastRenderTick = now;
        }

        var output = new StringBuilder();

        // Batch file indicator for batch mode.
        if (value.BatchFileIndex.HasValue && value.BatchFileTotal.HasValue)
        {
            output.Append(Colorize($"[File {value.BatchFileIndex}/{value.BatchFileTotal}] ", "36"));
        }

        // Stage label.
        var stage = FormatStage(value.Stage);
        output.Append(Colorize(stage, StageColor(value.Stage)));

        // Progress bar.
        output.Append(' ');
        AppendProgressBar(output, value.PercentComplete);

        // Percentage.
        output.Append(Colorize($" {value.PercentComplete,5:F1}%", "97"));

        // Elapsed time.
        output.Append(Colorize($"  {FormatElapsed(value.Elapsed)}", "90"));

        // Current language during inference.
        if (!string.IsNullOrEmpty(value.CurrentLanguage))
        {
            output.Append(Colorize($"  [{value.CurrentLanguage}]", "33"));
        }

        // Extra message (e.g. file name in batch mode).
        if (!string.IsNullOrEmpty(value.Message))
        {
            output.Append(Colorize($"  {value.Message}", "90"));
        }

        // Pad to overwrite any leftover characters from a previous longer line.
        var padded = output.ToString().PadRight(Console.IsOutputRedirected ? 0 : Console.WindowWidth - 1);

        if (isTerminal)
        {
            Console.Write($"\r{padded}");
            Console.WriteLine();
        }
        else
        {
            Console.Write($"\r{padded}");
        }
    }

    private void AppendProgressBar(StringBuilder sb, double percent)
    {
        var width = _options.ProgressBarWidth;
        var filled = (int)(percent / 100.0 * width);
        if (filled > width) filled = width;

        sb.Append(Colorize("[", "90"));

        if (filled > 0)
            sb.Append(Colorize(new string('█', filled), "92"));

        var remaining = width - filled;
        if (remaining > 0)
            sb.Append(Colorize(new string('░', remaining), "90"));

        sb.Append(Colorize("]", "90"));
    }

    private static string FormatStage(ProgressStage stage)
    {
        return stage switch
        {
            ProgressStage.Validating => "Validating ",
            ProgressStage.Converting => "Converting ",
            ProgressStage.LoadingModel => "Loading    ",
            ProgressStage.Transcribing => "Transcribing",
            ProgressStage.Filtering => "Filtering  ",
            ProgressStage.Writing => "Writing    ",
            ProgressStage.Complete => "Complete   ",
            ProgressStage.Failed => "Failed     ",
            _ => "Working    "
        };
    }

    private static string StageColor(ProgressStage stage)
    {
        return stage switch
        {
            ProgressStage.Complete => "92",
            ProgressStage.Failed => "91",
            ProgressStage.Transcribing => "96",
            _ => "94"
        };
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"h\:mm\:ss")
            : elapsed.ToString(@"m\:ss");
    }

    private string Colorize(string text, string colorCode)
    {
        if (!_useAnsi || string.IsNullOrEmpty(text))
            return text;

        return $"\u001b[{colorCode}m{text}\u001b[0m";
    }

    private static void ReportStructured(ProgressUpdate value)
    {
        var payload = JsonSerializer.Serialize(new CliProgressEnvelope(
            value.Stage.ToString(),
            value.PercentComplete,
            (long)value.Elapsed.TotalMilliseconds,
            value.Message,
            value.CurrentLanguage,
            value.BatchFileIndex,
            value.BatchFileTotal));

        Console.Error.WriteLine($"{StructuredProgressPrefix}{payload}");
    }
}

internal sealed record CliProgressEnvelope(
    string Stage,
    double PercentComplete,
    long ElapsedMilliseconds,
    string? Message,
    string? CurrentLanguage,
    int? BatchFileIndex,
    int? BatchFileTotal);
