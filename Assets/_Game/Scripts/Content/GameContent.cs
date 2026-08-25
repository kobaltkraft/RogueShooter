using System.Collections.Generic;
using UnityEngine;
using RogueArena.AI;
using RogueArena.Core;
using RogueArena.Environment;
using RogueArena.Pickups;
using RogueArena.Progression;
using RogueArena.Waves;
using RogueArena.Weapons;

namespace RogueArena.Content
{
    /// <summary>
    /// The game's design data. Every definition is a ScriptableObject instance built
    /// once per session with tuned, balanced values - the whole game can be rebalanced
    /// from this single file, and an editor command can export them as authored assets.
    /// </summary>
    public static class GameContent
    {
        // weapons
        public static readonly List<WeaponDefinition> Weapons = new List<WeaponDefinition>();
        public static WeaponDefinition Rifle, Shotgun, Rocket, Smg, Burst, Marksman, Plasma, Grenade;

        // enemies
        public static readonly List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
        public static EnemyDefinition Grunt, Rusher, Soldier, Assault, Shotgunner, Sniper, Medic, Explosive, Heavy, Tank, Drone;

        // elites / powerups / modes / arenas
        public static readonly List<EliteModifierDefinition> EliteModifiers = new List<EliteModifierDefinition>();
        public static readonly List<PowerupDefinition> Powerups = new List<PowerupDefinition>();
        public static readonly List<GameModeDefinition> Modes = new List<GameModeDefinition>();
        public static readonly List<ArenaDefinition> Arenas = new List<ArenaDefinition>();
        public static readonly List<ChallengeDefinition> Challenges = new List<ChallengeDefinition>();
        public static readonly List<PlayerUpgradeDefinition> PlayerUpgrades = new List<PlayerUpgradeDefinition>();
        public static readonly List<BossDefinition> Bosses = new List<BossDefinition>();
        public static BossDefinition Vulcan;

        public static readonly List<CrosshairStyle> Crosshairs = new List<CrosshairStyle>();

        public struct CrosshairStyle
        {
            public string Id;
            public string DisplayName;
            public Color Color;
            public CrosshairStyle(string id, string name, Color color) { Id = id; DisplayName = name; Color = color; }
        }

        static bool initialized;

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            BuildWeapons();
            BuildEnemies();
            BuildEliteModifiers();
            BuildPowerups();
            BuildBosses();
            BuildWavesAndModes();
            BuildArenas();
            BuildChallenges();
            BuildPlayerUpgrades();
            BuildCrosshairs();
        }

        // ---------------------------------------------------------------- lookups

        public static WeaponDefinition Weapon(string id) => Weapons.Find(w => w.id == id);
        public static EnemyDefinition Enemy(string id) => Enemies.Find(e => e.id == id);
        public static EnemyDefinition EnemyOf(EnemyKind kind) => Enemies.Find(e => e.kind == kind);
        public static EliteModifierDefinition Elite(EliteModifierKind kind) => EliteModifiers.Find(m => m.kind == kind);
        public static PowerupDefinition Powerup(string id) => Powerups.Find(p => p.id == id);
        public static GameModeDefinition Mode(string id) => Modes.Find(m => m.id == id);
        public static ArenaDefinition Arena(string id) => Arenas.Find(a => a.id == id);
        public static BossDefinition Boss(string id) => Bosses.Find(b => b.id == id);
        public static ChallengeDefinition Challenge(string id) => Challenges.Find(c => c.id == id);
        public static PlayerUpgradeDefinition Upgrade(string id) => PlayerUpgrades.Find(u => u.id == id);

        // ---------------------------------------------------------------- weapons

        static WeaponDefinition W(string id, string name, string desc, WeaponRarity rarity, string glyph)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.id = id;
            w.displayName = name;
            w.description = desc;
            w.rarity = rarity;
            w.hudGlyph = glyph;
            Weapons.Add(w);
            return w;
        }

        static void BuildWeapons()
        {
            Rifle = W("rifle", "AR-1 VANGUARD", "Reliable automatic rifle. The arena standard.", WeaponRarity.Standard, "AR");
            Rifle.damage = 16f; Rifle.fireRate = 9.5f; Rifle.magazineSize = 30; Rifle.reserveAmmo = 150;
            Rifle.spread = .5f; Rifle.spreadMoving = .9f; Rifle.spreadAir = 2.2f;
            Rifle.recoilPitch = .5f; Rifle.recoilYaw = .16f; Rifle.reloadTime = 1.35f;
            Rifle.shotSfx = "shot_rifle"; Rifle.accentColor = new Color(.2f, .95f, 1f);

            Shotgun = W("shotgun", "SG-8 BREAKER", "Pump-action. Devastating up close.", WeaponRarity.Standard, "SG");
            Shotgun.fireMode = FireMode.Pump; Shotgun.damage = 11f; Shotgun.pellets = 8; Shotgun.fireRate = 1.1f;
            Shotgun.magazineSize = 6; Shotgun.reserveAmmo = 36; Shotgun.spread = 4.5f; Shotgun.spreadMoving = 1.5f;
            Shotgun.recoilPitch = 2.6f; Shotgun.recoilYaw = .5f; Shotgun.reloadTime = 2.4f;
            Shotgun.knockback = 6f; Shotgun.shotSfx = "shot_shotgun"; Shotgun.shakePerShot = .18f;
            Shotgun.fovKick = 1f; Shotgun.accentColor = new Color(1f, .55f, .1f);
            Shotgun.sfxVolume = 1f;

            Rocket = W("rocket", "RL-5 COMET", "Explosive ordnance. Mind the splash.", WeaponRarity.Heavy, "RL");
            Rocket.fireMode = FireMode.Semi; Rocket.isProjectile = true; Rocket.damage = 20f;
            Rocket.fireRate = .8f; Rocket.magazineSize = 4; Rocket.reserveAmmo = 12;
            Rocket.reloadTime = 2.6f; Rocket.recoilPitch = 3.4f; Rocket.recoilYaw = .3f;
            Rocket.projectile.speed = 34f; Rocket.projectile.splashRadius = 6f;
            Rocket.projectile.splashDamage = 90f; Rocket.projectile.splashKnockback = 9f;
            Rocket.projectile.selfDamageFraction = .35f; Rocket.shotSfx = "shot_rocket";
            Rocket.shakePerShot = .16f; Rocket.accentColor = new Color(1f, .3f, .2f);
            Rocket.spread = 0f;

            Smg = W("smg", "VX-9 HORNET", "Compact SMG. Sprays fast, hits soft.", WeaponRarity.Standard, "SM");
            Smg.damage = 11f; Smg.fireRate = 14f; Smg.magazineSize = 36; Smg.reserveAmmo = 220;
            Smg.spread = 1.2f; Smg.spreadMoving = 1.6f; Smg.recoilPitch = .3f; Smg.recoilYaw = .22f;
            Smg.reloadTime = 1.25f; Smg.shotSfx = "shot_smg";
            Smg.unlockLevel = 2; Smg.unlockCost = 250; Smg.accentColor = new Color(.5f, 1f, .4f);

            Burst = W("burst", "BR-7 TRIDENT", "Three-round bursts with surgical accuracy.", WeaponRarity.Advanced, "BR");
            Burst.fireMode = FireMode.Burst; Burst.burstCount = 3; Burst.burstDelay = .055f;
            Burst.damage = 20f; Burst.fireRate = 8f; Burst.magazineSize = 27; Burst.reserveAmmo = 140;
            Burst.spread = .35f; Burst.spreadMoving = .8f; Burst.recoilPitch = .7f; Burst.recoilYaw = .18f;
            Burst.reloadTime = 1.5f; Burst.shotSfx = "shot_burst";
            Burst.unlockLevel = 3; Burst.unlockCost = 450; Burst.accentColor = new Color(1f, .85f, .3f);

            Marksman = W("marksman", "MR-12 LONGSHOT", "Semi-auto marksman rifle. Aim high.", WeaponRarity.Advanced, "MR");
            Marksman.fireMode = FireMode.Semi; Marksman.damage = 65f; Marksman.headshotMultiplier = 2.5f;
            Marksman.fireRate = 1.6f; Marksman.magazineSize = 10; Marksman.reserveAmmo = 40;
            Marksman.spread = .15f; Marksman.spreadMoving = 2.5f; Marksman.recoilPitch = 3f; Marksman.recoilYaw = .4f;
            Marksman.reloadTime = 1.8f; Marksman.shotSfx = "shot_marksman"; Marksman.fovKick = 1.2f;
            Marksman.shakePerShot = .12f;
            Marksman.unlockLevel = 4; Marksman.unlockCost = 650; Marksman.accentColor = new Color(.3f, .6f, 1f);

            Plasma = W("plasma", "PX-4 ION", "Superheated bolts. Watch the heat gauge.", WeaponRarity.Experimental, "PX");
            Plasma.fireMode = FireMode.Auto; Plasma.isProjectile = true; Plasma.usesHeat = true;
            Plasma.damage = 26f; Plasma.fireRate = 5.5f; Plasma.heatPerShot = .08f; Plasma.heatCooldown = .32f;
            Plasma.overheatLockout = 1.1f; Plasma.spread = .4f; Plasma.recoilPitch = .6f; Plasma.recoilYaw = .1f;
            Plasma.projectile.speed = 55f; Plasma.projectile.splashRadius = 2.2f;
            Plasma.projectile.splashDamage = 14f; Plasma.projectile.splashKnockback = 2f;
            Plasma.projectile.selfDamageFraction = 0f; Plasma.projectile.tracerColor = new Color(.3f, .9f, 1f);
            Plasma.shotSfx = "shot_plasma";
            Plasma.unlockLevel = 5; Plasma.unlockCost = 900; Plasma.accentColor = new Color(.3f, .9f, 1f);

            Grenade = W("grenade", "GL-6 HAILSTORM", "Arcing grenades. Rains area damage.", WeaponRarity.Heavy, "GL");
            Grenade.fireMode = FireMode.Semi; Grenade.isProjectile = true; Grenade.damage = 15f;
            Grenade.fireRate = 1.2f; Grenade.magazineSize = 6; Grenade.reserveAmmo = 24;
            Grenade.reloadTime = 2.8f; Grenade.recoilPitch = 1.8f; Grenade.recoilYaw = .2f;
            Grenade.projectile.speed = 24f; Grenade.projectile.gravity = 22f;
            Grenade.projectile.splashRadius = 4.5f; Grenade.projectile.splashDamage = 70f;
            Grenade.projectile.splashKnockback = 7f; Grenade.projectile.selfDamageFraction = .3f;
            Grenade.shotSfx = "shot_grenade";
            Grenade.unlockLevel = 6; Grenade.unlockCost = 1200; Grenade.accentColor = new Color(.7f, 1f, .2f);
        }

        // ---------------------------------------------------------------- enemies

        static EnemyDefinition E(string id, string name, EnemyKind kind)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            e.id = id; e.displayName = name; e.kind = kind;
            Enemies.Add(e);
            return e;
        }

        static void BuildEnemies()
        {
            // The original prototype drone, preserved and improved.
            Grunt = E("grunt", "SENTRY GRUNT", EnemyKind.Grunt);
            Grunt.maxHealth = 70f; Grunt.moveSpeed = 3.6f; Grunt.attackStyle = AttackStyle.Melee;
            Grunt.attackRange = 2.2f; Grunt.attackDamage = 12f; Grunt.attackInterval = 1f;
            Grunt.attackWindup = .3f; Grunt.xpValue = 20; Grunt.scoreValue = 100;
            Grunt.budgetCost = 1f; Grunt.minWave = 1;

            Rusher = E("rusher", "RUSHER", EnemyKind.Rusher);
            Rusher.maxHealth = 40f; Rusher.moveSpeed = 6.4f; Rusher.attackStyle = AttackStyle.Melee;
            Rusher.attackRange = 2f; Rusher.attackDamage = 8f; Rusher.attackInterval = .7f;
            Rusher.attackWindup = .18f; Rusher.strafeSpeed = 1f; Rusher.xpValue = 15;
            Rusher.scoreValue = 80; Rusher.budgetCost = 1f; Rusher.minWave = 2; Rusher.bodyScale = .85f;

            Soldier = E("soldier", "SOLDIER", EnemyKind.Soldier);
            Soldier.maxHealth = 85f; Soldier.moveSpeed = 3.8f; Soldier.attackStyle = AttackStyle.Hitscan;
            Soldier.attackRange = 26f; Soldier.preferredRange = 12f; Soldier.attackDamage = 7f;
            Soldier.attackInterval = 1f; Soldier.attackWindup = .5f; Soldier.strafeSpeed = 2.4f;
            Soldier.coverPreference = .25f; Soldier.xpValue = 30; Soldier.scoreValue = 150;
            Soldier.budgetCost = 2f; Soldier.minWave = 2;

            Assault = E("assault", "ASSAULT TROOPER", EnemyKind.Assault);
            Assault.maxHealth = 100f; Assault.moveSpeed = 4f; Assault.attackStyle = AttackStyle.Hitscan;
            Assault.attackRange = 24f; Assault.preferredRange = 10f; Assault.attackDamage = 8f;
            Assault.attackInterval = .9f; Assault.attackWindup = .45f; Assault.strafeSpeed = 2.8f;
            Assault.coverPreference = .55f; Assault.flankChance = .3f; Assault.xpValue = 35;
            Assault.scoreValue = 180; Assault.budgetCost = 2f; Assault.minWave = 3;

            Shotgunner = E("shotgunner", "SHOTGUNNER", EnemyKind.Shotgunner);
            Shotgunner.maxHealth = 130f; Shotgunner.moveSpeed = 4.6f; Shotgunner.attackStyle = AttackStyle.Hitscan;
            Shotgunner.attackRange = 10f; Shotgunner.preferredRange = 6f; Shotgunner.attackDamage = 7f;
            Shotgunner.projectileCount = 3; Shotgunner.attackSpread = 7f; Shotgunner.attackInterval = 1.4f;
            Shotgunner.attackWindup = .6f; Shotgunner.coverPreference = .4f; Shotgunner.strafeSpeed = 2.6f;
            Shotgunner.xpValue = 40; Shotgunner.scoreValue = 200; Shotgunner.budgetCost = 2.5f; Shotgunner.minWave = 4;

            Sniper = E("sniper", "SNIPER", EnemyKind.Sniper);
            Sniper.maxHealth = 60f; Sniper.moveSpeed = 3.2f; Sniper.attackStyle = AttackStyle.Hitscan;
            Sniper.attackRange = 45f; Sniper.preferredRange = 28f; Sniper.minimumRange = 10f;
            Sniper.attackDamage = 26f; Sniper.attackInterval = 3.2f; Sniper.attackWindup = 1.4f;
            Sniper.repositionAfterAttack = 2.5f; Sniper.usesElevatedPositions = true;
            Sniper.strafeSpeed = 1.6f; Sniper.coverPreference = .5f; Sniper.xpValue = 50;
            Sniper.scoreValue = 250; Sniper.budgetCost = 3f; Sniper.minWave = 5;

            Medic = E("medic", "MEDIC", EnemyKind.Medic);
            Medic.maxHealth = 90f; Medic.moveSpeed = 3.4f; Medic.attackStyle = AttackStyle.Healer;
            Medic.preferredRange = 16f; Medic.healPerSecond = 14f; Medic.healRange = 8f;
            Medic.retreatHealthFraction = .6f; Medic.strafeSpeed = 2.2f; Medic.xpValue = 60;
            Medic.scoreValue = 300; Medic.budgetCost = 3f; Medic.minWave = 6; Medic.maxConcurrent = 2;

            Explosive = E("explosive", "DEMOLISHER", EnemyKind.Explosive);
            Explosive.maxHealth = 110f; Explosive.moveSpeed = 3f; Explosive.attackStyle = AttackStyle.Projectile;
            Explosive.attackRange = 22f; Explosive.preferredRange = 14f; Explosive.attackDamage = 20f;
            Explosive.projectileSpeed = 16f; Explosive.projectileArc = 14f; Explosive.attackInterval = 2.6f;
            Explosive.attackWindup = .7f; Explosive.coverPreference = .3f; Explosive.xpValue = 45;
            Explosive.scoreValue = 220; Explosive.budgetCost = 3f; Explosive.minWave = 5;

            Heavy = E("heavy", "HEAVY GUNNER", EnemyKind.Heavy);
            Heavy.maxHealth = 300f; Heavy.moveSpeed = 2.4f; Heavy.attackStyle = AttackStyle.Hitscan;
            Heavy.attackRange = 26f; Heavy.preferredRange = 14f; Heavy.attackDamage = 14f;
            Heavy.attackInterval = 1.6f; Heavy.attackWindup = .7f; Heavy.knockbackResistance = .5f;
            Heavy.strafeSpeed = 0f; Heavy.coverPreference = .2f; Heavy.bodyScale = 1.25f;
            Heavy.xpValue = 80; Heavy.scoreValue = 400; Heavy.budgetCost = 4f; Heavy.minWave = 7;
            Heavy.shotSfx = "enemy_shot_heavy";

            Tank = E("tank", "TANK", EnemyKind.Tank);
            Tank.maxHealth = 700f; Tank.moveSpeed = 1.7f; Tank.attackStyle = AttackStyle.Hitscan;
            Tank.attackRange = 22f; Tank.preferredRange = 10f; Tank.attackDamage = 18f;
            Tank.projectileCount = 3; Tank.attackSpread = 6f; Tank.attackInterval = 2f;
            Tank.attackWindup = .8f; Tank.knockbackResistance = .95f; Tank.strafeSpeed = 0f;
            Tank.agentRadius = .8f; Tank.agentHeight = 2.9f; Tank.bodyScale = 1.7f;
            Tank.xpValue = 150; Tank.scoreValue = 750; Tank.budgetCost = 6f; Tank.minWave = 9;
            Tank.maxConcurrent = 2; Tank.shotSfx = "enemy_shot_heavy";

            Drone = E("drone", "HUNTER DRONE", EnemyKind.Drone);
            Drone.maxHealth = 55f; Drone.moveSpeed = 5f; Drone.attackStyle = AttackStyle.Projectile;
            Drone.flying = true; Drone.flyHeight = 3.2f; Drone.attackRange = 20f; Drone.preferredRange = 12f;
            Drone.attackDamage = 8f; Drone.projectileSpeed = 26f; Drone.attackInterval = 1.2f;
            Drone.attackWindup = .4f; Drone.strafeSpeed = 3.2f; Drone.xpValue = 30;
            Drone.scoreValue = 160; Drone.budgetCost = 2f; Drone.minWave = 4;
        }

        // ---------------------------------------------------------------- elites

        static EliteModifierDefinition EliteMod(EliteModifierKind kind, string title, string desc,
            float hp = 1f, float speed = 1f, float attackSpeed = 1f, float range = 1f)
        {
            var m = ScriptableObject.CreateInstance<EliteModifierDefinition>();
            m.kind = kind; m.title = title; m.description = desc;
            m.healthMultiplier = hp; m.speedMultiplier = speed;
            m.attackSpeedMultiplier = attackSpeed; m.attackRangeMultiplier = range;
            EliteModifiers.Add(m);
            return m;
        }

        static void BuildEliteModifiers()
        {
            var fast = EliteMod(EliteModifierKind.Fast, "FAST", "Moves much quicker.", speed: 1.5f);
            fast.tint = new Color(.3f, .8f, 1f);

            var armored = EliteMod(EliteModifierKind.Armored, "ARMOURED", "Reinforced plating.", hp: 1.9f);
            armored.tint = new Color(.55f, .55f, .6f);
            armored.scoreMultiplier = 2f; armored.xpMultiplier = 1.8f;

            var regen = EliteMod(EliteModifierKind.Regenerating, "REGENERATING", "Repairs itself over time.");
            regen.regenPerSecond = 5f;
            regen.tint = new Color(.3f, 1f, .45f);

            var explosive = EliteMod(EliteModifierKind.Explosive, "EXPLOSIVE", "Detonates on death.");
            explosive.explodeOnDeathRadius = 5f; explosive.explodeOnDeathDamage = 45f;
            explosive.tint = new Color(1f, .5f, .1f);

            var vampiric = EliteMod(EliteModifierKind.Vampiric, "VAMPIRIC", "Feeds on your pain.");
            vampiric.healOnDamageDealt = 12f;
            vampiric.tint = new Color(1f, .2f, .3f);

            var shielded = EliteMod(EliteModifierKind.Shielded, "SHIELDED", "Absorbs a burst of damage.", hp: 1.3f);
            shielded.shieldFraction = .5f;
            shielded.tint = new Color(.4f, .6f, 1f);

            var teleporting = EliteMod(EliteModifierKind.Teleporting, "TELEPORTING", "Blinks toward you.");
            teleporting.teleports = true;
            teleporting.tint = new Color(.8f, .4f, 1f);

            var frenzied = EliteMod(EliteModifierKind.Frenzied, "FRENZIED", "Attacks much faster.", attackSpeed: 1.55f);
            frenzied.tint = new Color(1f, .8f, .2f);

            var longRange = EliteMod(EliteModifierKind.LongRange, "LONG-RANGE", "Extended attack range.", range: 1.8f);
            longRange.tint = new Color(.2f, .9f, .8f);

            var summoner = EliteMod(EliteModifierKind.Summoner, "SUMMONER", "Calls in reinforcements.");
            summoner.summons = true; summoner.summonInterval = 9f;
            summoner.summonKind = EnemyKind.Grunt; summoner.summonCount = 2;
            summoner.tint = new Color(1f, .6f, 1f);
            summoner.scoreMultiplier = 2.2f;

            var berserker = EliteMod(EliteModifierKind.Berserker, "BERSERKER", "Enrages when wounded.");
            berserker.lowHealthSpeedBonus = 1.9f; berserker.lowHealthThreshold = .4f;
            berserker.tint = new Color(1f, .25f, .1f);
        }

        // ---------------------------------------------------------------- powerups

        static void BuildPowerups()
        {
            Powerups.Add(Powerup("damage", "DAMAGE BOOST", "Weapon damage x2.",
                PowerupKind.DamageBoost, 12f, 2f, new Color(1f, .4f, .15f), "D"));
            Powerups.Add(Powerup("rapidfire", "RAPID FIRE", "Fire rate x1.6.",
                PowerupKind.RapidFire, 10f, 1.6f, new Color(1f, .85f, .2f), "R"));
            Powerups.Add(Powerup("speed", "SPEED BOOST", "Move speed x1.35.",
                PowerupKind.SpeedBoost, 12f, 1.35f, new Color(.3f, .9f, 1f), "S"));
            Powerups.Add(Powerup("infiniteammo", "INFINITE AMMO", "No ammo consumed.",
                PowerupKind.InfiniteAmmo, 10f, 1f, new Color(.6f, 1f, .4f), "A"));
            Powerups.Add(Powerup("shield", "SHIELD", "+50 armour instantly.",
                PowerupKind.Shield, 1f, 50f, new Color(.45f, .65f, 1f), "O"));
        }

        static PowerupDefinition Powerup(string id, string name, string desc,
            PowerupKind kind, float duration, float magnitude, Color color, string glyph)
        {
            var p = ScriptableObject.CreateInstance<PowerupDefinition>();
            p.id = id; p.displayName = name; p.description = desc;
            p.kind = kind; p.duration = duration; p.magnitude = magnitude;
            p.color = color; p.glyph = glyph;
            return p;
        }

        // ---------------------------------------------------------------- bosses

        static void BuildBosses()
        {
            Vulcan = ScriptableObject.CreateInstance<BossDefinition>();
            Vulcan.id = "vulcan";
            Vulcan.displayName = "VULCAN-9 OVERLORD";
            Vulcan.description = "Decommissioned foundry warden. Still warm.";
            Vulcan.maxHealth = 2800f;
            Vulcan.phases.Add(new BossPhase
            {
                name = "PHASE I - WARM-UP",
                healthFraction = 1f, moveSpeed = 3.1f, attackInterval = 2.6f,
                barrageCount = 4, barrageSpread = 26f,
            });
            Vulcan.phases.Add(new BossPhase
            {
                name = "PHASE II - FOUNDRY FURY",
                healthFraction = .66f, moveSpeed = 3.9f, attackInterval = 1.9f,
                barrageCount = 6, barrageSpread = 32f,
                groundSlam = true, summon = true, summonCount = 3,
            });
            Vulcan.phases.Add(new BossPhase
            {
                name = "PHASE III - MELTDOWN",
                healthFraction = .33f, moveSpeed = 4.6f, attackInterval = 1.4f,
                barrageCount = 9, barrageSpread = 40f,
                groundSlam = true, charge = true, summon = true, summonCount = 4,
            });
            Bosses.Add(Vulcan);
        }

        // ---------------------------------------------------------------- waves & modes

        static WaveDefinition Wave(int number, float prep, float eliteChance, params WaveEntry[] entries)
        {
            var w = ScriptableObject.CreateInstance<WaveDefinition>();
            w.waveNumber = number; w.prepTime = prep; w.eliteChance = eliteChance;
            w.entries.AddRange(entries);
            return w;
        }

        static WaveEntry WE(EnemyKind kind, int count) => new WaveEntry { kind = kind, count = count };

        static void BuildWavesAndModes()
        {
            // --- Survival: 12 authored waves, wave 11 is the boss.
            var survival = ScriptableObject.CreateInstance<GameModeDefinition>();
            survival.id = "survival";
            survival.displayName = "SURVIVAL";
            survival.description = "Twelve escalating waves. Clear the arena, chase the score.";
            survival.kind = GameModeKind.Survival;
            survival.scriptedWaves.Add(Wave(1, 5f, 0f, WE(EnemyKind.Grunt, 3)));
            survival.scriptedWaves.Add(Wave(2, 5f, 0f, WE(EnemyKind.Grunt, 4), WE(EnemyKind.Rusher, 1)));
            survival.scriptedWaves.Add(Wave(3, 5f, 0f, WE(EnemyKind.Grunt, 3), WE(EnemyKind.Rusher, 2), WE(EnemyKind.Soldier, 1)));
            survival.scriptedWaves.Add(Wave(4, 5f, .1f, WE(EnemyKind.Grunt, 2), WE(EnemyKind.Rusher, 2), WE(EnemyKind.Soldier, 2)));
            survival.scriptedWaves.Add(Wave(5, 5f, .15f, WE(EnemyKind.Rusher, 2), WE(EnemyKind.Soldier, 3), WE(EnemyKind.Shotgunner, 1)));
            survival.scriptedWaves.Add(Wave(6, 5f, .15f, WE(EnemyKind.Soldier, 2), WE(EnemyKind.Shotgunner, 2), WE(EnemyKind.Drone, 1)));
            survival.scriptedWaves.Add(Wave(7, 6f, .2f, WE(EnemyKind.Soldier, 3), WE(EnemyKind.Assault, 2), WE(EnemyKind.Medic, 1)));
            survival.scriptedWaves.Add(Wave(8, 6f, .25f, WE(EnemyKind.Assault, 2), WE(EnemyKind.Shotgunner, 2), WE(EnemyKind.Sniper, 2), WE(EnemyKind.Heavy, 1)));
            survival.scriptedWaves.Add(Wave(9, 6f, .25f, WE(EnemyKind.Assault, 2), WE(EnemyKind.Explosive, 2), WE(EnemyKind.Drone, 2), WE(EnemyKind.Heavy, 1), WE(EnemyKind.Medic, 1)));
            survival.scriptedWaves.Add(Wave(10, 6f, .3f, WE(EnemyKind.Rusher, 4), WE(EnemyKind.Assault, 3), WE(EnemyKind.Sniper, 2), WE(EnemyKind.Tank, 1)));
            var bossWave = ScriptableObject.CreateInstance<WaveDefinition>();
            bossWave.waveNumber = 11; bossWave.prepTime = 7f; bossWave.isBossWave = true;
            bossWave.bossId = "vulcan"; bossWave.entries.Add(WE(EnemyKind.Grunt, 3));
            survival.scriptedWaves.Add(bossWave);
            survival.scriptedWaves.Add(Wave(12, 6f, .35f,
                WE(EnemyKind.Assault, 3), WE(EnemyKind.Shotgunner, 3), WE(EnemyKind.Explosive, 2), WE(EnemyKind.Medic, 2), WE(EnemyKind.Tank, 1)));
            survival.endlessAfterScripted = false;
            Modes.Add(survival);

            // --- Time Attack: generated waves, kill quota, best time.
            var timeAttack = ScriptableObject.CreateInstance<GameModeDefinition>();
            timeAttack.id = "timeattack";
            timeAttack.displayName = "TIME ATTACK";
            timeAttack.description = "Defeat 40 enemies as fast as you can. The clock is the enemy.";
            timeAttack.kind = GameModeKind.TimeAttack;
            timeAttack.killQuota = 40;
            timeAttack.difficultyScalePerWave = .04f;
            timeAttack.uiColor = new Color(1f, .8f, .25f);
            Modes.Add(timeAttack);

            // --- Boss Rush: three scaled fights.
            var bossRush = ScriptableObject.CreateInstance<GameModeDefinition>();
            bossRush.id = "bossrush";
            bossRush.displayName = "BOSS RUSH";
            bossRush.description = "Three escalating fights with VULCAN-9. No warm-up.";
            bossRush.kind = GameModeKind.BossRush;
            bossRush.bossOrder = new List<string> { "vulcan", "vulcan", "vulcan" };
            bossRush.musicId = "music_boss";
            bossRush.uiColor = new Color(1f, .3f, .25f);
            Modes.Add(bossRush);

            // --- Endless: infinite generated waves, elites invade.
            var endless = ScriptableObject.CreateInstance<GameModeDefinition>();
            endless.id = "endless";
            endless.displayName = "ENDLESS";
            endless.description = "Infinite waves, full enemy variety, elite invasions. High score hunt.";
            endless.kind = GameModeKind.Endless;
            endless.difficultyScalePerWave = .08f;
            endless.varietyRamp = 2f;
            endless.uiColor = new Color(.8f, .4f, 1f);
            Modes.Add(endless);
        }

        // ---------------------------------------------------------------- arenas

        static void BuildArenas()
        {
            Arenas.Add(Arena("industrial", "INDUSTRIAL COMPLEX",
                "Foundry halls, catwalks and container lanes. Tight and vertical.",
                ArenaKind.Industrial, "Arena_Industrial",
                new Color(.2f, .95f, 1f), new Color(.09f, .11f, .15f), .014f));
            Arenas.Add(Arena("desert", "DESERT FACILITY",
                "Sun-bleached compounds and long sightlines. Keep moving.",
                ArenaKind.Desert, "Arena_Desert",
                new Color(1f, .75f, .3f), new Color(.85f, .72f, .5f), .006f));
            Arenas.Add(Arena("city", "ABANDONED CITY",
                "Neon ruins, streets and rooftops. Watch every angle.",
                ArenaKind.City, "Arena_City",
                new Color(.9f, .3f, 1f), new Color(.06f, .05f, .12f), .02f));
        }

        static ArenaDefinition Arena(string id, string name, string desc, ArenaKind kind, string sceneName,
            Color accent, Color fog, float fogDensity)
        {
            var a = ScriptableObject.CreateInstance<ArenaDefinition>();
            a.id = id; a.displayName = name; a.description = desc;
            a.kind = kind; a.sceneName = sceneName;
            a.uiAccent = accent; a.fogColor = fog; a.fogDensity = fogDensity;
            return a;
        }

        // ---------------------------------------------------------------- challenges

        static ChallengeDefinition Challenge(string id, string title, string desc, ChallengeMetric metric,
            float goal, int xp, long currency, bool cumulative = true, string payload = null, string cosmetic = null)
        {
            var c = ScriptableObject.CreateInstance<ChallengeDefinition>();
            c.id = id; c.title = title; c.description = desc;
            c.metric = metric; c.goal = goal; c.cumulative = cumulative;
            c.payload = payload; c.rewardXp = xp; c.rewardCurrency = currency;
            c.rewardCosmetic = cosmetic;
            Challenges.Add(c);
            return c;
        }

        static void BuildChallenges()
        {
            Challenge("first_blood", "FIRST BLOOD", "Defeat 10 enemies.", ChallengeMetric.TotalKills, 10, 100, 100);
            Challenge("headhunter", "HEADHUNTER", "Land 20 headshot kills.", ChallengeMetric.HeadshotKills, 20, 200, 250);
            Challenge("combo_artist", "COMBO ARTIST", "Reach a x6 combo.", ChallengeMetric.ReachCombo, 6, 150, 300, cumulative: false);
            Challenge("untouchable", "UNTOUCHABLE", "Clear 5 waves without taking damage.", ChallengeMetric.WaveNoDamage, 5, 200, 250);
            Challenge("demolitionist", "DEMOLITIONIST", "Kill 5 enemies with explosive barrels.", ChallengeMetric.BarrelKills, 5, 150, 250);
            Challenge("speed_demon", "SPEED DEMON", "Finish Time Attack under 180 seconds.", ChallengeMetric.TimeAttackUnder, 180, 300, 400, cumulative: false);
            Challenge("cold_hands", "COLD HANDS", "Defeat a boss without healing.", ChallengeMetric.BossNoHeal, 1, 250, 500, cumulative: false);
            Challenge("deep_run", "DEEP RUN", "Reach wave 10 in Endless.", ChallengeMetric.ReachWave, 10, 300, 400, cumulative: false);
            Challenge("longshot_hero", "LONGSHOT HERO", "50 kills with the MR-12.", ChallengeMetric.WeaponKills, 50, 200, 300, payload: "marksman");
            Challenge("boom_operator", "BOOM OPERATOR", "30 explosive kills (rockets, grenades, plasma).", ChallengeMetric.ExplosiveKills, 30, 200, 300);
            Challenge("elite_hunter", "ELITE HUNTER", "Kill 15 elite enemies.", ChallengeMetric.EliteKills, 15, 300, 400);
            Challenge("boss_slayer", "BOSS SLAYER", "Defeat VULCAN-9.", ChallengeMetric.BossKills, 1, 400, 600, cosmetic: "gold");
        }

        // ---------------------------------------------------------------- upgrades

        static PlayerUpgradeDefinition UpgradeDef(string id, string name, string desc,
            PlayerUpgradeKind kind, int maxTier, long baseCost, float valuePerTier)
        {
            var u = ScriptableObject.CreateInstance<PlayerUpgradeDefinition>();
            u.id = id; u.displayName = name; u.description = desc;
            u.kind = kind; u.maxTier = maxTier; u.baseCost = baseCost; u.valuePerTier = valuePerTier;
            return u;
        }

        static void BuildPlayerUpgrades()
        {
            PlayerUpgrades.Add(UpgradeDef("health", "REINFORCED FRAME", "+25 max health per tier.",
                PlayerUpgradeKind.MaxHealth, 3, 150, 25f));
            PlayerUpgrades.Add(UpgradeDef("speed", "LIGHT BOOTS", "+5% move speed per tier.",
                PlayerUpgradeKind.MoveSpeed, 3, 150, .05f));
            PlayerUpgrades.Add(UpgradeDef("dash", "PHASE CAPACITOR", "Dash recharges 15% faster per tier.",
                PlayerUpgradeKind.DashRecharge, 3, 175, .15f));
            PlayerUpgrades.Add(UpgradeDef("armor", "COMBAT PLATING", "+25 starting armour per tier.",
                PlayerUpgradeKind.StartArmor, 2, 200, 25f));
            PlayerUpgrades.Add(UpgradeDef("regen", "NANO KNITTING", "Regenerate 1 HP/s after 5s without damage.",
                PlayerUpgradeKind.Regen, 2, 300, 1f));
            PlayerUpgrades.Add(UpgradeDef("weapondamage", "MATCH AMMO", "+8% weapon damage per tier.",
                PlayerUpgradeKind.WeaponDamage, 3, 250, .08f));
        }

        // ---------------------------------------------------------------- crosshairs

        static void BuildCrosshairs()
        {
            Crosshairs.Add(new CrosshairStyle("cyan", "CYON", new Color(.2f, .95f, 1f)));
            Crosshairs.Add(new CrosshairStyle("gold", "AURUM", new Color(1f, .8f, .2f)));
            Crosshairs.Add(new CrosshairStyle("magenta", "VOLT", new Color(.95f, .25f, 1f)));
            Crosshairs.Add(new CrosshairStyle("green", "SERAPH", new Color(.35f, 1f, .45f)));
            Crosshairs.Add(new CrosshairStyle("white", "GHOST", new Color(.95f, .95f, .95f)));
        }
    }
}
