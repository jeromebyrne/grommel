using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Stt
{
    /// <summary>
    /// Abstraction for speech-to-text providers.
    /// </summary>
    public interface ISttProvider
    {
        /// <summary>
        /// Transcribe a Unity AudioClip. Implementations may write to disk or stream in-memory as needed.
        /// </summary>
        Task<string> TranscribeAsync(AudioClip clip);

        /// <summary>
        /// Transcribe an existing WAV file on disk.
        /// </summary>
        Task<string> TranscribeAsync(string wavPath);
    }

    public enum SttProviderKind
    {
        Mac
        // Future: Whisper, Vosk, Azure, etc.
    }
}
