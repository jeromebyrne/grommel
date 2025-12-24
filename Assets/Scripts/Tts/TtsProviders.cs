using System.Threading.Tasks;
using UnityEngine;

using System.Threading.Tasks;
using UnityEngine;
using Grommel.Tts;

namespace Grommel.Tts
{
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

        public Task<AudioClip> GenerateClipAsync(string text)
        {
            return _impl.GenerateClipAsync(text);
        }
    }
}
