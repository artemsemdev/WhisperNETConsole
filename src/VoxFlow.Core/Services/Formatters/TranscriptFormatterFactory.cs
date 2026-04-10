using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Resolves the correct <see cref="ITranscriptFormatter"/> for a given <see cref="ResultFormat"/>.
/// </summary>
internal static class TranscriptFormatterFactory
{
    private static readonly TxtTranscriptFormatter Txt = new();
    private static readonly SrtTranscriptFormatter Srt = new();
    private static readonly VttTranscriptFormatter Vtt = new();
    private static readonly JsonTranscriptFormatter Json = new();
    private static readonly MdTranscriptFormatter Md = new();

    public static ITranscriptFormatter GetFormatter(ResultFormat format) => format switch
    {
        ResultFormat.Txt => Txt,
        ResultFormat.Srt => Srt,
        ResultFormat.Vtt => Vtt,
        ResultFormat.Json => Json,
        ResultFormat.Md => Md,
        _ => Txt
    };
}
