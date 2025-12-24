using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Abstraction for text-to-speech providers.
/// </summary>
public interface ITtsProvider
{
    float LengthScale { get; }
    Task<AudioClip> GenerateClipAsync(string text);
}

public enum TtsProviderKind
{
    Piper,
    Coqui
}
