using System.IO;
using System.Text;

internal static class FakeFfmpegFactory
{
    public static string Create(string directoryPath, string preparedWavPath)
    {
        var scriptPath = Path.Combine(directoryPath, "fake-ffmpeg.sh");
        var script = $$"""
#!/bin/bash
set -euo pipefail

if [[ "${1:-}" == "-version" ]]; then
  echo "ffmpeg version fake-1.0"
  exit 0
fi

output="${@: -1}"
cp "{{preparedWavPath}}" "$output"
""";

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                scriptPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }

        return scriptPath;
    }
}
