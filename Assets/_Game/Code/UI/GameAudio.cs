using System.Collections.Generic;
using Game.Content;
using Game.Core;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Game.UI
{
    public sealed partial class GameAudio : MonoBehaviour
    {
        public const string ObjectName = "GameAudio";
        public const int PoolSize = 4;
        public const float CrossfadeSeconds = 1.5f;
        public const float BattleCrossfadeSeconds = 0.6f;
        public const float StingFadeSeconds = 0.8f;
        public const float InstantFadeRate = 1_000_000f;
        public const float BlipCooldownSeconds = 0.045f;
        public const float MinimumPitch = 0.5f;
        public const float MaximumPitch = 2f;

        [AutoStaticsCleanup]
        private static GameAudio active;

        [AutoStaticsCleanup]
        private static bool muted;

        [SerializeField] private AudioLibrarySO library;

        private readonly Dictionary<AudioBus, float> volumes = new()
        {
            { AudioBus.Music, 1f },
            { AudioBus.Sfx, 1f },
            { AudioBus.Ui, 1f }
        };

        private readonly MusicResume resume = new();
        private AudioSource music;
        private AudioSource fadingOut;
        private float fadeInRate;
        private float fadeOutRate;
        private AudioSource[] effects;
        private AudioSource[] interfaceSounds;
        private int nextEffect;
        private int nextInterface;
        private string playing;
        private float blipCooldown;

        public static GameAudio Active => active;

        public static bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                if (value && active)
                {
                    active.SilenceMusic();
                }
            }
        }

        public AudioLibrarySO Library => library;

        public string PlayingMusic => playing;

        public static void Play(string id, AudioBus bus = AudioBus.Sfx)
        {
            if (active)
            {
                active.PlaySound(id, bus, AudioLibrarySO.DefaultPitch);
            }
        }

        public static void PlayUi(string id)
        {
            Play(id, AudioBus.Ui);
        }

        public static void Blip(string speakerId)
        {
            if (active)
            {
                active.PlayBlip(speakerId);
            }
        }

        public static void PlayMusic(string id, float fadeSeconds = CrossfadeSeconds)
        {
            if (active)
            {
                active.ChangeMusic(id, fadeSeconds);
            }
        }

        public static void StopMusic(float fadeSeconds = CrossfadeSeconds)
        {
            if (active)
            {
                active.ChangeMusic(null, fadeSeconds);
            }
        }

        public static void Shutdown()
        {
            if (active)
            {
                Destroy(active.gameObject);
            }

            active = null;
        }

        public void Configure(AudioLibrarySO newLibrary)
        {
            library = newLibrary;
        }

        public void SetVolume(AudioBus bus, float volume)
        {
            volumes[bus] = Mathf.Clamp01(volume);
            if (bus == AudioBus.Music && music && music.isPlaying)
            {
                music.volume = Mathf.Min(music.volume, volumes[AudioBus.Music]);
            }
        }

        public float VolumeOf(AudioBus bus) => volumes[bus];

        private void Awake()
        {
            if (active && active != this)
            {
                Destroy(gameObject);
                return;
            }

            active = this;
            DontDestroyOnLoad(gameObject);
            music = CreateSource(true);
            fadingOut = CreateSource(true);
            effects = CreatePool();
            interfaceSounds = CreatePool();
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void Update()
        {
            if (blipCooldown > 0f)
            {
                blipCooldown -= Time.unscaledDeltaTime;
            }

            FadeMusic(Time.unscaledDeltaTime);
        }

        private void FadeMusic(float deltaSeconds)
        {
            if (music && music.isPlaying)
            {
                music.volume = Mathf.MoveTowards(music.volume, volumes[AudioBus.Music], deltaSeconds * fadeInRate);
            }

            if (fadingOut && fadingOut.isPlaying)
            {
                fadingOut.volume = Mathf.MoveTowards(fadingOut.volume, 0f, deltaSeconds * fadeOutRate);
                if (fadingOut.volume <= 0f)
                {
                    fadingOut.Stop();
                    fadingOut.clip = null;
                }
            }
        }

        private static float RateFor(float fadeSeconds) => fadeSeconds > 0f ? 1f / fadeSeconds : InstantFadeRate;

        private AudioSource CreateSource(bool looping)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = looping;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private AudioSource[] CreatePool()
        {
            var pool = new AudioSource[PoolSize];
            for (var index = 0; index < PoolSize; index++)
            {
                pool[index] = CreateSource(false);
            }

            return pool;
        }

        private void PlaySound(string id, AudioBus bus, float pitch)
        {
            var clip = muted || !library ? null : library.Get(id);
            if (clip == null)
            {
                return;
            }

            var pool = bus == AudioBus.Ui ? interfaceSounds : effects;
            var index = bus == AudioBus.Ui ? nextInterface : nextEffect;
            var source = pool[index];
            if (bus == AudioBus.Ui)
            {
                nextInterface = (index + 1) % PoolSize;
            }
            else
            {
                nextEffect = (index + 1) % PoolSize;
            }

            source.pitch = Mathf.Clamp(pitch, MinimumPitch, MaximumPitch);
            source.volume = volumes[bus];
            source.PlayOneShot(clip);
        }

        private void PlayBlip(string speakerId)
        {
            if (blipCooldown > 0f)
            {
                return;
            }

            blipCooldown = BlipCooldownSeconds;
            PlaySound(AudioIds.VoiceBlip, AudioBus.Ui, library ? library.PitchFor(speakerId) : AudioLibrarySO.DefaultPitch);
        }

        private void ChangeMusic(string id, float fadeSeconds)
        {
            if (playing == id)
            {
                return;
            }

            playing = id;
            if (!music)
            {
                return;
            }

            var clip = id != null && library && !muted ? library.Get(id) : null;
            fadeInRate = RateFor(fadeSeconds);
            fadeOutRate = fadeInRate;
            if (clip != null && music.clip == clip && music.isPlaying)
            {
                return;
            }

            RememberWhereItStopped(music);
            if (clip != null && fadingOut.clip == clip && fadingOut.isPlaying)
            {
                (music, fadingOut) = (fadingOut, music);
                return;
            }

            fadingOut.Stop();
            (music, fadingOut) = (fadingOut, music);
            music.clip = clip;
            if (clip == null)
            {
                return;
            }

            music.time = resume.StartFor(clip.name, clip.length, Time.unscaledTime);
            music.volume = 0f;
            music.Play();
        }

        private void RememberWhereItStopped(AudioSource source)
        {
            if (source.isPlaying && source.clip != null)
            {
                resume.Remember(source.clip.name, source.time, Time.unscaledTime);
            }
        }

        private void SilenceMusic()
        {
            playing = null;
            if (music)
            {
                music.Stop();
                music.clip = null;
            }

            if (fadingOut)
            {
                fadingOut.Stop();
                fadingOut.clip = null;
            }
        }
    }
}
