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
        public const float FadeSeconds = 0.4f;
        public const float BlipCooldownSeconds = 0.045f;
        public const float MinimumPitch = 0.5f;
        public const float MaximumPitch = 2f;

        [AutoStaticsCleanup]
        private static GameAudio active;

        [SerializeField] private AudioLibrarySO library;

        private readonly Dictionary<AudioBus, float> volumes = new()
        {
            { AudioBus.Music, 1f },
            { AudioBus.Sfx, 1f },
            { AudioBus.Ui, 1f }
        };

        private AudioSource music;
        private AudioSource[] effects;
        private AudioSource[] interfaceSounds;
        private int nextEffect;
        private int nextInterface;
        private string playing;
        private float blipCooldown;

        public static GameAudio Active => active;

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

        public static void PlayMusic(string id)
        {
            if (active)
            {
                active.ChangeMusic(id);
            }
        }

        public static void StopMusic()
        {
            if (active)
            {
                active.ChangeMusic(null);
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
            if (bus == AudioBus.Music && music && !music.isPlaying)
            {
                music.volume = volumes[AudioBus.Music];
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

            if (music && music.isPlaying && !Mathf.Approximately(music.volume, volumes[AudioBus.Music]))
            {
                music.volume = Mathf.MoveTowards(music.volume, volumes[AudioBus.Music],
                    Time.unscaledDeltaTime / FadeSeconds);
            }
        }

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
            var clip = library ? library.Get(id) : null;
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

        private void ChangeMusic(string id)
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

            var clip = id != null && library ? library.Get(id) : null;
            music.Stop();
            music.clip = clip;
            if (clip == null)
            {
                return;
            }

            music.volume = 0f;
            music.Play();
        }
    }
}
