#nullable enable
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using VoxFlow.McpServer.Tools;
using Xunit;

namespace VoxFlow.McpServer.Tests;

public sealed class TranscribeFileToolSchemaTests
{
    [Fact]
    public void TranscribeFileAsync_SchemaContainsEnableSpeakersBooleanParameter()
    {
        var method = typeof(WhisperMcpTools)
            .GetMethod("TranscribeFileAsync", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        var parameter = method!.GetParameters().SingleOrDefault(p => p.Name == "enableSpeakers");
        Assert.NotNull(parameter);

        Assert.Equal(typeof(bool?), parameter!.ParameterType);
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);

        var description = parameter.GetCustomAttribute<DescriptionAttribute>();
        Assert.NotNull(description);
        Assert.False(string.IsNullOrWhiteSpace(description!.Description));
        Assert.Contains("speakerLabeling", description.Description, System.StringComparison.Ordinal);
    }
}
