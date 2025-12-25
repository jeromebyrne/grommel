using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Stt
{
    /// <summary>
    /// Whisper STT bridge using the whisper.cpp CLI.
    /// </summary>
    public class WhisperStt : ISttProvider
    {
        readonly string _whisperExecutable;
        readonly string _modelPath;
        readonly string _language;
        readonly bool _useGpu;
        readonly bool _noTimestamps;
        readonly int _threads;

        /// <param name="whisperExecutable">Path to whisper.cpp binary (e.g., Tools/whisper.cpp/main).</param>
        /// <param name="modelPath">Path to ggml model (e.g., Tools/models/ggml-small.en.bin).</param>
        /// <param name="language">Optional ISO code (e.g., "en"). Leave empty to auto-detect.</param>
        public WhisperStt(string whisperExecutable, string modelPath, string language = "", bool useGpu = false, bool noTimestamps = true, int threads = 2)
        {
            _whisperExecutable = whisperExecutable ?? string.Empty;
            _modelPath = modelPath ?? string.Empty;
            _language = language ?? string.Empty;
            _useGpu = useGpu;
            _noTimestamps = noTimestamps;
            _threads = threads;
        }

        public async Task<string> TranscribeAsync(AudioClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            var pcmBytes = ConvertToPcm16(clip);
            return await TranscribePcmAsync(pcmBytes);
        }

        public async Task<string> TranscribeAsync(string wavPath)
        {
            if (string.IsNullOrWhiteSpace(_whisperExecutable) || !File.Exists(_whisperExecutable))
            {
                UnityEngine.Debug.LogError($"Whisper STT: Executable not found at '{_whisperExecutable}'.");
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(_modelPath) || !File.Exists(_modelPath))
            {
                UnityEngine.Debug.LogError($"Whisper STT: Model not found at '{_modelPath}'.");
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
            {
                UnityEngine.Debug.LogError($"Whisper STT: WAV path not found: {wavPath}");
                return string.Empty;
            }

            return await RunWhisperAsync($"-m \"{_modelPath}\" -f \"{wavPath}\"");
        }

        async Task<string> TranscribePcmAsync(byte[] pcmData)
        {
            if (string.IsNullOrWhiteSpace(_whisperExecutable) || !File.Exists(_whisperExecutable))
            {
                UnityEngine.Debug.LogError($"Whisper STT: Executable not found at '{_whisperExecutable}'.");
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(_modelPath) || !File.Exists(_modelPath))
            {
                UnityEngine.Debug.LogError($"Whisper STT: Model not found at '{_modelPath}'.");
                return string.Empty;
            }

            return await RunWhisperAsync($"-m \"{_modelPath}\" -f -", pcmData);
        }

        async Task<string> RunWhisperAsync(string baseArgs, byte[] stdinData = null)
        {
            return await Task.Run(() =>
            {
                string tempBase = Path.Combine(Path.GetTempPath(), $"whisper_out_{DateTime.UtcNow.Ticks}");
                string txtPath = tempBase + ".txt";
                try
                {
                    string args = $"{baseArgs} -otxt -of \"{tempBase}\" -np";
                    if (_noTimestamps)
                    {
                        args += " -nt";
                    }
                    if (_threads > 0)
                    {
                        args += $" -t {_threads}";
                    }
                    if (!_useGpu)
                    {
                        args += " -ng";
                    }
                    if (!string.IsNullOrWhiteSpace(_language))
                    {
                        args += $" -l {_language}";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = _whisperExecutable,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = stdinData != null,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_whisperExecutable)
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (stdinData != null)
                        {
                            proc.StandardInput.BaseStream.Write(stdinData, 0, stdinData.Length);
                            proc.StandardInput.BaseStream.Flush();
                            proc.StandardInput.Close();
                        }

                        string output = proc.StandardOutput.ReadToEnd();
                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        if (proc.ExitCode != 0)
                        {
                            UnityEngine.Debug.LogError($"Whisper STT failed ({proc.ExitCode}). Stderr: {stderr}");
                            return string.Empty;
                        }

                        if (File.Exists(txtPath))
                        {
                            var text = File.ReadAllText(txtPath).Trim();
                            return text;
                        }

                        return output.Trim();
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Whisper STT exception: {ex.Message}");
                    return string.Empty;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(txtPath)) File.Delete(txtPath);
                    }
                    catch { }
                }
            });
        }

        byte[] ConvertToPcm16(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var pcm = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)(val & 0xFF);
                pcm[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return pcm;
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
