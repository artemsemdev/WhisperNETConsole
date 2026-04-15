using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services;
using VoxFlow.Core.Services.Diarization;
using VoxFlow.Core.Services.Python;

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
        services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
        services.AddSingleton<IVenvPaths, DefaultVenvPaths>();
        services.AddSingleton<ISpeakerEnrichmentService>(sp =>
            new CompositionSpeakerEnrichmentService(
                sp.GetRequiredService<IProcessLauncher>(),
                sp.GetRequiredService<IVenvPaths>(),
                sp.GetRequiredService<ISpeakerMergeService>(),
                ResolveSidecarScriptPath()));
        services.AddSingleton<ISpeakerLabelingPreflight>(sp =>
            new CompositionSpeakerLabelingPreflight(
                sp.GetRequiredService<IProcessLauncher>(),
                sp.GetRequiredService<IVenvPaths>(),
                CompositionSpeakerLabelingPreflight.ResolveDefaultHubCacheRoot()));
        services.AddSingleton<IVoxflowTranscriptArtifactWriter, VoxflowTranscriptArtifactWriter>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<IBatchTranscriptionService, BatchTranscriptionService>();
        return services;
    }

    /// <summary>
    /// Locates <c>voxflow_diarize.py</c> next to the currently executing
    /// assembly. Tests link the script into <c>python/voxflow_diarize.py</c>
    /// under the test output dir; production packaging uses the same layout.
    /// </summary>
    private static string ResolveSidecarScriptPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ServiceCollectionExtensions).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var candidate = Path.Combine(assemblyDir, "python", "voxflow_diarize.py");
        return candidate;
    }
}
