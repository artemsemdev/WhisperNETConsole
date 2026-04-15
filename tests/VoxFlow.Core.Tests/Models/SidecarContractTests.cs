using System;
using System.IO;
using System.Threading.Tasks;
using NJsonSchema;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class SidecarContractTests
{
    [Fact]
    public async Task ValidResponse_ValidatesAgainstSchema()
    {
        var schema = await LoadSchemaAsync();

        const string validResponse = """
        {
          "version": 1,
          "status": "ok",
          "speakers": [
            { "id": "A", "totalDuration": 3.5 },
            { "id": "B", "totalDuration": 2.0 }
          ],
          "segments": [
            { "speaker": "A", "start": 0.0, "end": 3.5 },
            { "speaker": "B", "start": 3.5, "end": 5.5 }
          ]
        }
        """;

        var errors = schema.Validate(validResponse);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task InvalidResponse_MissingVersion_FailsSchema()
    {
        var schema = await LoadSchemaAsync();

        const string invalidResponse = """
        {
          "status": "ok",
          "speakers": [
            { "id": "A", "totalDuration": 3.5 }
          ],
          "segments": [
            { "speaker": "A", "start": 0.0, "end": 3.5 }
          ]
        }
        """;

        var errors = schema.Validate(invalidResponse);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ErrorResponse_WithErrorString_ValidatesAgainstSchema()
    {
        var schema = await LoadSchemaAsync();

        const string errorResponse = """
        {
          "version": 1,
          "status": "error",
          "error": "failed to load model",
          "speakers": [],
          "segments": []
        }
        """;

        var errors = schema.Validate(errorResponse);

        Assert.Empty(errors);
    }

    private static async Task<JsonSchema> LoadSchemaAsync()
    {
        var schemaPath = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "sidecar-diarization-v1.schema.json");
        Assert.True(File.Exists(schemaPath), $"Schema not found at {schemaPath}");
        return await JsonSchema.FromFileAsync(schemaPath);
    }
}
