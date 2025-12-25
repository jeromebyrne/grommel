using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Grommel.Stt
{
    /// <summary>
    /// macOS speech-to-text bridge. This class shells out to a configurable command that accepts a WAV path
    /// and returns the recognized text on stdout. By default it logs a warning until you point it at a
    /// working macOS STT script/binary (e.g., a small Swift tool using SFSpeechRecognizer, or a whisper server CLI).
    /// </summary>
    public class MacStt : ISttProvider
    {
        readonly string _sttCommand;
        readonly string _argumentsFormat;

        /// <param name="sttCommand">Executable that performs STT (defaults to empty; you should set this to your macOS STT helper).</param>
        /// <param name="argumentsFormat">Format string for arguments; should contain {0} for WAV path.</param>
        public MacStt(string sttCommand = "", string argumentsFormat = "{0}")
        {
            _sttCommand = sttCommand ?? string.Empty;
            _argumentsFormat = string.IsNullOrWhiteSpace(argumentsFormat) ? "{0}" : argumentsFormat;
        }

        public async Task<string> TranscribeAsync(AudioClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            string tempWav = Path.Combine(Application.temporaryCachePath, $"stt_{DateTime.UtcNow.Ticks}.wav");
            try
            {
                WriteWav(tempWav, clip);
                return await TranscribeAsync(tempWav);
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

        public async Task<string> TranscribeAsync(string wavPath)
        {
            if (string.IsNullOrWhiteSpace(_sttCommand))
            {
                UnityEngine.Debug.LogWarning("MacStt: No STT command configured. Please set a macOS STT helper executable.");
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
            {
                UnityEngine.Debug.LogError($"MacStt: WAV path not found: {wavPath}");
                return string.Empty;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _sttCommand,
                        Arguments = string.Format(CultureInfo.InvariantCulture, _argumentsFormat, wavPath),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        if (proc.ExitCode != 0)
                        {
                            UnityEngine.Debug.LogError($"MacStt: STT process failed ({proc.ExitCode}). Stderr: {stderr}");
                            return string.Empty;
                        }
                        return output.Trim();
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"MacStt: Failed to run STT command: {ex.Message}");
                    return string.Empty;
                }
            });
        }

        void WriteWav(string path, AudioClip clip)
        {
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
