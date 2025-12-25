using System;
using System.IO;
using System.Threading.Tasks;
using Grommel.Stt;
using UnityEngine;
using System.Collections;

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
        const string _macSttArgs = "{0}";  // format string with {0} = wav path
        const string _whisperLanguage = "en";
        [SerializeField] int _maxRecordSeconds = 15;
        [SerializeField] int _sampleRate = 16000;
        [SerializeField] float _streamIntervalSeconds = 0.75f;

        ISttProvider _sttProvider;
        string _micDevice;
        AudioClip _recording;
        bool _isRecording;
        int _lastSampleIndex;
        Coroutine _streamRoutine;

        public event Action<string> OnPartialTranscription;
        public event Action<string> OnFinalTranscription;

        void Awake()
        {
            _sttProvider = CreateSttProvider();
        }

        public int MaxRecordSeconds => _maxRecordSeconds;

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
            // Default to Tools/stt_mac relative to project root.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string defaultPath = Path.Combine(projectRoot, "Tools", "stt_mac");
            return defaultPath;
        }

        string ResolveWhisperPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Tools", "whisper.cpp", "build", "bin", "whisper-cli");
        }

        string ResolveWhisperModel()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Tools", "whisper.cpp", "models", "ggml-small.en.bin");
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
            _lastSampleIndex = 0;
            _streamRoutine = StartCoroutine(StreamPartials());
        }

        public void StopAndTranscribe()
        {
            if (!_isRecording)
            {
                return;
            }
            _isRecording = false;
            Microphone.End(_micDevice);
            if (_streamRoutine != null)
            {
                StopCoroutine(_streamRoutine);
                _streamRoutine = null;
            }
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
                    OnFinalTranscription?.Invoke(text);
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

        IEnumerator StreamPartials()
        {
            while (_isRecording)
            {
                yield return new WaitForSeconds(_streamIntervalSeconds);
                if (!_isRecording || _recording == null)
                {
                    continue;
                }

                int currentPos = Microphone.GetPosition(_micDevice);
                int sampleCount = currentPos - _lastSampleIndex;
                if (sampleCount <= 0)
                {
                    continue;
                }

                var buffer = new float[sampleCount * _recording.channels];
                _recording.GetData(buffer, _lastSampleIndex);
                _lastSampleIndex = currentPos;

                string tempPath = Path.Combine(Application.temporaryCachePath, $"voice_partial_{DateTime.UtcNow.Ticks}.wav");
                WriteWavFromSamples(tempPath, buffer, _recording.channels, _sampleRate);
                var task = _sttProvider.TranscribeAsync(tempPath);
                while (!task.IsCompleted)
                {
                    yield return null;
                }
                try
                {
                    if (!task.IsFaulted && !task.IsCanceled)
                    {
                        var text = task.Result;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            OnPartialTranscription?.Invoke(text);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"VoiceInput partial STT failed: {ex.Message}");
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                }
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

        void WriteWavFromSamples(string path, float[] samples, int channels, int frequency)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentNullException(nameof(samples));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                int sampleCount = samples.Length;
                int byteRate = frequency * channels * 2;
                int dataSize = sampleCount * 2;

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)channels);
                bw.Write(frequency);
                bw.Write(byteRate);
                bw.Write((short)(channels * 2));
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
