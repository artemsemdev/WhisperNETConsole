using System.Text;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Formatters;

namespace VoxFlow.Core.Services;

/// <summary>
/// Writes filtered transcript segments to disk in the configured output format.
/// Delegates format-specific serialization to <see cref="ITranscriptFormatter"/> implementations.
/// </summary>
internal sealed class OutputWriter : IOutputWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes the formatted transcript to the target file using UTF-8 without a BOM.
    /// The output path extension is normalized to match the selected format.
    /// </summary>
    public async Task WriteAsync(
        string outputPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptOutputContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = BuildOutputText(segments, context);

        cancellationToken.ThrowIfCancellationRequested();
        await using var writer = new StreamWriter(outputPath, append: false, Utf8NoBom);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the formatted output text using the format specified in the context.
    /// </summary>
    public string BuildOutputText(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var formatter = TranscriptFormatterFactory.GetFormatter(context.Format);
        return formatter.Format(segments, context);
    }
}
