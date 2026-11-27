using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoinRush.Audio
{
    /// <summary>
    /// Procedural electronic synthesizer that generates arcade SFX AudioClips at runtime.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SynthAudioManager : MonoBehaviour
    {
        public static SynthAudioManager Instance { get; private set; }

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float opponentCoinVolumeScale = 0.5f;

        private const int SampleRate = 44100;

        private AudioSource _audioSource;
        private readonly List<AudioClip> _generatedClips = new List<AudioClip>();

        private AudioClip _playerCoinClip;
        private AudioClip _opponentCoinClip;
        private AudioClip _countdownTickClip;
        private AudioClip _matchGoClip;
        private AudioClip _urgentTickClip;
        private AudioClip _bumpClip;
        private AudioClip _winFanfareClip;
        private AudioClip _loseFanfareClip;
        private AudioClip _drawFanfareClip;
        private AudioClip _uiClickClip;

        private float _lastBumpTime = -10f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;

            BuildAllSynthClips();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _generatedClips.Count; i++)
            {
                if (_generatedClips[i] != null)
                {
                    Destroy(_generatedClips[i]);
                }
            }
            _generatedClips.Clear();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void PlayCoinPickup(bool isLocalPlayer)
        {
            AudioClip clip = isLocalPlayer ? _playerCoinClip : _opponentCoinClip;
            float vol = isLocalPlayer ? masterVolume : masterVolume * opponentCoinVolumeScale;
            PlayClip(clip, vol);
        }

        public void PlayCountdownTick() => PlayClip(_countdownTickClip, masterVolume * 0.85f);
        public void PlayMatchGo() => PlayClip(_matchGoClip, masterVolume);
        public void PlayUrgentTick() => PlayClip(_urgentTickClip, masterVolume * 0.65f);
        public void PlayUiClick() => PlayClip(_uiClickClip, masterVolume * 0.8f);

        public void PlayBump()
        {
            if (Time.unscaledTime - _lastBumpTime < 0.25f)
            {
                return;
            }
            _lastBumpTime = Time.unscaledTime;
            PlayClip(_bumpClip, masterVolume * 0.75f);
        }

        public void PlayMatchEnd(int player1Score, int player2Score)
        {
            if (player1Score > player2Score)
            {
                PlayClip(_winFanfareClip, masterVolume);
            }
            else if (player2Score > player1Score)
            {
                PlayClip(_loseFanfareClip, masterVolume);
            }
            else
            {
                PlayClip(_drawFanfareClip, masterVolume);
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (_audioSource == null || clip == null || volume <= 0f)
            {
                return;
            }
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void BuildAllSynthClips()
        {
            // Bright B5 -> E6 electronic coin chime for Player 1
            _playerCoinClip = CreateSequenceClip(
                "Synth_CoinP1",
                new[]
                {
                    new SynthNote(987.77f, 987.77f, 0.065f, SynthWave.SoftSquare, 0.42f, 12f),
                    new SynthNote(1318.51f, 1318.51f, 0.14f, SynthWave.SoftSquare, 0.44f, 10f)
                }
            );

            // Slightly lower E5 -> A5 chime for opponent/AI
            _opponentCoinClip = CreateSequenceClip(
                "Synth_CoinP2",
                new[]
                {
                    new SynthNote(659.25f, 659.25f, 0.055f, SynthWave.Triangle, 0.38f, 14f),
                    new SynthNote(880.00f, 880.00f, 0.11f, SynthWave.Triangle, 0.38f, 12f)
                }
            );

            // Countdown 3, 2, 1 clean electronic tone
            _countdownTickClip = CreateSequenceClip(
                "Synth_CountdownTick",
                new[]
                {
                    new SynthNote(523.25f, 523.25f, 0.14f, SynthWave.Triangle, 0.45f, 11f)
                }
            );

            // Energetic rising C5 -> G5 -> C6 GO! synth
            _matchGoClip = CreateSequenceClip(
                "Synth_MatchGo",
                new[]
                {
                    new SynthNote(523.25f, 523.25f, 0.07f, SynthWave.SoftSquare, 0.40f, 8f),
                    new SynthNote(783.99f, 783.99f, 0.07f, SynthWave.SoftSquare, 0.42f, 8f),
                    new SynthNote(1046.50f, 1046.50f, 0.24f, SynthWave.SoftSquare, 0.46f, 6.5f)
                }
            );

            // Urgent last-10-seconds clock pip
            _urgentTickClip = CreateSequenceClip(
                "Synth_UrgentTick",
                new[]
                {
                    new SynthNote(1174.66f, 1046.50f, 0.06f, SynthWave.Sine, 0.36f, 22f)
                }
            );

            // Low punchy thump for player-to-player collision
            _bumpClip = CreateSequenceClip(
                "Synth_Bump",
                new[]
                {
                    new SynthNote(165f, 62f, 0.10f, SynthWave.Triangle, 0.55f, 16f)
                }
            );

            // Victory arpeggio C5 - E5 - G5 - C6
            _winFanfareClip = CreateSequenceClip(
                "Synth_Win",
                new[]
                {
                    new SynthNote(523.25f, 523.25f, 0.09f, SynthWave.SoftSquare, 0.42f, 7f),
                    new SynthNote(659.25f, 659.25f, 0.09f, SynthWave.SoftSquare, 0.42f, 7f),
                    new SynthNote(783.99f, 783.99f, 0.09f, SynthWave.SoftSquare, 0.42f, 7f),
                    new SynthNote(1046.50f, 1046.50f, 0.34f, SynthWave.SoftSquare, 0.48f, 4.5f)
                }
            );

            // Defeat descending synth G4 - Eb4 - C4
            _loseFanfareClip = CreateSequenceClip(
                "Synth_Lose",
                new[]
                {
                    new SynthNote(392.00f, 392.00f, 0.13f, SynthWave.SawSoft, 0.34f, 6f),
                    new SynthNote(311.13f, 311.13f, 0.13f, SynthWave.SawSoft, 0.34f, 6f),
                    new SynthNote(261.63f, 246.94f, 0.34f, SynthWave.SawSoft, 0.36f, 4.5f)
                }
            );

            // Draw neutral double chime
            _drawFanfareClip = CreateSequenceClip(
                "Synth_Draw",
                new[]
                {
                    new SynthNote(659.25f, 659.25f, 0.12f, SynthWave.Triangle, 0.42f, 8f),
                    new SynthNote(659.25f, 659.25f, 0.24f, SynthWave.Triangle, 0.42f, 6f)
                }
            );

            // Crisp UI button click
            _uiClickClip = CreateSequenceClip(
                "Synth_UiClick",
                new[]
                {
                    new SynthNote(780f, 1180f, 0.055f, SynthWave.Sine, 0.40f, 24f)
                }
            );
        }

        private AudioClip CreateSequenceClip(string clipName, SynthNote[] notes)
        {
            int totalSamples = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                totalSamples += Mathf.Max(1, Mathf.RoundToInt(notes[i].Duration * SampleRate));
            }

            float[] samples = new float[totalSamples];
            int writeIndex = 0;
            float phase = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                SynthNote note = notes[n];
                int noteSamples = Mathf.Max(1, Mathf.RoundToInt(note.Duration * SampleRate));
                int attackSamples = Mathf.Min(noteSamples / 4, Mathf.RoundToInt(0.005f * SampleRate));
                int releaseSamples = Mathf.Min(noteSamples / 3, Mathf.RoundToInt(0.012f * SampleRate));

                for (int i = 0; i < noteSamples; i++)
                {
                    float tNorm = (float)i / noteSamples;
                    float tSec = (float)i / SampleRate;
                    float freq = Mathf.Lerp(note.StartFreq, note.EndFreq, tNorm);

                    phase += 2f * Mathf.PI * freq / SampleRate;
                    if (phase > 2f * Mathf.PI)
                    {
                        phase -= 2f * Mathf.PI;
                    }

                    float raw = EvaluateWave(note.Wave, phase);
                    float env = Mathf.Exp(-note.DecayRate * tSec);

                    if (i < attackSamples && attackSamples > 0)
                    {
                        env *= (float)i / attackSamples;
                    }
                    int distFromEnd = noteSamples - 1 - i;
                    if (distFromEnd < releaseSamples && releaseSamples > 0)
                    {
                        env *= (float)distFromEnd / releaseSamples;
                    }

                    samples[writeIndex++] = Mathf.Clamp(raw * env * note.Amplitude, -0.95f, 0.95f);
                }
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            _generatedClips.Add(clip);
            return clip;
        }

        private static float EvaluateWave(SynthWave wave, float phase)
        {
            switch (wave)
            {
                case SynthWave.Sine:
                    return Mathf.Sin(phase);

                case SynthWave.Triangle:
                    return 2f * Mathf.Abs(2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f))) - 1f;

                case SynthWave.SoftSquare:
                    // Fundamental + softened 3rd & 5th harmonics for warm chiptune timbre
                    return 0.72f * Mathf.Sin(phase) + 0.22f * Mathf.Sin(3f * phase) + 0.08f * Mathf.Sin(5f * phase);

                case SynthWave.SawSoft:
                    return 0.65f * Mathf.Sin(phase) - 0.25f * Mathf.Sin(2f * phase) + 0.12f * Mathf.Sin(3f * phase);

                default:
                    return Mathf.Sin(phase);
            }
        }

        private enum SynthWave
        {
            Sine,
            Triangle,
            SoftSquare,
            SawSoft
        }

        private readonly struct SynthNote
        {
            public readonly float StartFreq;
            public readonly float EndFreq;
            public readonly float Duration;
            public readonly SynthWave Wave;
            public readonly float Amplitude;
            public readonly float DecayRate;

            public SynthNote(float startFreq, float endFreq, float duration, SynthWave wave, float amplitude, float decayRate)
            {
                StartFreq = startFreq;
                EndFreq = endFreq;
                Duration = duration;
                Wave = wave;
                Amplitude = amplitude;
                DecayRate = decayRate;
            }
        }
    }
}
