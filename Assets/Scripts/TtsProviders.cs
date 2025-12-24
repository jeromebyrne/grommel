using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Piper TTS adapter implementing ITtsProvider.
/// </summary>
public class PiperTtsProvider : ITtsProvider
{
    readonly PiperTts _impl = new PiperTts();

    public float LengthScale => _impl.LengthScale;

    public Task<AudioClip> GenerateClipAsync(string text)
    {
        return _impl.GenerateClipAsync(text);
    }
}

/// <summary>
/// Coqui TTS adapter implementing ITtsProvider.
/// </summary>
public class CoquiTtsProvider : ITtsProvider
{
    readonly CoquiTts _impl = new CoquiTts();

    public float LengthScale => _impl.LengthScale;

    public Task<AudioClip> GenerateClipAsync(string text)
    {
        return _impl.GenerateClipAsync(text);
    }
}
