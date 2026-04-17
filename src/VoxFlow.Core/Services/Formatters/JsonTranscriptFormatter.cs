using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Formats transcript segments as structured JSON output.
/// Includes metadata (format, language, counts, warnings) and segment data.
/// </summary>
internal sealed class JsonTranscriptFormatter : ITranscriptFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var output = new JsonTranscriptOutput
        {
            Format = context.Format.ToString().ToLowerInvariant(),
            DetectedLanguage = context.DetectedLanguage,
            AcceptedSegmentCount = context.AcceptedSegmentCount,
            SkippedSegmentCount = context.SkippedSegmentCount,
            Warnings = context.Warnings ?? Array.Empty<string>(),
            Segments = segments.Select(s => new JsonTranscriptSegment
            {
                Start = FormatTimestamp(s.Start),
                End = FormatTimestamp(s.End),
                Text = s.Text
            }).ToArray(),
            Transcript = BuildPlainTranscript(segments),
            SpeakerTranscript = context.SpeakerTranscript
        };

        return JsonSerializer.Serialize(output, SerializerOptions);
    }

    private static string FormatTimestamp(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static string BuildPlainTranscript(IReadOnlyList<FilteredSegment> segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(segment.Text.Trim());
        }
        return builder.ToString();
    }

    private sealed class JsonTranscriptOutput
    {
        public string Format { get; set; } = string.Empty;
        public string? DetectedLanguage { get; set; }
        public int AcceptedSegmentCount { get; set; }
        public int SkippedSegmentCount { get; set; }
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
        public JsonTranscriptSegment[] Segments { get; set; } = Array.Empty<JsonTranscriptSegment>();
        public string Transcript { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TranscriptDocument? SpeakerTranscript { get; set; }
    }

    private sealed class JsonTranscriptSegment
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
