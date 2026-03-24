using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoxFlow.Core.Interfaces;

namespace VoxFlow.Desktop.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IConfigurationService _configService;

    private string _modelType = "base";
    private string _language = "English";
    private string _outputDirectory = "";
    private string _ffmpegPath = "";

    public SettingsViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    public string ModelType { get => _modelType; set { _modelType = value; OnPropertyChanged(); } }
    public string Language { get => _language; set { _language = value; OnPropertyChanged(); } }
    public string OutputDirectory { get => _outputDirectory; set { _outputDirectory = value; OnPropertyChanged(); } }
    public string FfmpegPath { get => _ffmpegPath; set { _ffmpegPath = value; OnPropertyChanged(); } }

    public async Task LoadAsync()
    {
        var options = await _configService.LoadAsync();
        ModelType = options.ModelType ?? "base";
        Language = options.SupportedLanguages.Count > 0
            ? options.SupportedLanguages[0].DisplayName
            : "English";
        OutputDirectory = options.ResultFilePath ?? "";
        FfmpegPath = options.FfmpegExecutablePath ?? "";
    }

    public async Task SaveAsync()
    {
        // Desktop config saving will be implemented in Task 18
        // For now just holds the values in memory
        await Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
