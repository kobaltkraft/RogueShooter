using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RogueArena.Audio;
using RogueArena.Core;

namespace RogueArena.Persistence
{
    /// <summary>
    /// Applies and persists player settings. All changes take effect immediately and
    /// are autosaved (debounced) by <see cref="SaveService"/>.
    /// </summary>
    public class SettingsService
    {
        public SettingsData Data => save.Data.settings;

        readonly SaveService save;
        public event Action Applied;

        public SettingsService(SaveService saveService)
        {
            save = saveService;
        }

        /// <summary>Called once after the audio director exists, and after any load.</summary>
        public void ApplyAll()
        {
            var s = Data;
            ApplyVolumes();
            ApplyQuality();
            Applied?.Invoke();
        }

        // ------------------------------------------------------------------ volumes

        public void SetVolume(AudioCategory category, float value01)
        {
            value01 = Mathf.Clamp01(value01);
            switch (category)
            {
                case AudioCategory.Master: Data.masterVolume = value01; break;
                case AudioCategory.Music: Data.musicVolume = value01; break;
                case AudioCategory.Sfx: Data.sfxVolume = value01; break;
                case AudioCategory.Ui: Data.uiVolume = value01; break;
                case AudioCategory.Voice: Data.voiceVolume = value01; break;
            }
            ApplyVolumes();
            save.MarkDirty();
        }

        public float GetVolume(AudioCategory category) => category switch
        {
            AudioCategory.Master => Data.masterVolume,
            AudioCategory.Music => Data.musicVolume,
            AudioCategory.Sfx => Data.sfxVolume,
            AudioCategory.Ui => Data.uiVolume,
            AudioCategory.Voice => Data.voiceVolume,
            _ => 1f,
        };

        void ApplyVolumes()
        {
            var audio = Services.Audio;
            if (audio == null) return;
            audio.SetCategoryVolume(AudioCategory.Master, Data.masterVolume);
            audio.SetCategoryVolume(AudioCategory.Music, Data.musicVolume);
            audio.SetCategoryVolume(AudioCategory.Sfx, Data.sfxVolume);
            audio.SetCategoryVolume(AudioCategory.Ui, Data.uiVolume);
            audio.SetCategoryVolume(AudioCategory.Voice, Data.voiceVolume);
        }

        // ------------------------------------------------------------------ gameplay

        public void SetSensitivity(float multiplier) { Data.mouseSensitivity = Mathf.Clamp(multiplier, .05f, 6f); save.MarkDirty(); Applied?.Invoke(); }
        public void SetFieldOfView(float fov) { Data.fieldOfView = Mathf.Clamp(fov, 60f, 110f); save.MarkDirty(); Applied?.Invoke(); }
        public void SetInvertY(bool value) { Data.invertY = value; save.MarkDirty(); Applied?.Invoke(); }
        public void SetHeadBob(bool value) { Data.headBob = value; save.MarkDirty(); Applied?.Invoke(); }
        public void SetScreenShake(bool value) { Data.screenShake = value; save.MarkDirty(); Applied?.Invoke(); }
        public void SetShowFps(bool value) { Data.showFps = value; save.MarkDirty(); Applied?.Invoke(); }
        public void SetCrosshair(string id) { Data.crosshair = id; save.MarkDirty(); Applied?.Invoke(); }

        // ------------------------------------------------------------------ quality

        public void SetQuality(int quality)
        {
            Data.quality = Mathf.Clamp(quality, 0, 2);
            ApplyQuality();
            save.MarkDirty();
        }

        void ApplyQuality()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null) return; // running on the built-in fallback; nothing to tune

            try
            {
                switch (Data.quality)
                {
                    case 0: // low
                        asset.renderScale = .75f;
                        asset.msaaSampleCount = 1;
                        asset.shadowDistance = 30f;
                        break;
                    case 1: // medium
                        asset.renderScale = 1f;
                        asset.msaaSampleCount = 2;
                        asset.shadowDistance = 50f;
                        break;
                    default: // high
                        asset.renderScale = 1f;
                        asset.msaaSampleCount = 4;
                        asset.shadowDistance = 70f;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Settings] Could not apply render quality: " + e.Message);
            }
        }
    }
}
