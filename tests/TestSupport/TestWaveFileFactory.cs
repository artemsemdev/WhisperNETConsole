using System.IO;
using System.Text;

internal static class TestWaveFileFactory
{
    public static string CreatePcm16MonoWave(string filePath, int sampleRate, short[] samples)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        const short bitsPerSample = 16;
        const short channelCount = 1;
        var byteRate = sampleRate * channelCount * bitsPerSample / 8;
        var blockAlign = (short)(channelCount * bitsPerSample / 8);
        var dataSize = samples.Length * sizeof(short);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        return filePath;
    }
}
