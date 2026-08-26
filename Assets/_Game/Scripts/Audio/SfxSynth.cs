using System.Collections.Generic;
using UnityEngine;

namespace RogueArena.Audio
{
    /// <summary>
    /// Procedural sound bank. Every clip in the game is synthesised at runtime from
    /// deterministic math - no external audio assets required, and every sound is
    /// original. Clips are cached by <see cref="AudioDirector"/>.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 22050;

        // ---------------------------------------------------------------- helpers

        static float[] NewBuffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static void AddTone(float[] buffer, int startSample, int lengthSamples,
            float freqStart, float freqEnd, float amplitude, WaveForm wave, float decay, float attack = .002f)
        {
            float phase = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                int idx = startSample + i;
                if (idx < 0 || idx >= buffer.Length) break;
                float t = i / (float)lengthSamples;
                float freq = Mathf.Lerp(freqStart, freqEnd, t);
                phase += freq / Rate;
                float sample = wave switch
                {
                    WaveForm.Sine => Mathf.Sin(phase * Mathf.PI * 2f),
                    WaveForm.Square => Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)) * .6f,
                    WaveForm.Saw => (Mathf.Repeat(phase, 1f) * 2f - 1f) * .7f,
                    WaveForm.Triangle => Mathf.PingPong(phase * 2f, 2f) - 1f,
                    _ => 0f,
                };
                float env = Mathf.Min(i / (attack * Rate), 1f) * Mathf.Exp(-t * decay);
                buffer[idx] += sample * amplitude * env;
            }
        }

        static void AddNoise(float[] buffer, int startSample, int lengthSamples,
            float amplitude, float decay, float attack = 0f, int seed = 1)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < lengthSamples; i++)
            {
                int idx = startSample + i;
                if (idx < 0 || idx >= buffer.Length) break;
                float t = i / (float)lengthSamples;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Min(i / (attack * Rate + 1f), 1f) * Mathf.Exp(-t * decay);
                buffer[idx] += noise * amplitude * env;
            }
        }

        static void AddKick(float[] buffer, int startSample, float amplitude)
        {
            int len = Mathf.Min((int)(.18f * Rate), buffer.Length - startSample);
            AddTone(buffer, startSample, len, 130f, 42f, amplitude, WaveForm.Sine, 9f, attack: .0005f);
            AddNoise(buffer, startSample, (int)(.01f * Rate), amplitude * .3f, 60f);
        }

        static void LowPass(float[] buffer, float alpha)
        {
            float last = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                last += alpha * (buffer[i] - last);
                buffer[i] = last;
            }
        }

        static void Distort(float[] buffer, float drive)
        {
            float driveOut = (float)System.Math.Tanh(drive);
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (float)System.Math.Tanh(buffer[i] * drive) / driveOut;
        }

        static float Peak(float[] buffer)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            return peak;
        }

        static AudioClip Bake(string name, float[] buffer, float targetPeak = .9f)
        {
            float peak = Peak(buffer);
            float gain = peak > .0001f ? targetPeak / peak : 0f;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;

            var clip = AudioClip.Create(name, buffer.Length, 1, Rate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        enum WaveForm { Sine, Square, Saw, Triangle }

        // ---------------------------------------------------------------- bank

        /// <summary>Builds a clip by id. Returns a short silence placeholder for unknown ids.</summary>
        public static AudioClip Create(string id)
        {
            switch (id)
            {
                // ---------- UI ----------
                case "ui_click": return UiClick();
                case "ui_hover": return UiHover();
                case "ui_back": return UiBack();
                case "level_up": return LevelUp();
                case "challenge": return ChallengeSting();
                case "alarm": return Alarm();

                // ---------- player ----------
                case "player_hurt": return PlayerHurt();
                case "player_heal": return PlayerHeal();
                case "player_shield": return PlayerShield();
                case "footstep": return Footstep();
                case "jump": return Jump();
                case "land": return Land();
                case "dash": return Dash();
                case "teleport": return Teleport();
                case "door": return Door();

                // ---------- weapons ----------
                case "shot_rifle": return RifleShot();
                case "shot_smg": return SmgShot();
                case "shot_burst": return BurstShot();
                case "shot_shotgun": return ShotgunBlast();
                case "shot_marksman": return MarksmanShot();
                case "shot_plasma": return PlasmaShot();
                case "shot_rocket": return RocketLaunch();
                case "shot_grenade": return GrenadeLaunch();
                case "reload_start": return ReloadStart();
                case "reload_end": return ReloadEnd();
                case "dry_fire": return DryFire();

                // ---------- impacts & feedback ----------
                case "hit": return HitConfirm();
                case "hit_head": return HeadshotHit();
                case "kill": return KillConfirm();
                case "impact_bullet": return BulletImpact();
                case "impact_energy": return EnergyImpact();
                case "crate_break": return CrateBreak();
                case "glass_break": return GlassBreak();

                // ---------- enemies ----------
                case "enemy_shot": return EnemyShot();
                case "enemy_shot_heavy": return EnemyShotHeavy();
                case "enemy_alert": return EnemyAlert();
                case "enemy_die": return EnemyDeath();
                case "enemy_die_big": return EnemyDeathBig();
                case "enemy_melee": return EnemyMelee();
                case "heal_beam": return HealBeam();
                case "turret_shot": return TurretShot();
                case "turret_die": return TurretDie();
                case "electric": return Electric();

                // ---------- explosions ----------
                case "explosion": return Explosion();
                case "explosion_big": return ExplosionBig();

                // ---------- pickups ----------
                case "pickup_weapon": return PickupWeapon();
                case "pickup_ammo": return PickupAmmo();
                case "pickup_health": return PickupHealth();
                case "pickup_powerup": return PickupPowerup();

                // ---------- boss ----------
                case "boss_spawn": return BossSpawn();
                case "boss_roar": return BossRoar();
                case "boss_phase": return BossPhase();
                case "boss_die": return BossDie();
                case "boss_charge": return BossCharge();
                case "boss_slam": return BossSlam();

                // ---------- waves ----------
                case "wave_start": return WaveStart();
                case "wave_clear": return WaveClear();

                // ---------- music & stingers ----------
                case "music_menu": return MusicMenu();
                case "music_combat": return MusicCombat();
                case "music_boss": return MusicBoss();
                case "stinger_victory": return StingerVictory();
                case "stinger_defeat": return StingerDefeat();

                // Runtime aliases: GameFlow/Progression play bare ids.
                case "victory": return StingerVictory();
                case "defeat": return StingerDefeat();
                case "unlock": return Unlock();

                default:
                    Debug.LogWarning($"[SfxSynth] Unknown clip id '{id}', using silence.");
                    return Bake("silence", new float[220]);
            }
        }

        // ---------------------------------------------------------------- recipes

        static AudioClip UiClick()
        {
            var b = NewBuffer(.09f);
            AddTone(b, 0, b.Length, 1250f, 600f, .8f, WaveForm.Square, 14f, attack: .001f);
            return Bake("ui_click", b);
        }

        static AudioClip UiHover()
        {
            var b = NewBuffer(.05f);
            AddTone(b, 0, b.Length, 1800f, 1900f, .25f, WaveForm.Sine, 20f);
            return Bake("ui_hover", b);
        }

        static AudioClip UiBack()
        {
            var b = NewBuffer(.11f);
            AddTone(b, 0, b.Length, 700f, 320f, .6f, WaveForm.Square, 12f);
            return Bake("ui_back", b);
        }

        static AudioClip LevelUp()
        {
            var b = NewBuffer(.7f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
                AddTone(b, (int)(i * .09f * Rate), (int)(.24f * Rate), notes[i], notes[i], .5f, WaveForm.Triangle, 4f);
            return Bake("level_up", b);
        }

        static AudioClip ChallengeSting()
        {
            var b = NewBuffer(.5f);
            AddTone(b, 0, (int)(.4f * Rate), 880f, 880f, .35f, WaveForm.Sine, 5f);
            AddTone(b, (int)(.12f * Rate), (int)(.3f * Rate), 1318.5f, 1318.5f, .3f, WaveForm.Triangle, 5f);
            return Bake("challenge", b);
        }

        static AudioClip Alarm()
        {
            var b = NewBuffer(.8f);
            for (int i = 0; i < 4; i++)
                AddTone(b, (int)(i * .2f * Rate), (int)(.12f * Rate), 640f, 880f, .5f, WaveForm.Square, 3f);
            return Bake("alarm", b);
        }

        static AudioClip PlayerHurt()
        {
            var b = NewBuffer(.22f);
            AddTone(b, 0, b.Length, 210f, 90f, .8f, WaveForm.Saw, 8f);
            AddNoise(b, 0, (int)(.05f * Rate), .3f, 20f);
            LowPass(b, .35f);
            return Bake("player_hurt", b);
        }

        static AudioClip PlayerHeal()
        {
            var b = NewBuffer(.3f);
            AddTone(b, 0, (int)(.14f * Rate), 523f, 784f, .4f, WaveForm.Sine, 4f);
            AddTone(b, (int)(.12f * Rate), (int)(.16f * Rate), 784f, 1046f, .3f, WaveForm.Sine, 4f);
            return Bake("player_heal", b);
        }

        static AudioClip PlayerShield()
        {
            var b = NewBuffer(.34f);
            AddTone(b, 0, b.Length, 300f, 900f, .35f, WaveForm.Triangle, 3f);
            return Bake("player_shield", b);
        }

        static AudioClip Footstep()
        {
            var b = NewBuffer(.08f);
            AddNoise(b, 0, b.Length, .8f, 26f);
            LowPass(b, .18f);
            return Bake("footstep", b, .55f);
        }

        static AudioClip Jump()
        {
            var b = NewBuffer(.14f);
            AddTone(b, 0, b.Length, 300f, 560f, .35f, WaveForm.Triangle, 7f);
            return Bake("jump", b, .5f);
        }

        static AudioClip Land()
        {
            var b = NewBuffer(.16f);
            AddNoise(b, 0, b.Length, .9f, 16f);
            AddTone(b, 0, (int)(.07f * Rate), 120f, 60f, .5f, WaveForm.Sine, 10f);
            LowPass(b, .25f);
            return Bake("land", b, .7f);
        }

        static AudioClip Dash()
        {
            var b = NewBuffer(.3f);
            AddNoise(b, 0, b.Length, .7f, 7f, attack: .01f);
            AddTone(b, 0, (int)(.28f * Rate), 1400f, 200f, .25f, WaveForm.Saw, 4f);
            LowPass(b, .5f);
            return Bake("dash", b, .75f);
        }

        static AudioClip Teleport()
        {
            var b = NewBuffer(.35f);
            AddTone(b, 0, (int)(.14f * Rate), 1800f, 120f, .4f, WaveForm.Sine, 4f);
            AddTone(b, (int)(.16f * Rate), (int)(.16f * Rate), 120f, 2200f, .35f, WaveForm.Sine, 4f);
            return Bake("teleport", b, .6f);
        }

        static AudioClip Door()
        {
            var b = NewBuffer(.5f);
            AddNoise(b, 0, (int)(.45f * Rate), .3f, 4f, attack: .05f);
            AddTone(b, 0, (int)(.45f * Rate), 90f, 70f, .3f, WaveForm.Saw, 1.5f);
            LowPass(b, .2f);
            return Bake("door", b, .6f);
        }

        static AudioClip RifleShot()
        {
            var b = NewBuffer(.16f);
            AddNoise(b, 0, (int)(.05f * Rate), 1f, 22f);
            AddTone(b, 0, (int)(.06f * Rate), 240f, 70f, .8f, WaveForm.Saw, 16f);
            LowPass(b, .55f);
            Distort(b, 1.6f);
            return Bake("shot_rifle", b, .8f);
        }

        static AudioClip SmgShot()
        {
            var b = NewBuffer(.11f);
            AddNoise(b, 0, (int)(.035f * Rate), 1f, 26f);
            AddTone(b, 0, (int)(.04f * Rate), 300f, 110f, .7f, WaveForm.Saw, 18f);
            LowPass(b, .6f);
            return Bake("shot_smg", b, .65f);
        }

        static AudioClip BurstShot()
        {
            var b = NewBuffer(.18f);
            AddNoise(b, 0, (int)(.04f * Rate), 1f, 24f);
            AddTone(b, 0, (int)(.05f * Rate), 200f, 80f, .85f, WaveForm.Square, 16f);
            LowPass(b, .5f);
            Distort(b, 1.4f);
            return Bake("shot_burst", b, .78f);
        }

        static AudioClip ShotgunBlast()
        {
            var b = NewBuffer(.34f);
            AddNoise(b, 0, (int)(.13f * Rate), 1f, 11f);
            AddTone(b, 0, (int)(.1f * Rate), 130f, 40f, 1f, WaveForm.Saw, 12f);
            LowPass(b, .4f);
            Distort(b, 2f);
            return Bake("shot_shotgun", b, .95f);
        }

        static AudioClip MarksmanShot()
        {
            var b = NewBuffer(.3f);
            AddNoise(b, 0, (int)(.09f * Rate), 1f, 13f);
            AddTone(b, 0, (int)(.1f * Rate), 320f, 60f, .9f, WaveForm.Saw, 14f);
            AddTone(b, (int)(.02f * Rate), (int)(.14f * Rate), 1400f, 300f, .2f, WaveForm.Sine, 8f);
            LowPass(b, .5f);
            Distort(b, 1.5f);
            return Bake("shot_marksman", b, .9f);
        }

        static AudioClip PlasmaShot()
        {
            var b = NewBuffer(.22f);
            AddTone(b, 0, (int)(.16f * Rate), 900f, 180f, .7f, WaveForm.Sine, 6f);
            AddTone(b, 0, (int)(.1f * Rate), 1800f, 500f, .3f, WaveForm.Saw, 10f);
            return Bake("shot_plasma", b, .7f);
        }

        static AudioClip RocketLaunch()
        {
            var b = NewBuffer(.5f);
            AddNoise(b, 0, (int)(.3f * Rate), .8f, 6f, attack: .01f);
            AddTone(b, 0, (int)(.25f * Rate), 180f, 60f, .8f, WaveForm.Saw, 5f);
            LowPass(b, .45f);
            return Bake("shot_rocket", b, .85f);
        }

        static AudioClip GrenadeLaunch()
        {
            var b = NewBuffer(.24f);
            AddTone(b, 0, (int)(.1f * Rate), 160f, 420f, .7f, WaveForm.Triangle, 9f);
            AddNoise(b, 0, (int)(.06f * Rate), .4f, 18f);
            return Bake("shot_grenade", b, .7f);
        }

        static AudioClip ReloadStart()
        {
            var b = NewBuffer(.18f);
            AddNoise(b, 0, (int)(.03f * Rate), .8f, 30f);
            AddTone(b, (int)(.07f * Rate), (int)(.04f * Rate), 400f, 300f, .5f, WaveForm.Square, 12f);
            return Bake("reload_start", b, .6f);
        }

        static AudioClip ReloadEnd()
        {
            var b = NewBuffer(.16f);
            AddTone(b, 0, (int)(.03f * Rate), 500f, 700f, .6f, WaveForm.Square, 12f);
            AddNoise(b, (int)(.08f * Rate), (int)(.03f * Rate), .6f, 25f);
            return Bake("reload_end", b, .6f);
        }

        static AudioClip DryFire()
        {
            var b = NewBuffer(.07f);
            AddNoise(b, 0, (int)(.025f * Rate), .7f, 30f);
            LowPass(b, .3f);
            return Bake("dry_fire", b, .5f);
        }

        static AudioClip HitConfirm()
        {
            var b = NewBuffer(.07f);
            AddTone(b, 0, b.Length, 2200f, 1500f, .6f, WaveForm.Sine, 16f);
            return Bake("hit", b, .55f);
        }

        static AudioClip HeadshotHit()
        {
            var b = NewBuffer(.12f);
            AddTone(b, 0, (int)(.05f * Rate), 2600f, 1800f, .7f, WaveForm.Sine, 10f);
            AddTone(b, (int)(.03f * Rate), (int)(.07f * Rate), 1600f, 1200f, .4f, WaveForm.Triangle, 10f);
            return Bake("hit_head", b, .6f);
        }

        static AudioClip KillConfirm()
        {
            var b = NewBuffer(.16f);
            AddTone(b, 0, (int)(.07f * Rate), 900f, 1400f, .5f, WaveForm.Square, 8f);
            AddTone(b, (int)(.05f * Rate), (int)(.09f * Rate), 1400f, 1900f, .4f, WaveForm.Sine, 8f);
            return Bake("kill", b, .6f);
        }

        static AudioClip BulletImpact()
        {
            var b = NewBuffer(.09f);
            AddNoise(b, 0, b.Length, .9f, 24f);
            LowPass(b, .4f);
            return Bake("impact_bullet", b, .5f);
        }

        static AudioClip EnergyImpact()
        {
            var b = NewBuffer(.13f);
            AddTone(b, 0, b.Length, 1200f, 300f, .6f, WaveForm.Sine, 8f);
            AddNoise(b, 0, (int)(.04f * Rate), .25f, 15f);
            return Bake("impact_energy", b, .55f);
        }

        static AudioClip CrateBreak()
        {
            var b = NewBuffer(.25f);
            var rng = new System.Random(7);
            for (int i = 0; i < 7; i++)
            {
                int at = (int)((i * .026f + (float)rng.NextDouble() * .01f) * Rate);
                AddNoise(b, at, (int)(.03f * Rate), .7f, 14f, seed: 10 + i);
            }
            LowPass(b, .5f);
            return Bake("crate_break", b, .7f);
        }

        static AudioClip GlassBreak()
        {
            var b = NewBuffer(.3f);
            var rng = new System.Random(23);
            for (int i = 0; i < 10; i++)
            {
                int at = (int)((i * .02f + (float)rng.NextDouble() * .02f) * Rate);
                AddTone(b, at, (int)(.05f * Rate), 2600f + i * 180f, 2000f + i * 150f, .3f, WaveForm.Sine, 10f);
            }
            return Bake("glass_break", b, .55f);
        }

        static AudioClip EnemyShot()
        {
            var b = NewBuffer(.13f);
            AddTone(b, 0, (int)(.08f * Rate), 700f, 260f, .6f, WaveForm.Square, 12f);
            AddNoise(b, 0, (int)(.03f * Rate), .3f, 22f);
            return Bake("enemy_shot", b, .6f);
        }

        static AudioClip EnemyShotHeavy()
        {
            var b = NewBuffer(.22f);
            AddTone(b, 0, (int)(.14f * Rate), 380f, 120f, .8f, WaveForm.Saw, 8f);
            AddNoise(b, 0, (int)(.06f * Rate), .5f, 14f);
            LowPass(b, .45f);
            return Bake("enemy_shot_heavy", b, .75f);
        }

        static AudioClip EnemyAlert()
        {
            var b = NewBuffer(.28f);
            AddTone(b, 0, (int)(.1f * Rate), 520f, 660f, .5f, WaveForm.Square, 6f);
            AddTone(b, (int)(.11f * Rate), (int)(.12f * Rate), 660f, 520f, .5f, WaveForm.Square, 6f);
            return Bake("enemy_alert", b, .5f);
        }

        static AudioClip EnemyDeath()
        {
            var b = NewBuffer(.34f);
            AddTone(b, 0, (int)(.22f * Rate), 400f, 60f, .7f, WaveForm.Saw, 6f);
            AddNoise(b, 0, (int)(.1f * Rate), .4f, 12f);
            LowPass(b, .5f);
            return Bake("enemy_die", b, .7f);
        }

        static AudioClip EnemyDeathBig()
        {
            var b = NewBuffer(.6f);
            AddTone(b, 0, (int)(.4f * Rate), 250f, 40f, .9f, WaveForm.Saw, 4f);
            AddNoise(b, 0, (int)(.2f * Rate), .6f, 7f);
            LowPass(b, .4f);
            Distort(b, 1.3f);
            return Bake("enemy_die_big", b, .85f);
        }

        static AudioClip EnemyMelee()
        {
            var b = NewBuffer(.14f);
            AddNoise(b, 0, (int)(.08f * Rate), .8f, 12f);
            AddTone(b, 0, (int)(.06f * Rate), 180f, 80f, .6f, WaveForm.Triangle, 12f);
            LowPass(b, .5f);
            return Bake("enemy_melee", b, .6f);
        }

        static AudioClip HealBeam()
        {
            var b = NewBuffer(.5f);
            AddTone(b, 0, b.Length, 660f, 990f, .25f, WaveForm.Sine, 1.2f);
            return Bake("heal_beam", b, .35f);
        }

        static AudioClip TurretShot()
        {
            var b = NewBuffer(.1f);
            AddTone(b, 0, (int)(.06f * Rate), 850f, 300f, .6f, WaveForm.Square, 14f);
            return Bake("turret_shot", b, .5f);
        }

        static AudioClip TurretDie()
        {
            var b = NewBuffer(.4f);
            AddTone(b, 0, (int)(.3f * Rate), 800f, 80f, .6f, WaveForm.Saw, 5f);
            AddNoise(b, 0, (int)(.15f * Rate), .4f, 8f);
            return Bake("turret_die", b, .65f);
        }

        static AudioClip Electric()
        {
            var b = NewBuffer(.3f);
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                int at = (int)((float)rng.NextDouble() * .22f * Rate);
                AddNoise(b, at, (int)(.012f * Rate), .5f, 10f, seed: 100 + i);
            }
            AddTone(b, 0, b.Length, 120f, 55f, .3f, WaveForm.Saw, 2f);
            return Bake("electric", b, .6f);
        }

        static AudioClip Explosion()
        {
            var b = NewBuffer(.75f);
            AddNoise(b, 0, (int)(.5f * Rate), 1f, 5f, attack: .002f);
            AddTone(b, 0, (int)(.3f * Rate), 90f, 30f, 1f, WaveForm.Sine, 6f);
            LowPass(b, .35f);
            Distort(b, 1.4f);
            return Bake("explosion", b, .95f);
        }

        static AudioClip ExplosionBig()
        {
            var b = NewBuffer(1.1f);
            AddNoise(b, 0, (int)(.8f * Rate), 1f, 3.5f, attack: .004f);
            AddTone(b, 0, (int)(.5f * Rate), 70f, 24f, 1f, WaveForm.Sine, 4f);
            LowPass(b, .28f);
            Distort(b, 1.6f);
            return Bake("explosion_big", b, 1f);
        }

        static AudioClip PickupWeapon()
        {
            var b = NewBuffer(.3f);
            AddTone(b, 0, (int)(.12f * Rate), 400f, 800f, .5f, WaveForm.Square, 6f);
            AddTone(b, (int)(.1f * Rate), (int)(.18f * Rate), 800f, 1200f, .45f, WaveForm.Sine, 5f);
            return Bake("pickup_weapon", b, .65f);
        }

        static AudioClip PickupAmmo()
        {
            var b = NewBuffer(.2f);
            AddTone(b, 0, (int)(.08f * Rate), 600f, 900f, .5f, WaveForm.Triangle, 8f);
            AddNoise(b, (int)(.06f * Rate), (int)(.04f * Rate), .3f, 16f);
            return Bake("pickup_ammo", b, .55f);
        }

        static AudioClip PickupHealth()
        {
            var b = NewBuffer(.26f);
            AddTone(b, 0, (int)(.1f * Rate), 660f, 880f, .45f, WaveForm.Sine, 5f);
            AddTone(b, (int)(.09f * Rate), (int)(.12f * Rate), 880f, 1100f, .4f, WaveForm.Sine, 5f);
            return Bake("pickup_health", b, .55f);
        }

        static AudioClip PickupPowerup()
        {
            var b = NewBuffer(.42f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
                AddTone(b, (int)(i * .07f * Rate), (int)(.14f * Rate), notes[i], notes[i] * 1.01f, .4f, WaveForm.Triangle, 4f);
            return Bake("pickup_powerup", b, .6f);
        }

        static AudioClip BossSpawn()
        {
            var b = NewBuffer(1.6f);
            AddTone(b, 0, (int)(.8f * Rate), 55f, 110f, .9f, WaveForm.Saw, 1.2f, attack: .3f);
            AddTone(b, 0, (int)(.9f * Rate), 82.5f, 55f, .6f, WaveForm.Square, 1.5f, attack: .3f);
            AddNoise(b, 0, (int)(.6f * Rate), .35f, 2.5f, attack: .2f);
            LowPass(b, .3f);
            Distort(b, 1.5f);
            return Bake("boss_spawn", b, .95f);
        }

        static AudioClip BossRoar()
        {
            var b = NewBuffer(.9f);
            AddTone(b, 0, (int)(.6f * Rate), 160f, 70f, .9f, WaveForm.Saw, 3f, attack: .05f);
            AddTone(b, 0, (int)(.55f * Rate), 240f, 105f, .5f, WaveForm.Square, 3f);
            AddNoise(b, 0, (int)(.3f * Rate), .4f, 6f);
            Distort(b, 1.8f);
            return Bake("boss_roar", b, .9f);
        }

        static AudioClip BossPhase()
        {
            var b = NewBuffer(.7f);
            AddTone(b, 0, (int)(.25f * Rate), 220f, 440f, .6f, WaveForm.Saw, 3f);
            AddTone(b, (int)(.26f * Rate), (int)(.4f * Rate), 440f, 220f, .6f, WaveForm.Saw, 3f);
            Distort(b, 1.4f);
            return Bake("boss_phase", b, .8f);
        }

        static AudioClip BossDie()
        {
            var b = NewBuffer(2.2f);
            AddTone(b, 0, (int)(1.6f * Rate), 130f, 25f, 1f, WaveForm.Saw, 1.6f);
            AddNoise(b, 0, (int)(1.2f * Rate), .7f, 2.2f);
            for (int i = 0; i < 5; i++)
            {
                int at = (int)((.2f + i * .28f) * Rate);
                if (at + 3000 < b.Length) AddNoise(b, at, (int)(.25f * Rate), .5f, 4f, seed: 200 + i);
            }
            LowPass(b, .32f);
            Distort(b, 1.5f);
            return Bake("boss_die", b, 1f);
        }

        static AudioClip BossCharge()
        {
            var b = NewBuffer(.6f);
            AddTone(b, 0, b.Length, 100f, 500f, .7f, WaveForm.Saw, 1.5f, attack: .1f);
            AddNoise(b, 0, (int)(.5f * Rate), .35f, 3f, attack: .1f);
            return Bake("boss_charge", b, .7f);
        }

        static AudioClip BossSlam()
        {
            var b = NewBuffer(.8f);
            AddNoise(b, 0, (int)(.5f * Rate), 1f, 5f);
            AddTone(b, 0, (int)(.4f * Rate), 80f, 30f, 1f, WaveForm.Sine, 5f);
            LowPass(b, .3f);
            Distort(b, 1.7f);
            return Bake("boss_slam", b, .95f);
        }

        static AudioClip WaveStart()
        {
            var b = NewBuffer(.7f);
            AddTone(b, 0, (int)(.22f * Rate), 392f, 392f, .5f, WaveForm.Square, 3f);
            AddTone(b, (int)(.24f * Rate), (int)(.3f * Rate), 523.25f, 523.25f, .55f, WaveForm.Square, 3f);
            AddNoise(b, (int)(.24f * Rate), (int)(.1f * Rate), .2f, 10f);
            return Bake("wave_start", b, .7f);
        }

        static AudioClip WaveClear()
        {
            var b = NewBuffer(.8f);
            float[] notes = { 523.25f, 659.25f, 783.99f };
            for (int i = 0; i < notes.Length; i++)
                AddTone(b, (int)(i * .12f * Rate), (int)(.3f * Rate), notes[i], notes[i], .45f, WaveForm.Triangle, 3f);
            return Bake("wave_clear", b, .65f);
        }

        // ---------------------------------------------------------------- music

        static AudioClip MusicMenu()
        {
            const float bpm = 84f;
            float beat = 60f / bpm;
            int beats = 16;
            var b = NewBuffer(beat * beats);

            // Warm Am pad (A2 C3 E3) breathing.
            float[] pad = { 110f, 130.81f, 164.81f };
            for (int bar = 0; bar < 4; bar++)
            {
                int start = (int)(bar * 4 * beat * Rate);
                foreach (float f in pad)
                    AddTone(b, start, (int)(3.8f * beat * Rate), f * .997f, f, .22f, WaveForm.Saw, .45f, attack: .6f);
            }
            // Sparse bell arpeggio.
            float[] bells = { 440f, 523.25f, 659.25f, 587.33f };
            for (int i = 0; i < 8; i++)
            {
                int start = (int)((i * 2 + 1) * beat * Rate);
                float f = bells[i % bells.Length];
                AddTone(b, start, (int)(1.5f * beat * Rate), f, f, .12f, WaveForm.Sine, 2f);
            }
            LowPass(b, .45f);
            return Bake("music_menu", b, .5f);
        }

        static AudioClip MusicCombat()
        {
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = 8;
            var b = NewBuffer(bars * 4 * beat);

            float[] bassLine = { 55f, 55f, 65.41f, 49f }; // A A C G
            for (int bar = 0; bar < bars; bar++)
            {
                int barStart = (int)(bar * 4 * beat * Rate);
                float bass = bassLine[bar % 4];
                for (int e = 0; e < 8; e++)
                {
                    int at = barStart + (int)(e * .5f * beat * Rate);
                    AddTone(b, at, (int)(.42f * beat * Rate), bass, bass, .5f, WaveForm.Saw, 3f);
                }
                AddKick(b, barStart, .8f);
                AddKick(b, barStart + (int)(2 * beat * Rate), .8f);
                if (bar % 2 == 1) AddKick(b, barStart + (int)(3.5f * beat * Rate), .6f);

                // Hats on the offbeats.
                for (int e = 0; e < 8; e++)
                    AddNoise(b, barStart + (int)((e + .5f) * .5f * beat * Rate), (int)(.03f * Rate), .12f, 18f, seed: bar * 16 + e);

                // Minor stab chord every other bar.
                if (bar % 2 == 0)
                {
                    int at = barStart + (int)(2.5f * beat * Rate);
                    AddTone(b, at, (int)(.8f * beat * Rate), 220f, 220f, .18f, WaveForm.Square, 3f);
                    AddTone(b, at, (int)(.8f * beat * Rate), 261.63f, 261.63f, .16f, WaveForm.Square, 3f);
                    AddTone(b, at, (int)(.8f * beat * Rate), 329.63f, 329.63f, .14f, WaveForm.Square, 3f);
                }
            }
            LowPass(b, .6f);
            return Bake("music_combat", b, .6f);
        }

        static AudioClip MusicBoss()
        {
            const float bpm = 144f;
            float beat = 60f / bpm;
            int bars = 8;
            var b = NewBuffer(bars * 4 * beat);

            float[] bass = { 41.2f, 41.2f, 58.27f, 43.65f }; // F F Bb Ab tritone push
            for (int bar = 0; bar < bars; bar++)
            {
                int barStart = (int)(bar * 4 * beat * Rate);
                float f = bass[bar % 4];
                for (int e = 0; e < 8; e++)
                {
                    int at = barStart + (int)(e * .5f * beat * Rate);
                    AddTone(b, at, (int)(.4f * beat * Rate), f, f * 1.01f, .55f, WaveForm.Saw, 3.5f);
                }
                AddKick(b, barStart, .9f);
                AddKick(b, barStart + (int)(beat * Rate), .7f);
                AddKick(b, barStart + (int)(2 * beat * Rate), .9f);
                if (bar % 4 == 3)
                {
                    for (int i = 0; i < 4; i++)
                        AddKick(b, barStart + (int)((3 + i * .25f) * beat * Rate), .5f + i * .1f);
                }
                for (int e = 0; e < 16; e++)
                    AddNoise(b, barStart + (int)(e * .25f * beat * Rate), (int)(.02f * Rate), .1f, 22f, seed: bar * 32 + e);

                // Aggressive stabs.
                int stabAt = barStart + (int)(1.75f * beat * Rate);
                AddTone(b, stabAt, (int)(.5f * beat * Rate), 220f, 233f, .2f, WaveForm.Square, 4f);
                AddTone(b, stabAt, (int)(.5f * beat * Rate), 311.13f, 320f, .18f, WaveForm.Square, 4f);
            }
            Distort(b, 1.2f);
            LowPass(b, .65f);
            return Bake("music_boss", b, .65f);
        }

        static AudioClip Unlock()
        {
            var b = NewBuffer(.45f);
            AddTone(b, 0, (int)(.18f * Rate), 659.25f, 659.25f, .4f, WaveForm.Sine, 6f);
            AddTone(b, (int)(.11f * Rate), (int)(.3f * Rate), 987.77f, 987.77f, .35f, WaveForm.Triangle, 6f);
            return Bake("unlock", b);
        }

        static AudioClip StingerVictory()
        {
            var b = NewBuffer(2.4f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
                AddTone(b, (int)(i * .16f * Rate), (int)(1.2f * Rate), notes[i], notes[i], .4f, WaveForm.Triangle, 1.2f);
            AddTone(b, (int)(.7f * Rate), (int)(1.6f * Rate), 1318.5f, 1318.5f, .25f, WaveForm.Sine, 1f);
            return Bake("stinger_victory", b, .6f);
        }

        static AudioClip StingerDefeat()
        {
            var b = NewBuffer(2.6f);
            float[] notes = { 392f, 349.23f, 293.66f, 220f };
            for (int i = 0; i < notes.Length; i++)
                AddTone(b, (int)(i * .3f * Rate), (int)(1.3f * Rate), notes[i], notes[i] * .995f, .4f, WaveForm.Saw, 1.1f, attack: .05f);
            LowPass(b, .5f);
            return Bake("stinger_defeat", b, .55f);
        }
    }
}
