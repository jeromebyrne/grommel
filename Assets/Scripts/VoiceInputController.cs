using System;
using System.IO;
using System.Threading.Tasks;
using Grommel.Stt;
using UnityEngine;

namespace Grommel
{
    /// <summary>
    /// Simple push-to-talk voice input: records mic audio to WAV, runs STT, and forwards text to the dialogue controller.
    /// Hook StartRecording/StopAndTranscribe to UI buttons or key events.
    /// </summary>
    public class VoiceInputController : MonoBehaviour
    {
        [SerializeField] NpcDialogueController _dialogue;
        [SerializeField] SttProviderKind _sttProviderKind = SttProviderKind.Mac;
        [SerializeField] string _macSttCommand = ""; // optional override; leave empty to use Tools/stt_mac relative to project
        [SerializeField] string _macSttArgs = "{0}";  // format string with {0} = wav path
        [SerializeField] string _whisperCommand = ""; // optional override; leave empty to use Tools/whisper.cpp/main relative to project
        [SerializeField] string _whisperModel = "";   // optional override; leave empty to use Tools/models/ggml-small.en.bin
        [SerializeField] string _whisperLanguage = "en";
        [SerializeField] int _maxRecordSeconds = 15;
        [SerializeField] int _sampleRate = 16000;

        ISttProvider _sttProvider;
        string _micDevice;
        AudioClip _recording;
        bool _isRecording;

        void Awake()
        {
            _sttProvider = CreateSttProvider();
        }

        ISttProvider CreateSttProvider()
        {
            switch (_sttProviderKind)
            {
                case SttProviderKind.Mac:
                default:
                    return new MacSttProvider(ResolveMacSttPath(), _macSttArgs);
                case SttProviderKind.Whisper:
                    return new WhisperSttProvider(ResolveWhisperPath(), ResolveWhisperModel(), _whisperLanguage);
            }
        }

        string ResolveMacSttPath()
        {
            if (!string.IsNullOrWhiteSpace(_macSttCommand))
            {
                return _macSttCommand;
            }
            // Default to Tools/stt_mac relative to project root.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string defaultPath = Path.Combine(projectRoot, "Tools", "stt_mac");
            return defaultPath;
        }

        string ResolveWhisperPath()
        {
            if (!string.IsNullOrWhiteSpace(_whisperCommand))
            {
                return _whisperCommand;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Tools", "whisper.cpp", "main");
        }

        string ResolveWhisperModel()
        {
            if (!string.IsNullOrWhiteSpace(_whisperModel))
            {
                return _whisperModel;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Tools", "models", "ggml-small.en.bin");
        }

        public void StartRecording()
        {
            if (_isRecording)
            {
                return;
            }

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogError("VoiceInput: No microphone devices found.");
                return;
            }

            _micDevice = Microphone.devices[0];
            _recording = Microphone.Start(_micDevice, false, _maxRecordSeconds, _sampleRate);
            _isRecording = true;
        }

        public void StopAndTranscribe()
        {
            if (!_isRecording)
            {
                return;
            }
            _isRecording = false;
            Microphone.End(_micDevice);
            _ = TranscribeAsync();
        }

        async Task TranscribeAsync()
        {
            if (_recording == null)
            {
                return;
            }

            string tempWav = Path.Combine(Application.temporaryCachePath, $"voice_{DateTime.UtcNow.Ticks}.wav");
            try
            {
                WriteWav(tempWav, _recording);
                string text = await _sttProvider.TranscribeAsync(tempWav);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _dialogue?.SubmitExternalLine(text);
                }
                else
                {
                    Debug.LogWarning("VoiceInput: STT returned empty text.");
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempWav))
                    {
                        File.Delete(tempWav);
                    }
                }
                catch { }
            }
        }

        void WriteWav(string path, AudioClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }
            if (clip.channels <= 0)
            {
                throw new InvalidOperationException("AudioClip has no channels.");
            }

            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                int sampleCount = samples.Length;
                int byteRate = clip.frequency * clip.channels * 2;
                int dataSize = sampleCount * 2;

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)clip.channels);
                bw.Write(clip.frequency);
                bw.Write(byteRate);
                bw.Write((short)(clip.channels * 2));
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataSize);

                foreach (var sample in samples)
                {
                    short val = (short)Mathf.Clamp(sample * 32767f, short.MinValue, short.MaxValue);
                    bw.Write(val);
                }
            }
        }
    }
}
