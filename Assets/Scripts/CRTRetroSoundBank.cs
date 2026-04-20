using UnityEngine;

namespace ArchiveNull.UI
{
    [DisallowMultipleComponent]
    public sealed class CRTRetroSoundBank : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField] private int _sampleRate = 44100;
        [SerializeField] private float _masterVolume = 0.25f;

        public AudioClip BootStartClip { get; private set; }
        public AudioClip MenuOpenClip { get; private set; }
        public AudioClip MoveClip { get; private set; }
        public AudioClip ConfirmClip { get; private set; }
        public AudioClip BackClip { get; private set; }
        public AudioClip GlitchClip { get; private set; }
        public AudioClip ShutdownClip { get; private set; }

        private void Awake()
        {
            BuildBank();
        }

        [ContextMenu("Rebuild Retro Bank")]
        public void BuildBank()
        {
            BootStartClip = CreateBootHum();
            MenuOpenClip = CreateSweep("crt_menu_open", 0.09f, 540f, 860f, Waveform.Square, 0.18f);
            MoveClip = CreateDualTone("crt_move", 0.05f, 760f, 930f, Waveform.Square, 0.14f);
            ConfirmClip = CreateDualTone("crt_confirm", 0.08f, 780f, 1160f, Waveform.Square, 0.18f);
            BackClip = CreateDualTone("crt_back", 0.07f, 880f, 540f, Waveform.Square, 0.14f);
            GlitchClip = CreateNoiseBurst("crt_glitch", 0.18f, 0.28f);
            ShutdownClip = CreateShutdownTone();
        }

        private enum Waveform
        {
            Sine,
            Square,
            Triangle
        }

        private AudioClip CreateBootHum()
        {
            float length = 0.26f;
            int sampleCount = Mathf.CeilToInt(length * _sampleRate);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)_sampleRate;
                float env = Mathf.Clamp01(t / 0.04f) * (1f - Mathf.Clamp01((t - 0.18f) / 0.08f));
                float low = SampleWave(Waveform.Sine, 52f, t);
                float buzz = SampleWave(Waveform.Square, Mathf.Lerp(90f, 140f, t / length), t) * 0.45f;
                float sparkle = SampleWave(Waveform.Sine, 1200f, t) * 0.05f;
                data[i] = (low * 0.75f + buzz + sparkle) * env * _masterVolume;
            }

            return ToClip("crt_boot_start", data);
        }

        private AudioClip CreateShutdownTone()
        {
            float length = 0.15f;
            int sampleCount = Mathf.CeilToInt(length * _sampleRate);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)_sampleRate;
                float normalized = t / length;
                float freq = Mathf.Lerp(820f, 70f, normalized);
                float env = 1f - normalized;
                data[i] = SampleWave(Waveform.Sine, freq, t) * env * 0.45f * _masterVolume;
            }

            return ToClip("crt_shutdown", data);
        }

        private AudioClip CreateSweep(string name, float length, float startFreq, float endFreq, Waveform wave, float gain)
        {
            int sampleCount = Mathf.CeilToInt(length * _sampleRate);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)_sampleRate;
                float normalized = t / length;
                float freq = Mathf.Lerp(startFreq, endFreq, normalized);
                float env = Mathf.Sin(normalized * Mathf.PI);
                data[i] = SampleWave(wave, freq, t) * env * gain * _masterVolume;
            }

            return ToClip(name, data);
        }

        private AudioClip CreateDualTone(string name, float length, float firstFreq, float secondFreq, Waveform wave, float gain)
        {
            int sampleCount = Mathf.CeilToInt(length * _sampleRate);
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)_sampleRate;
                float normalized = t / length;
                float env = Mathf.Sin(normalized * Mathf.PI);
                float split = normalized < 0.55f ? 0f : 1f;
                float freq = split < 0.5f ? firstFreq : secondFreq;
                float layer = SampleWave(wave, freq, t);
                float accent = SampleWave(Waveform.Sine, freq * 0.5f, t) * 0.3f;
                data[i] = (layer + accent) * env * gain * _masterVolume;
            }

            return ToClip(name, data);
        }

        private AudioClip CreateNoiseBurst(string name, float length, float gain)
        {
            int sampleCount = Mathf.CeilToInt(length * _sampleRate);
            float[] data = new float[sampleCount];
            float last = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)_sampleRate;
                float normalized = t / length;
                float env = 1f - normalized;
                float white = Random.Range(-1f, 1f);
                float filtered = Mathf.Lerp(last, white, 0.55f);
                last = filtered;
                float squeal = SampleWave(Waveform.Sine, Mathf.Lerp(1800f, 240f, normalized), t) * 0.25f;
                data[i] = (filtered * 0.85f + squeal) * env * gain * _masterVolume;
            }

            return ToClip(name, data);
        }

        private static float SampleWave(Waveform waveform, float frequency, float time)
        {
            float phase = time * frequency * Mathf.PI * 2f;
            return waveform switch
            {
                Waveform.Sine => Mathf.Sin(phase),
                Waveform.Square => Mathf.Sign(Mathf.Sin(phase)),
                Waveform.Triangle => Mathf.PingPong(time * frequency, 1f) * 4f - 1f,
                _ => 0f
            };
        }

        private AudioClip ToClip(string clipName, float[] data)
        {
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, _sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
