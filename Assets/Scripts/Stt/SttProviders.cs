using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Stt
{
    /// <summary>
    /// STT provider adapters to decouple consumers from concrete implementations.
    /// </summary>
    public class MacSttProvider : ISttProvider
    {
        readonly MacStt _impl;

        public MacSttProvider(string sttCommand = "", string argumentsFormat = "{0}")
        {
            _impl = new MacStt(sttCommand, argumentsFormat);
        }

        public Task<string> TranscribeAsync(AudioClip clip) => _impl.TranscribeAsync(clip);
        public Task<string> TranscribeAsync(string wavPath) => _impl.TranscribeAsync(wavPath);
    }

    public class WhisperSttProvider : ISttProvider
    {
        readonly WhisperStt _impl;

        public WhisperSttProvider(string whisperExecutable, string modelPath, string language = "")
        {
            _impl = new WhisperStt(whisperExecutable, modelPath, language);
        }

        public Task<string> TranscribeAsync(AudioClip clip) => _impl.TranscribeAsync(clip);
        public Task<string> TranscribeAsync(string wavPath) => _impl.TranscribeAsync(wavPath);
    }
}
