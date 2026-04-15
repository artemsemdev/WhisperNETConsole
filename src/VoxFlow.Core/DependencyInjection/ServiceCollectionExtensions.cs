using Microsoft.Extensions.DependencyInjection;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services;
using VoxFlow.Core.Services.Diarization;

namespace VoxFlow.Core.DependencyInjection;

/// <summary>
/// Registers the shared VoxFlow core services used by CLI, Desktop, and MCP hosts.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core transcription pipeline services to the supplied service collection.
    /// </summary>
    public static IServiceCollection AddVoxFlowCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IAudioConversionService, AudioConversionService>();
        services.AddSingleton<IModelService, ModelService>();
        services.AddSingleton<IWavAudioLoader, WavAudioLoader>();
        services.AddSingleton<ILanguageSelectionService, LanguageSelectionService>();
        services.AddSingleton<ITranscriptionFilter, TranscriptionFilter>();
        services.AddSingleton<IOutputWriter, OutputWriter>();
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        services.AddSingleton<IBatchSummaryWriter, BatchSummaryWriter>();
        services.AddSingleton<ITranscriptReader, TranscriptReader>();
        services.AddSingleton<ISpeakerMergeService, SpeakerMergeService>();
        services.AddSingleton<ISpeakerEnrichmentService, NullSpeakerEnrichmentService>();
        services.AddSingleton<IVoxflowTranscriptArtifactWriter, VoxflowTranscriptArtifactWriter>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<IBatchTranscriptionService, BatchTranscriptionService>();
        return services;
    }
}
