#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Orchestrates application startup, validation, transcription, and output writing.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the transcription workflow and returns a process exit code.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, eventArgs) =>
        {
            // Keep the process alive long enough for the current async operation
            // to observe the token and exit through the normal cancellation path.
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
            Console.Error.WriteLine("Cancellation requested. Stopping...");
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            var options = TranscriptionOptions.Load();

            if (options.StartupValidation.Enabled)
            {
                // Fail fast before conversion, model download, or transcription begin.
                var validationReport = await StartupValidationService.ValidateAsync(options, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                StartupValidationConsoleReporter.Write(validationReport, options.StartupValidation.PrintDetailedReport);

                if (!validationReport.CanStart)
                {
                    Console.Error.WriteLine("Startup validation failed. Transcription will not start.");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("Startup validation is disabled by configuration.");
                AudioConversionService.ValidateInputFile(options.InputFilePath);
                await AudioConversionService.ValidateFfmpegAvailabilityAsync(options, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }

            Console.WriteLine("Starting transcription...");
            await AudioConversionService.ConvertToWavAsync(options, cancellationTokenSource.Token).ConfigureAwait(false);

            // Native Whisper teardown has shown instability on macOS after multi-language passes,
            // so the process intentionally keeps the factory alive until process exit.
            var whisperFactory = await ModelService.CreateFactoryAsync(options, cancellationTokenSource.Token)
                .ConfigureAwait(false);

            // Load the prepared WAV only after conversion and model setup succeed.
            var audioSamples = await WavAudioLoader.LoadSamplesAsync(
                    options.WavFilePath,
                    options,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false);
            var selectionResult = await LanguageSelectionService.SelectBestCandidateAsync(
                whisperFactory,
                audioSamples,
                options,
                cancellationTokenSource.Token).ConfigureAwait(false);

            Console.WriteLine($"Detected language: {selectionResult.Language.DisplayName} ({selectionResult.Language.Code})");

            await OutputWriter.WriteAsync(
                    options.ResultFilePath,
                    selectionResult.AcceptedSegments,
                    cancellationTokenSource.Token)
                .ConfigureAwait(false);

            Console.WriteLine($"Final output file path: {options.ResultFilePath}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Processing canceled.");
            return 1;
        }
        catch (UnsupportedLanguageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Processing failed: {ex.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
