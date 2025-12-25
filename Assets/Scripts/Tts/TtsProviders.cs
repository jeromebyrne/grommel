using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Tts
{
    /// <summary>
    /// Piper TTS adapter implementing ITtsProvider.
    /// </summary>
    public class PiperTtsProvider : ITtsProvider
    {
        readonly PiperTts _impl = new PiperTts();

        public float LengthScale => _impl.LengthScale;

        public Task<AudioClip> GenerateClipAsync(string text, string speakerId = null, float? speechRate = null)
        {
            return _impl.GenerateClipAsync(text, speakerId, speechRate);
        }
    }

    /// <summary>
    /// Coqui TTS adapter implementing ITtsProvider.
    /// </summary>
    public class CoquiTtsProvider : ITtsProvider
    {
        readonly CoquiTts _impl = new CoquiTts();

        public float LengthScale => _impl.LengthScale;

        public Task<AudioClip> GenerateClipAsync(string text, string speakerId = null, float? speechRate = null)
        {
            return _impl.GenerateClipAsync(text, speakerId, speechRate);
        }
    }

    /// <summary>
    /// macOS 'say' adapter implementing ITtsProvider (no audio clip returned).
    /// </summary>
    public class MacTtsProvider : ITtsProvider
    {
        readonly MacTts _impl;

        public float LengthScale => _impl.LengthScale;

        public MacTtsProvider(string voice = "Whisper", int rate = 200)
        {
            _impl = new MacTts(voice, rate);
        }

        public Task<AudioClip> GenerateClipAsync(string text, string speakerId = null, float? speechRate = null)
        {
            return _impl.GenerateClipAsync(text, speakerId, speechRate);
        }
    }
}
