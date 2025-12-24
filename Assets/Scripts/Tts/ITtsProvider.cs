using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Tts
{
    /// <summary>
    /// Abstraction for text-to-speech providers.
    /// </summary>
    public interface ITtsProvider
    {
        float LengthScale { get; }
        Task<AudioClip> GenerateClipAsync(string text, string speakerId = null);
    }

    public enum TtsProviderKind
    {
        Piper,
        Coqui,
        Mac
    }
}
