using System.Collections.Generic;
using UnityEngine;
using RogueArena.Core;

namespace RogueArena.Audio
{
    public enum AudioCategory { Master, Music, Sfx, Ui, Voice }

    /// <summary>
    /// Central audio playback with category volumes, basic voice limiting and
    /// duplicate-trigger suppression so huge fights never collapse into noise.
    /// All clips come from <see cref="SfxSynth"/>; missing ids degrade to silence
    /// with a single warning instead of errors.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        const int VoiceCount = 18;
        const float DuplicateInterval = .04f;   // same clip retrigger window
        const int MaxConcurrentSfx = 11;

        struct Voice
        {
            public AudioSource Source;
            public AudioCategory Category;
            public string ClipId;
        }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(96);
        readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>(96);
        readonly Voice[] voices = new Voice[VoiceCount];
        readonly float[] categoryVolume = { 1f, 1f, 1f, 1f, 1f };

        AudioSource musicA;
        AudioSource musicB;
        AudioSource uiSource;
        AudioSource voiceSource;
        bool musicOnA = true;

        enum MusicState { Idle, Steady, Crossfading, FadingOut }
        MusicState musicState = MusicState.Idle;
        string currentMusicId;
        string pendingMusicId;
        float musicFade;          // 0..1 progress of the active fade
        float musicFadeDuration = 1f;

        void Awake()
        {
            gameObject.name = "Audio Director";
            BuildSources();
        }

        void BuildSources()
        {
            musicA = CreateSource("Music A", spatial: false, loop: true, priority: 0);
            musicB = CreateSource("Music B", spatial: false, loop: true, priority: 0);
            uiSource = CreateSource("UI", spatial: false, loop: false, priority: 96);
            voiceSource = CreateSource("Voice", spatial: false, loop: false, priority: 64);
            uiSource.playOnAwake = false;
            voiceSource.playOnAwake = false;

            for (int i = 0; i < VoiceCount; i++)
            {
                var src = CreateSource("Voice " + i, spatial: true, loop: false, priority: 128 + (i % 4) * 32);
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 45f;
                voices[i].Source = src;
                voices[i].Category = AudioCategory.Sfx;
                voices[i].ClipId = null;
            }
        }

        AudioSource CreateSource(string name, bool spatial, bool loop, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatial ? 1f : 0f;
            src.priority = priority;
            src.dopplerLevel = 0f;
            return src;
        }

        void Update()
        {
            UpdateMusicCrossfade();
        }

        // ---------------------------------------------------------------- clips

        public AudioClip GetClip(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (clips.TryGetValue(id, out AudioClip clip)) return clip;

            clip = SfxSynth.Create(id);
            clips[id] = clip;
            return clip;
        }

        /// <summary>Preloads a set of clips so first playback never hitches.</summary>
        public void Preload(IEnumerable<string> ids)
        {
            foreach (string id in ids) GetClip(id);
        }

        // ---------------------------------------------------------------- volumes

        public void SetCategoryVolume(AudioCategory category, float volume01)
        {
            categoryVolume[(int)category] = Mathf.Clamp01(volume01);
            ApplyVolumes();
        }

        public float GetCategoryVolume(AudioCategory category) => categoryVolume[(int)category];

        void ApplyVolumes()
        {
            float master = categoryVolume[(int)AudioCategory.Master];
            musicA.volume = master * categoryVolume[(int)AudioCategory.Music];
            musicB.volume = master * categoryVolume[(int)AudioCategory.Music];
            uiSource.volume = master * categoryVolume[(int)AudioCategory.Ui];
            voiceSource.volume = master * categoryVolume[(int)AudioCategory.Voice];
        }

        // ---------------------------------------------------------------- playback

        /// <summary>Plays a one-shot. World position makes it 3D; omit it for flat UI audio.</summary>
        public void Play(string clipId, float volume = 1f, float pitch = 1f, Vector3? position = null)
        {
            if (string.IsNullOrEmpty(clipId)) return;
            if (categoryVolume[(int)AudioCategory.Master] <= 0f) return;

            AudioCategory category = CategoryOf(clipId);

            // Duplicate suppression: identical clips fired within the window are dropped.
            if (lastPlayTime.TryGetValue(clipId, out float last) && Time.unscaledTime - last < DuplicateInterval)
                return;

            // Voice limiting for SFX: skip when too many are already sounding.
            if (category == AudioCategory.Sfx && CountActive(AudioCategory.Sfx) >= MaxConcurrentSfx)
                return;

            switch (category)
            {
                case AudioCategory.Ui:
                    PlayFlat(uiSource, clipId, volume, pitch);
                    break;
                case AudioCategory.Voice:
                    PlayFlat(voiceSource, clipId, volume, pitch);
                    break;
                case AudioCategory.Music:
                    // Music ids route through PlayMusic; one-shot stingers still play here.
                    PlayFlat(musicOnA ? musicA : musicB, clipId, volume, pitch, oneShot: true);
                    break;
                default:
                    PlayWorld(clipId, volume, pitch, position);
                    break;
            }

            lastPlayTime[clipId] = Time.unscaledTime;
        }

        void PlayFlat(AudioSource target, string clipId, float volume, float pitch, bool oneShot = false)
        {
            AudioClip clip = GetClip(clipId);
            if (clip == null) return;
            float master = categoryVolume[(int)AudioCategory.Master];
            float cat = categoryVolume[(int)CategoryOf(clipId)];
            if (oneShot) target.PlayOneShot(clip, volume * cat * master);
            else
            {
                target.pitch = pitch;
                target.PlayOneShot(clip, volume * cat * master);
            }
        }

        void PlayWorld(string clipId, float volume, float pitch, Vector3? position)
        {
            AudioClip clip = GetClip(clipId);
            if (clip == null) return;

            int index = FindFreeVoice();
            if (index < 0) return; // every voice busy - drop politely

            var voice = voices[index];
            voice.Source.transform.position = position ?? Vector3.zero;
            voice.Source.pitch = pitch;
            voice.Source.clip = clip;
            voice.Source.volume = volume;
            voice.Source.spatialBlend = position.HasValue ? 1f : 0f;
            voice.Source.Play();
            voice.Category = AudioCategory.Sfx;
            voice.ClipId = clipId;
            voices[index] = voice;
        }

        int FindFreeVoice()
        {
            // Prefer a finished voice; otherwise steal the oldest quietest one.
            for (int i = 0; i < VoiceCount; i++)
                if (!voices[i].Source.isPlaying) return i;

            int steal = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < VoiceCount; i++)
            {
                float vol = voices[i].Source.volume;
                if (vol < lowest) { lowest = vol; steal = i; }
            }
            return steal;
        }

        int CountActive(AudioCategory category)
        {
            int count = 0;
            for (int i = 0; i < VoiceCount; i++)
                if (voices[i].Category == category && voices[i].Source.isPlaying) count++;
            return count;
        }

        static AudioCategory CategoryOf(string clipId)
        {
            if (clipId.StartsWith("ui_") || clipId == "level_up" || clipId == "challenge") return AudioCategory.Ui;
            if (clipId.StartsWith("music_")) return AudioCategory.Music;
            if (clipId.StartsWith("stinger_") || clipId.StartsWith("boss_") || clipId.StartsWith("wave_")
                || clipId == "alarm" || clipId == "victory" || clipId == "defeat") return AudioCategory.Voice;
            return AudioCategory.Sfx;
        }

        // ---------------------------------------------------------------- music

        public void PlayMusic(string musicId, float fadeSeconds = 1.2f)
        {
            if (string.IsNullOrEmpty(musicId)) return;
            if (currentMusicId == musicId && musicState is MusicState.Steady or MusicState.Crossfading) return;
            if (pendingMusicId == musicId) return;

            AudioClip clip = GetClip(musicId);
            if (clip == null) { Debug.LogWarning($"[Audio] Unknown music id '{musicId}'"); return; }

            if (musicState == MusicState.Idle)
            {
                // Nothing playing - start immediately, no crossfade needed.
                musicOnA = !musicOnA;
                var src = musicOnA ? musicA : musicB;
                src.clip = clip;
                src.loop = true;
                src.volume = categoryVolume[(int)AudioCategory.Master] * categoryVolume[(int)AudioCategory.Music];
                src.Play();
                currentMusicId = musicId;
                pendingMusicId = null;
                musicState = MusicState.Steady;
                return;
            }

            pendingMusicId = musicId;
            musicFadeDuration = Mathf.Max(.1f, fadeSeconds);
            musicFade = 0f;
            musicState = MusicState.Crossfading;

            var next = musicOnA ? musicB : musicA;
            next.clip = clip;
            next.loop = true;
            next.volume = 0f;
            next.Play();
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            if (musicState != MusicState.Steady) return;
            pendingMusicId = null;
            musicFadeDuration = Mathf.Max(.1f, fadeSeconds);
            musicFade = 0f;
            musicState = MusicState.FadingOut;
        }

        void UpdateMusicCrossfade()
        {
            float master = categoryVolume[(int)AudioCategory.Master];
            float musicVol = master * categoryVolume[(int)AudioCategory.Music];
            var current = musicOnA ? musicA : musicB;
            var other = musicOnA ? musicB : musicA;

            switch (musicState)
            {
                case MusicState.Idle:
                    break;

                case MusicState.Steady:
                    current.volume = musicVol;
                    if (!current.isPlaying && current.clip != null) current.Play();
                    if (other.isPlaying) { other.Stop(); other.clip = null; }
                    break;

                case MusicState.Crossfading:
                    musicFade += Time.unscaledDeltaTime / musicFadeDuration;
                    float tIn = Mathf.Clamp01(musicFade);
                    current.volume = Mathf.Lerp(musicVol, 0f, tIn);
                    other.volume = Mathf.Lerp(0f, musicVol, tIn);
                    if (tIn >= 1f)
                    {
                        currentMusicId = pendingMusicId;
                        pendingMusicId = null;
                        current.Stop();
                        current.clip = null;
                        current.volume = 0f;
                        musicOnA = !musicOnA;
                        musicFade = 0f;
                        musicState = MusicState.Steady;
                    }
                    break;

                case MusicState.FadingOut:
                    musicFade += Time.unscaledDeltaTime / musicFadeDuration;
                    float tOut = Mathf.Clamp01(musicFade);
                    current.volume = Mathf.Lerp(musicVol, 0f, tOut);
                    if (tOut >= 1f)
                    {
                        current.Stop();
                        current.clip = null;
                        current.volume = 0f;
                        currentMusicId = null;
                        pendingMusicId = null;
                        musicFade = 0f;
                        musicState = MusicState.Idle;
                    }
                    break;
            }
        }
    }
}
