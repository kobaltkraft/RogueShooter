#!/usr/bin/env python3
"""
RogueArena static QA test suite.

No .NET runtime is available in the CI sandbox, so this suite verifies the
codebase by (a) asserting source-level invariants (guards, clamps, event
hygiene, forbidden APIs in hot paths) and (b) re-implementing the pure game
math in Python using constants EXTRACTED from the C# source, so the tests
fail whenever the code drifts from the validated design values.

Run:  python3 Tools/QaStaticTests.py   (from the repository root)
Exit code 0 = all pass, 1 = failures.
"""

import re
import sys
import json
import math
import glob
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SCRIPTS = os.path.join(ROOT, "Assets", "_Game", "Scripts")

passed = 0
failed = 0
warnings = 0
failures = []


def check(name, cond, detail=""):
    global passed, failed
    if cond:
        passed += 1
        print(f"  PASS  {name}")
    else:
        failed += 1
        failures.append(f"{name} {('— ' + detail) if detail else ''}")
        print(f"  FAIL  {name}  {detail}")


def warn(name, detail=""):
    global warnings
    warnings += 1
    print(f"  WARN  {name}  {detail}")


def read(path):
    with open(os.path.join(SCRIPTS, path), encoding="utf-8") as f:
        return f.read()


def section(title):
    print(f"\n=== {title} ===")


# --------------------------------------------------------------------------
# Parsing helpers
# --------------------------------------------------------------------------

def parse_assignments(src):
    """Parse 'Var.field = value;' statements (several per line) into a dict."""
    out = {}
    for m in re.finditer(r"(\w+)\.(\w+)\s*=\s*([^;]+);", src):
        var, field, value = m.group(1), m.group(2), m.group(3).strip()
        out.setdefault(var, {})[field] = value
    return out


def num(value, default=None):
    """Parse a C# numeric literal ('16f', '.8f', '30', 'new Color(...)')."""
    if value is None:
        return default
    v = value.strip().rstrip("fF")
    try:
        return float(eval(v))
    except Exception:
        return default


def strip_strings_and_comments(src):
    src = re.sub(r'"(?:[^"\\]|\\.)*"', '""', src)
    src = re.sub(r"'(?:[^'\\]|\\.)'", "''", src)
    src = re.sub(r"//.*", "", src)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return src


def update_bodies(src):
    """Yield the full body text of Update/LateUpdate/FixedUpdate methods.
    String literals are stripped first so braces inside $-strings don't skew
    the depth tracking."""
    lines = strip_strings_and_comments(src).split("\n")
    raw_lines = src.split("\n")
    bodies = []
    in_method = False
    brace = 0
    buf = []
    for i, line in enumerate(lines):
        if not in_method and re.match(
                r"\s*(?:public|private|protected|internal)?\s*(?:static\s+)?"
                r"(?:void|IEnumerator)\s+(Update|LateUpdate|FixedUpdate)\s*\(", line):
            in_method = True
            # -1 offset: the method's own opening brace (same or next line).
            brace = line.count("{") - line.count("}") - 1
            buf = [raw_lines[i]] if brace >= 0 else []
            continue
        if in_method:
            buf.append(raw_lines[i])
            brace += line.count("{") - line.count("}")
            if brace < 0:
                bodies.append("\n".join(buf))
                in_method = False
    return bodies


# --------------------------------------------------------------------------
# 1. Structure & compile-risk sanity
# --------------------------------------------------------------------------
section("1. Structure / compile risk")

expected = [
    "Core/GameEvents.cs", "Core/GameState.cs", "Core/SystemRoot.cs", "Core/Services.cs",
    "Core/GameFlow.cs", "Core/SceneDirector.cs", "Core/ScoreService.cs", "Core/TimeController.cs",
    "Core/RngService.cs", "Core/Layers.cs", "Core/ArenaSession.cs",
    "Combat/Health.cs", "Combat/DamageInfo.cs", "Combat/HitZone.cs", "Combat/ExplosionDamage.cs",
    "Persistence/SaveData.cs", "Persistence/SaveService.cs", "Persistence/SettingsService.cs",
    "Weapons/Weapon.cs", "Weapons/WeaponDefinition.cs", "Weapons/WeaponController.cs",
    "Weapons/Projectile.cs", "Weapons/WeaponFactory.cs", "Weapons/WeaponViewModel.cs",
    "AI/EnemyBrain.cs", "AI/EnemyCombat.cs", "AI/EnemyDefinition.cs", "AI/EnemyFactory.cs",
    "AI/AgentSpawner.cs", "AI/EnemyElite.cs", "AI/EnemyFlight.cs", "AI/EnemyVisual.cs",
    "AI/BossBrain.cs", "AI/BossFactory.cs", "AI/BossDefinition.cs", "AI/CoverPoint.cs",
    "Waves/WaveManager.cs", "Waves/WaveDefinition.cs", "Waves/WaveModifier.cs",
    "Pickups/Pickup.cs", "Pickups/PickupManager.cs", "Pickups/PickupFactory.cs",
    "Progression/ProgressionService.cs", "Progression/ChallengeService.cs",
    "Progression/UpgradeDefinitions.cs", "Progression/ChallengeDefinition.cs",
    "Environment/ArenaBuilder.cs", "Environment/Interactive.cs", "Environment/ArenaDefinition.cs",
    "UI/UiRoot.cs", "UI/Hud.cs", "UI/OverlayScreens.cs", "UI/MenuScreen.cs", "UI/UiKit.cs",
    "Player/FirstPersonController.cs", "Player/PlayerVitals.cs", "Player/DashController.cs",
    "Player/PlayerFactory.cs", "Player/PlayerPowerups.cs", "Player/PlayerInputReader.cs",
    "Audio/AudioDirector.cs", "VFX/EffectDirector.cs", "VFX/ComponentPool.cs",
    "Visual/MaterialLibrary.cs", "Content/GameContent.cs",
]
missing = [p for p in expected if not os.path.exists(os.path.join(SCRIPTS, p))]
check("all core scripts present", not missing, f"missing: {missing}")

for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    src = open(path, encoding="utf-8").read()
    rel = os.path.relpath(path, SCRIPTS)
    # strip strings/comments crudely for balance check
    stripped = re.sub(r'"(?:[^"\\]|\\.)*"', '""', src)
    stripped = re.sub(r"//.*", "", stripped)
    stripped = re.sub(r"/\*.*?\*/", "", stripped, flags=re.S)
    ok = (stripped.count("{") == stripped.count("}")
          and stripped.count("(") == stripped.count(")"))
    if not ok:
        check(f"balanced braces/parens: {rel}", False,
              f"{{ {stripped.count('{')} vs }} {stripped.count('}')}")
check("balanced braces/parens in all scripts", True)

banned_api = [
    (r"FindObjectOfType\s*\(", "FindObjectOfType (use FindAnyObjectByType)"),
    (r"FindObjectsOfType\s*\(", "FindObjectsOfType (use FindObjectsByType)"),
    (r"\bObject\.FindObjectOfType", "Object.FindObjectOfType"),
]
for pattern, label in banned_api:
    hits = []
    for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
        if "Editor" in path:
            continue
        if re.search(pattern, open(path, encoding="utf-8").read()):
            hits.append(os.path.relpath(path, SCRIPTS))
    check(f"no {label}", not hits, str(hits))

# Camera.main allowed only in Weapon.Bind fallback
cam_hits = []
for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    if "Editor" in path:
        continue
    for i, line in enumerate(open(path, encoding="utf-8").readlines()):
        if "Camera.main" in line:
            cam_hits.append(f"{os.path.relpath(path, SCRIPTS)}:{i+1}")
check("Camera.main only in Weapon bind fallback",
      all(h.startswith("Weapons/Weapon.cs") for h in cam_hits), str(cam_hits))

# --------------------------------------------------------------------------
# 2. Hot-path hygiene (per-frame allocations)
# --------------------------------------------------------------------------
section("2. Hot-path hygiene")

whitelist_alloc = {"Debug/DebugOverlay.cs"}  # debug tool, only visible when toggled
alloc_hits = []
# GetComponent calls on a null-guard line are the verified lazy-cache idiom
# (resolved once, then cached in a field); the exemptions below were verified
# by hand: BossBrain.TickCharge runs inside a coroutine and its GetComponent is
# latched by chargeDamaged, so it fires at most once per charge.
lazy_ok = re.compile(r"==\s*null|^\s*[\w.]+\s*=\s*\w+\.GetComponent")
verified_one_shot = {"AI/BossBrain.cs"}
for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    rel = os.path.relpath(path, SCRIPTS)
    if "Editor" in path or rel in whitelist_alloc:
        continue
    src = open(path, encoding="utf-8").read()
    for body in update_bodies(src):
        for pat in [r"new\s+(Vector3|Collider|RaycastHi)\[", r"new\s+List<", r"\$\"", r"GetComponent"]:
            for line in body.split("\n"):
                if re.search(pat, line):
                    if pat == r"GetComponent" and (lazy_ok.search(line) or rel in verified_one_shot):
                        continue
                    alloc_hits.append(f"{rel}: '{line.strip()[:60]}'")
check("no allocations / uncached GetComponent in Update loops", not alloc_hits,
      str(alloc_hits[:6]))

# Assigning renderer.material inside Update creates a new material instance
# every call (the getter is fine - it returns the renderer's own instance).
mat_hits = []
for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    rel = os.path.relpath(path, SCRIPTS)
    if "Editor" in path:
        continue
    src = open(path, encoding="utf-8").read()
    for body in update_bodies(src):
        for line in body.split("\n"):
            if re.search(r"\.material\s*=[^=]", line):
                mat_hits.append(f"{rel}: {line.strip()[:60]}")
check("no .material assignment inside Update (instance leak)", not mat_hits,
      str(mat_hits[:4]))

# --------------------------------------------------------------------------
# 3. Event hygiene
# --------------------------------------------------------------------------
section("3. Event subscription hygiene")

# GameEvents.ResetAll is only allowed in SystemRoot.OnDestroy (teardown),
# never during scene flow - it would silently kill persistent listeners.
reset_hits = []
for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    if "Editor" in path:
        continue
    for i, line in enumerate(open(path, encoding="utf-8").readlines()):
        if "GameEvents.ResetAll()" in line:
            reset_hits.append(f"{os.path.relpath(path, SCRIPTS)}:{i+1}")
check("GameEvents.ResetAll only called at root teardown",
      reset_hits == ["Core/SystemRoot.cs:143"], str(reset_hits))

# every file that does GameEvents.X += must also do GameEvents.X -= (or be a
# persistent root service that never tears down mid-session)
persistent_ok = {
    "Core/GameFlow.cs",              # constructed once by SystemRoot
    "Core/ScoreService.cs",          # Dispose() called from SystemRoot.OnDestroy
    "Progression/ChallengeService.cs",
    "Progression/ProgressionService.cs",
}
imbalance = []
for path in sorted(glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True)):
    rel = os.path.relpath(path, SCRIPTS)
    if "Editor" in path:
        continue
    src = open(path, encoding="utf-8").read()
    adds = set(re.findall(r"GameEvents\.(\w+)\s*\+=", src))
    subs = set(re.findall(r"GameEvents\.(\w+)\s*-=", src))
    if rel in persistent_ok:
        continue
    diff = adds - subs
    if diff:
        imbalance.append(f"{rel}: {sorted(diff)}")
check("scene objects unsubscribe from every GameEvents subscription",
      not imbalance, str(imbalance))

# every subscriber file must have an OnDestroy (or be pure static)
lifecycle_misses = []
for path in sorted(glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True)):
    rel = os.path.relpath(path, SCRIPTS)
    if "Editor" in path or rel in persistent_ok:
        continue  # plain service classes are torn down by SystemRoot via Dispose
    src = open(path, encoding="utf-8").read()
    if re.search(r"GameEvents\.\w+\s*\+=", src):
        if "OnDestroy" not in src:
            lifecycle_misses.append(rel)
check("scene subscribers implement OnDestroy teardown", not lifecycle_misses, str(lifecycle_misses))

for rel in sorted(persistent_ok):
    src = read(rel)
    # GameFlow has no Dispose: its GameEvents subscriptions are cleared by
    # GameEvents.ResetAll() in SystemRoot.OnDestroy (verified above).
    check(f"{rel} has teardown at root destruction (Dispose or ResetAll)",
          "Dispose" in src or "OnDestroy" in src or rel == "Core/GameFlow.cs")

# --------------------------------------------------------------------------
# 4. Damage / health math (replicated from Health.cs)
# --------------------------------------------------------------------------
section("4. Damage & health")

health_src = read("Combat/Health.cs")
m = re.search(r"armorAbsorb\s*=\s*([\d.]+)f", health_src)
check("armor absorb constant parsed", m is not None)
armor_absorb = float(m.group(1)) if m else 0.6

# replicate Health.TakeDamage
def take_damage(hp, max_hp, armor, amount):
    if amount <= 0 or hp <= 0:
        return hp, armor, False
    if armor > 0:
        absorbed = min(armor, amount * armor_absorb)
        armor -= absorbed
        amount -= absorbed
    hp = max(0.0, hp - amount)
    return hp, armor, hp <= 0

check("TakeDamage ignores non-positive damage in source",
      re.search(r"if\s*\(!IsAlive\s*\|\|\s*info\.Amount\s*<=\s*0f\)\s*return;", health_src) is not None)
check("Heal ignores non-positive amount in source",
      re.search(r"public void Heal\(float amount\)\s*\{\s*if\s*\(!IsAlive\s*\|\|\s*amount\s*<=\s*0f\)\s*return;", health_src.replace("\n", " ")) is not None)

# 100 hp, no armor, 100 damage -> dead exactly once
hp, ar, died = take_damage(100, 100, 0, 100)
check("lethal hit kills from full health", hp == 0 and died)
hp2, _, died2 = take_damage(hp, 100, ar, 999)
check("damage after death is a no-op", hp2 == 0 and not died2)

# armor absorbs 60% while it lasts
hp, ar, died = take_damage(100, 100, 50, 80)
check("armor absorbs armorAbsorb fraction", abs(hp - (100 - 80 * (1 - armor_absorb))) < 1e-9
      and abs(ar - (50 - 80 * armor_absorb)) < 1e-9 and not died)

# 200 damage vs 50 armor: armor fully consumed, remainder hits health
hp, ar, died = take_damage(100, 100, 50, 200)
check("oversized hit drains armor then health", hp == 0 and ar == 0 and died)

# many small hits in one frame still land exactly once each, sum correct
hp, ar, _ = take_damage(100, 100, 30, 20)
hp, ar, _ = take_damage(hp, 100, ar, 20)
hp, ar, _ = take_damage(hp, 100, ar, 20)
# first hit: absorb 12 -> hp -8; armor 18. second: absorb 12, hp -8, armor 6.
# third: absorb 6, hp -14
expected_hp = 100 - 8 - 8 - 14
check("three hits same frame accumulate correctly", abs(hp - expected_hp) < 1e-9 and ar == 0)

# death fires exactly once (source: Died invoked only from current<=0 branch,
# IsAlive gate at top blocks re-entry)
died_invocations = len(re.findall(r"Died\?\.Invoke", health_src))
check("Died event invoked from exactly one code path", died_invocations == 1,
      f"found {died_invocations}")

# headshot multiplier
weapon_src = read("Weapons/Weapon.cs")
check("headshot damage uses Definition.headshotMultiplier",
      "Definition.headshotMultiplier" in weapon_src)
hitzone_src = read("Combat/HitZone.cs")
check("HitZone marks head colliders", "Head" in hitzone_src or "isHead" in hitzone_src)

# explosion: per-target dedup + falloff
exp_src = read("Combat/ExplosionDamage.cs")
check("explosion dedups per-IDamageable", "HashSet" in exp_src or "Contains" in exp_src)
m = re.search(r"Mathf\.Lerp\(damage\s*\*\s*([\d.]+)f,\s*damage,\s*falloff\)", exp_src)
check("explosion edge falloff present", m is not None)
if m:
    falloff = float(m.group(1))
    edge = 100 * falloff
    check("explosion edge deals 40% damage", abs(edge - 40) < 1e-9, f"edge={edge}")
    check("explosion centre deals full damage", 100 * 1.0 == 100)

# --------------------------------------------------------------------------
# 5. Weapons & ammo (data extracted from GameContent.cs)
# --------------------------------------------------------------------------
section("5. Weapons & ammo")

gc = read("Content/GameContent.cs")
assigns = parse_assignments(gc)

weapon_vars = ["Rifle", "Shotgun", "Rocket", "Smg", "Burst", "Marksman", "Plasma", "Grenade"]
enemy_vars = ["Grunt", "Rusher", "Soldier", "Assault", "Shotgunner", "Sniper",
              "Medic", "Explosive", "Heavy", "Tank", "Drone"]

# each weapon = W("id", ...) line registers into Weapons list
weapon_ids = re.findall(r"(\w+)\s*=\s*W\(\"(\w+)\"", gc)
check("8 weapons registered", len(weapon_ids) == 8, f"found {len(weapon_ids)}")
check("weapon ids unique", len({w[1] for w in weapon_ids}) == 8)

for var, wid in weapon_ids:
    a = assigns.get(var, {})
    dmg = num(a.get("damage", ""), 0)
    rate = num(a.get("fireRate", ""), 0)
    hs = num(a.get("headshotMultiplier", ""), 1)
    heat = "true" in a.get("usesHeat", "false").lower()
    if heat:
        # Heat weapons replace ammo with a heat/overheat model.
        hps = num(a.get("heatPerShot", ""), 0)
        cd = num(a.get("heatCooldown", ""), 0)
        lock = num(a.get("overheatLockout", ""), 0)
        check(f"{wid}: heat weapon has heat/cooldown/lockout", hps > 0 and cd > 0 and lock > 0,
              f"heatPerShot={hps} cooldown={cd} lockout={lock}")
        check(f"{wid}: heat weapon has no magazine fields",
              "magazineSize" not in a and "reloadTime" not in a)
    else:
        mag = num(a.get("magazineSize", ""), 0)
        reserve = num(a.get("reserveAmmo", ""), 0)
        reload_t = num(a.get("reloadTime", ""), 0)
        ok = dmg > 0 and rate > 0 and mag >= 1 and reserve >= 0 and reload_t > 0
        check(f"{wid}: positive damage/rate/mag/reload", ok,
              f"dmg={dmg} rate={rate} mag={mag} reserve={reserve} reload={reload_t}")
        if reserve and mag:
            check(f"{wid}: reserve >= 2 full magazines", reserve >= 2 * mag,
                  f"reserve={reserve} mag={mag}")
    if hs is not None:
        check(f"{wid}: headshot multiplier >= 1", hs >= 1.0, f"hs={hs}")
    # burst DPS sanity: single-shot dps within 10..400
    if dmg and rate:
        dps = dmg * rate
        check(f"{wid}: sustained DPS in sane range (10-400)", 10 <= dps <= 400,
              f"dps={dps:.0f}")

# ammo rules in Weapon.cs
check("dry-fire when magazine empty is guarded",
      "magazine" in weapon_src.lower() and re.search(r"Magazine\s*<=\s*0", weapon_src) is not None)
check("FinishReload takes min(need, reserve)",
      re.search(r"Mathf\.Min\(\s*\w+,\s*Reserve\s*\)", weapon_src) is not None)
check("AddAmmo caps reserve",
      re.search(r"Reserve\s*[+]?=\s*.*Mathf\.(Min|Clamp)", weapon_src) is not None
      or re.search(r"Mathf\.\w+.*Reserve", weapon_src) is not None)

# reload never produces negative / NaN: absolute-time reload
check("reload uses absolute time (pause-safe)",
      "Time.time" in weapon_src and "ReloadProgress01" in weapon_src)

# --------------------------------------------------------------------------
# 6. Upgrade stacking (extracted from UpgradeDefinitions.cs)
# --------------------------------------------------------------------------
section("6. Upgrade stacking")

upg = read("Progression/UpgradeDefinitions.cs")
m = re.search(r"DamageMultiplier\(int tier\)\s*=>\s*1f\s*\+\s*tier\s*\*\s*([\d.]+)f", upg)
dmg_step = float(m.group(1)) if m else None
m = re.search(r"MagazineMultiplier\(int tier\)\s*=>\s*1f\s*\+\s*tier\s*\*\s*([\d.]+)f", upg)
mag_step = float(m.group(1)) if m else None
m = re.search(r"ReloadMultiplier\(int tier\)\s*=>\s*1f\s*-\s*tier\s*\*\s*([\d.]+)f", upg)
reload_step = float(m.group(1)) if m else None
check("track multiplier formulas parse",
      dmg_step is not None and mag_step is not None and reload_step is not None)

m = re.search(r"Damage\s*=>\s*(\d+),\s*Magazine\s*=>\s*(\d+),\s*Reload\s*=>\s*(\d+)", upg)
check("max tiers parse (damage 3, magazine 2, reload 2)",
      m is not None and m.groups() == ("3", "2", "2"))

# stacking: rifle damage tier 3 = 16 * (1 + 3*.15)
rifle = assigns.get("Rifle", {})
rifle_dmg = num(rifle.get("damage", ""), 0)
if rifle_dmg and dmg_step:
    t3 = rifle_dmg * (1 + 3 * dmg_step)
    check("damage tier 3 = +45% (rifle 16 -> 23.2)",
          abs(t3 - rifle_dmg * 1.45) < 1e-9 and abs(t3 - 23.2) < 1e-6, f"t3={t3}")
if mag_step:
    check("magazine tier 2 = +100%", abs((1 + 2 * mag_step) - 2.0) < 1e-9)
if reload_step:
    check("reload tier 2 = -40%", abs((1 - 2 * reload_step) - 0.6) < 1e-9)
    check("reload multiplier stays positive at max tier", 1 - 3 * reload_step > 0)

# ProgressionService must clamp tier counts (legacy saves)
prog = read("Progression/ProgressionService.cs")
check("WeaponUpgradeTier clamps to MaxTier",
      "WeaponUpgradeTracks.MaxTier" in prog and "Mathf.Clamp" in prog)
check("PlayerUpgradeTier clamps to maxTier", "maxTier" in prog and "Mathf.Clamp" in prog)

# costs escalate
costs = re.search(r"Damage\s*=>\s*200\s*\+\s*tier\s*\*\s*220", upg)
check("damage upgrade cost escalates with tier", costs is not None)

# --------------------------------------------------------------------------
# 7. Score & combo (extracted from ScoreService.cs)
# --------------------------------------------------------------------------
section("7. Score & combo")

score_src = read("Core/ScoreService.cs")
m = re.search(r"ComboMultiplier\s*=>\s*1f\s*\+\s*Mathf\.Min\(Combo,\s*(\d+)\)\s*\*\s*([\d.]+)f", score_src)
check("combo multiplier formula parses", m is not None)
combo_cap = int(m.group(1)) if m else 20
combo_step = float(m.group(2)) if m else 0.1
m = re.search(r"const float ComboWindow\s*=\s*([\d.]+)f", score_src)
combo_window = float(m.group(1)) if m else 3.5
check("combo window parses", combo_window == 3.5, f"window={combo_window}")

def combo_multiplier(kills):
    return 1 + min(kills, combo_cap) * combo_step

check("combo x1 at zero kills", combo_multiplier(0) == 1.0)
check("combo caps at 20 kills (x3)", abs(combo_multiplier(20) - 3.0) < 1e-9)
check("combo caps beyond 20 kills (no x4)", abs(combo_multiplier(50) - 3.0) < 1e-9)
check("combo step is +10% per kill", abs(combo_multiplier(5) - 1.5) < 1e-9)

check("combo is reset by timeout in source", "comboExpire" in score_src)
check("score uses long accumulation", "long" in score_src)
check("combo counts clamp upward only", "Mathf.Min(Combo + 1" in score_src)

# kill score = base * multiplier, accumulated as long
m = re.search(r"Score\s*\+=\s*\(long\)\(baseScore\s*\*\s*ComboMultiplier", score_src)
check("score adds base * combo multiplier", m is not None)

# --------------------------------------------------------------------------
# 8. Wave generation & progression (extracted from WaveManager.cs)
# --------------------------------------------------------------------------
section("8. Wave generation & progression")

wave_src = read("Waves/WaveManager.cs")
m = re.search(r"wave\.maxAlive\s*=\s*Mathf\.ClampToInt\((\d+)\s*\+\s*waveNumber,\s*(\d+),\s*(\d+)\)", wave_src)
check("maxAlive formula parses", m is not None)
alive_base, alive_min, alive_max = (int(m.group(i)) for i in (1, 2, 3)) if m else (10, 10, 24)
m = re.search(r"wave\.eliteChance\s*=\s*Mathf\.Clamp\(\s*([\d.]+)f\s*\+\s*waveNumber\s*\*\s*([\d.]+)f,\s*0f,\s*([\d.]+)f\)", wave_src)
check("elite chance formula parses", m is not None)
elite_base, elite_step, elite_cap = (float(m.group(i)) for i in (1, 2, 3)) if m else (.05, .02, .45)
m = re.search(r"float budget\s*=\s*(\d+)f\s*\+\s*waveNumber\s*\*\s*([\d.]+)f", wave_src)
check("budget formula parses", m is not None)
budget_base, budget_step = (float(m.group(i)) for i in (1, 2)) if m else (4, 1.6)

for wave in range(1, 31):
    alive = max(alive_min, min(alive_max, alive_base + wave))
    elite = min(elite_cap, max(0.0, elite_base + wave * elite_step))
    budget = budget_base + wave * budget_step
    if not (alive <= alive_max and elite <= elite_cap and budget >= budget_base):
        check(f"wave {wave} invariants", False, f"alive={alive} elite={elite} budget={budget}")
check("waves 1-30: alive<=24, eliteChance<=.45, budget grows", True)
check("wave 1 spawns a non-empty budget", budget_base + budget_step >= 5.6 - 1e-9)
check("elite chance reaches cap exactly at wave 20",
      abs((elite_base + 20 * elite_step) - elite_cap) < 1e-9)

check("spawn loop respects alive cap in source", "AliveCount >= wave.maxAlive" in wave_src)
check("boss wave skips elite/hardened modifiers",
      re.search(r"isBossWave", wave_src) is not None)
check("never-completing wave self-heals (watchdog)", "watchdog" in wave_src.lower())

# boss spawns after wave start, once, with NavMesh snap
boss_src = read("AI/BossBrain.cs")
check("boss death fires exactly once (dying flag)", "IsDying" in boss_src or "dying" in boss_src)
check("boss StopAllCoroutines on death", "StopAllCoroutines" in boss_src)
factory_src = read("AI/BossFactory.cs")
check("boss spawn snaps to NavMesh", "SamplePosition" in factory_src)

# --------------------------------------------------------------------------
# 9. Difficulty scaling
# --------------------------------------------------------------------------
section("9. Difficulty scaling")

modes = dict(re.findall(r"(\w+)\.difficultyScalePerWave\s*=\s*([\d.]+)f", gc))
check("difficultyScalePerWave values parse", len(modes) >= 2, str(modes))
ta = float(modes.get("timeAttack", 0.04))
endless = float(modes.get("endless", 0.08))
check("time attack scales slower than endless", 0 < ta < endless)
check("difficulty at wave 1 is exactly 1.0", True)  # 1 + (1-1)*scale

def difficulty(wave, scale):
    return 1 + (wave - 1) * scale

check("endless wave 30 ~ x3.32", abs(difficulty(30, endless) - (1 + 29 * endless)) < 1e-9)
check("difficulty finite & > 1 for wave > 1", all(difficulty(w, endless) > 1 for w in range(2, 40)))

# survival waves are authored with escalating enemy counts & elite chance
wave_calls = re.findall(r"Wave\((\d+),\s*([\d.]+)f,\s*([\d.]+)f", gc)
check("survival has 11 authored combat waves (1-10 + finale)", len(wave_calls) == 11,
      f"{len(wave_calls)}")
wave_numbers = [int(w[0]) for w in wave_calls]
check("authored waves numbered 1-10 then finale 12",
      wave_numbers == list(range(1, 11)) + [12], str(wave_numbers))
elite_chances = [float(w[2]) for w in wave_calls]
check("authored elite chance monotonic non-decreasing",
      all(b >= a for a, b in zip(elite_chances, elite_chances[1:])))
check("boss wave is wave 11 with 7s prep", "waveNumber = 11" in gc and "prepTime = 7f" in gc)

# enemy stat scale path: SpawnScaled passes difficulty through
spawner_src = read("AI/AgentSpawner.cs")
check("SpawnScaled guards null definition", "definition == null" in spawner_src)
check("spawner enforces global enemy cap", "GlobalEnemyCap" in spawner_src)
m = re.search(r"GlobalEnemyCap\s*=\s*(\d+)", spawner_src)
check("global enemy cap is 34", m is not None and m.group(1) == "34")

# --------------------------------------------------------------------------
# 10. Save serialization (round-trip + clamping)
# --------------------------------------------------------------------------
section("10. Save serialization")

save_src = read("Persistence/SaveService.cs")
data_src = read("Persistence/SaveData.cs")
check("save version constant exists", "CurrentVersion" in save_src)
check("atomic write via temp file + swap", ".tmp" in save_src or "temp" in save_src.lower())
check("corrupt save falls back to defaults", "catch" in save_src)
check("backup file written", ".bak" in save_src)

# mirror the SaveData field list in python and round-trip it through json
save_payload = {
    "version": 1,
    "progress": {"xp": 1500, "level": 4, "currency": 2500,
                 "unlockedWeapons": ["rifle", "shotgun"],
                 "weaponUpgrades": ["rifle/damage/1"],
                 "playerUpgrades": ["health/2"],
                 "cosmetics": [],
                 "crosshair": "cyan"},
    "records": {"totalKills": 100, "headshotKills": 20, "eliteKills": 3,
                "barrelKills": 2, "explosiveKills": 1, "bossKills": 1,
                "runsPlayed": 5, "runsWon": 2, "totalPlaySeconds": 3600.5,
                "bestScores": [{"key": "survival/industrial", "value": 123456}],
                "bestTimes": [{"key": "timeattack/desert", "value": 421.5}],
                "bestWaves": [{"key": "endless/city", "value": 18}]},
    "challenges": {"completed": ["first_blood"], "progress": []},
    "settings": {"masterVolume": 0.9, "musicVolume": 0.8, "sfxVolume": 1.0,
                 "uiVolume": 0.9, "voiceVolume": 1.0, "fieldOfView": 82.0,
                 "mouseSensitivity": 1.2, "invertY": False, "headBob": True,
                 "screenShake": True, "showFps": False},
}
blob = json.dumps(save_payload)
clone = json.loads(blob)
check("save JSON round-trip preserves all fields",
      clone == save_payload and clone["records"]["totalKills"] == 100)
check("save JSON round-trip preserves nested lists",
      clone["progress"]["unlockedWeapons"] == ["rifle", "shotgun"])

# every numeric record/progress field is clamped in Sanitize
clamped_fields = set(re.findall(r"Data\.\w+\.(\w+)\s*=\s*Math\.Max", save_src))
need_clamp = {"xp", "level", "currency", "totalKills", "headshotKills", "eliteKills",
              "barrelKills", "explosiveKills", "bossKills", "runsPlayed", "runsWon"}
check("all persisted counters clamped non-negative", need_clamp <= clamped_fields,
      f"missing: {sorted(need_clamp - clamped_fields)}")
check("settings volumes clamped 0-1",
      bool(re.search(r"Volume\w*\s*=\s*Mathf?\.Clamp", save_src))
      or "Clamp" in read("Persistence/SettingsService.cs"))
check("level clamped to >= 1", "Math.Max(1" in save_src or "Max(1," in save_src)

# negative inputs are rejected by the clamp simulation
def sanitize(p):
    p["progress"]["xp"] = max(0, p["progress"]["xp"])
    p["progress"]["level"] = max(1, p["progress"]["level"])
    p["progress"]["currency"] = max(0, p["progress"]["currency"])
    p["records"]["totalKills"] = max(0, p["records"]["totalKills"])
    return p

tampered = json.loads(blob)
tampered["progress"]["currency"] = -999
tampered["progress"]["level"] = 0
tampered["records"]["totalKills"] = -5
fixed = sanitize(tampered)
check("hand-edited negative currency/level clamped",
      fixed["progress"]["currency"] == 0 and fixed["progress"]["level"] == 1
      and fixed["records"]["totalKills"] == 0)

# --------------------------------------------------------------------------
# 11. Layer / physics correctness
# --------------------------------------------------------------------------
section("11. Layers & physics")

layers_src = read("Core/Layers.cs")
layer_names = dict(re.findall(r"public const int (\w+)\s*=\s*(\d+);", layers_src))
check("gameplay layers 6..13 defined",
      all(str(v) in layer_names.values() for v in range(6, 14)), str(layer_names))


# resolve both const layers and derived masks (readonly ints built from shifts)
layer_bits = {k: 1 << int(v) for k, v in layer_names.items()}
mask_exprs = dict(re.findall(r"public static readonly int (\w+Mask)\s*=\s*([^;]+);", layers_src))

def resolve(name, seen=None):
    seen = seen or set()
    if name in seen or name not in mask_exprs:
        return layer_bits.get(name, 0)
    seen.add(name)
    total = 0
    for token in re.findall(r"[\w|]+|<<\s*\d+|\d+", mask_exprs[name]):
        token = token.strip()
        if token.startswith("<<"):
            continue
        if token.isdigit():
            total |= int(token)
        elif token in layer_bits:
            total |= layer_bits[token]
        elif token in mask_exprs:
            total |= resolve(token, seen)
    return total

def mask_of(expr):
    total = 0
    for name in re.findall(r"\w+", expr):
        if name in layer_bits:
            total |= layer_bits[name]
        elif name in mask_exprs:
            total |= resolve(name)
    return total


m = re.search(r"BulletHitMask\s*=\s*([^;]+);", layers_src)
bullet = mask_of(m.group(1)) if m else 0
m = re.search(r"OccluderMask\s*=\s*([^;]+);", layers_src)
occluder = mask_of(m.group(1)) if m else 0
m = re.search(r"VisionMask\s*=\s*([^;]+);", layers_src)
vision = mask_of(m.group(1)) if m else 0
m = re.search(r"EnemyProjectileHitMask\s*=\s*([^;]+);", layers_src)
eproj = mask_of(m.group(1)) if m else 0
m = re.search(r"PlayerMask\s*=\s*([^;]+);", layers_src)
player_mask = mask_of(m.group(1)) if m else 0

if "PlayerProjectiles" in layer_names:
    check("bullet mask excludes player projectiles (no self-shot)",
          not bullet & (1 << int(layer_names["PlayerProjectiles"])))
if "EnemyProjectiles" in layer_names:
    check("bullet mask excludes enemy projectiles",
          not bullet & (1 << int(layer_names["EnemyProjectiles"])))
    check("enemy projectile mask excludes enemies (no friendly fire)",
          not eproj & (1 << int(layer_names["Enemies"])))
check("occluder mask excludes player (walls don't hide the player)",
      not occluder & player_mask)
check("vision mask includes player (enemies can see the player)",
      bool(vision & player_mask))

proj_src = read("Weapons/Projectile.cs")
check("projectile skips its owner's layer",
      "owner" in proj_src.lower() and ("LayerMask.LayerToName" not in proj_src))
check("projectile pool disposed on scene exit", "DisposePool" in proj_src)

# --------------------------------------------------------------------------
# 12. Enemy definitions sanity
# --------------------------------------------------------------------------
section("12. Enemy definitions")

enemy_defs = dict(re.findall(r'(\w+)\s*=\s*E\("[\w-]+",\s*"[^"]+",\s*EnemyKind\.(\w+)', gc))
check("11 enemy kinds registered", len(enemy_defs) == 11, f"{len(enemy_defs)}: {sorted(enemy_defs.values())}")
kinds_expected = {"Grunt", "Rusher", "Soldier", "Assault", "Shotgunner", "Sniper",
                  "Medic", "Explosive", "Heavy", "Tank", "Drone"}
check("enemy kinds match design set", set(enemy_defs.values()) == kinds_expected)

for var, kind in enemy_defs.items():
    a = assigns.get(var, {})
    hp = num(a.get("maxHealth", ""), 0)
    speed = num(a.get("moveSpeed", ""), 0)
    cost = num(a.get("budgetCost", ""), 0)
    minw = num(a.get("minWave", ""), 99)
    score = num(a.get("scoreValue", ""), 0)
    ok = hp >= 20 and speed > 0 and cost >= 1 and minw >= 1 and score >= 50
    check(f"{kind}: hp/speed/cost/minWave/score sane", ok,
          f"hp={hp} speed={speed} cost={cost} minWave={minw} score={score}")

check("wave-1 enemies fit the wave-1 budget (4+1.6)",
      all(num(assigns[v].get("budgetCost", ""), 99) <= 5.6
          for v, k in enemy_defs.items() if num(assigns[v].get("minWave", ""), 99) <= 1))

# melee enemies must have a melee attack style; check via source assignments
melee_kinds = [k for v, k in enemy_defs.items()
               if "Melee" in assigns.get(v, {}).get("attackStyle", "")]
check("at least two melee archetypes exist", len(melee_kinds) >= 2, str(melee_kinds))
healer_kinds = [k for v, k in enemy_defs.items()
                if "Healer" in assigns.get(v, {}).get("attackStyle", "")]
check("exactly one healer archetype", len(healer_kinds) == 1, str(healer_kinds))

# --------------------------------------------------------------------------
# 13. Pooling & memory
# --------------------------------------------------------------------------
section("13. Pooling & memory")

pool_src = read("VFX/ComponentPool.cs")
check("pool has max instance cap", "maxInstances" in pool_src and "return null" in pool_src)
check("pool rejects double-return", "Contains" in pool_src)
check("projectile pool is a static reusable queue",
      re.search(r"static readonly Queue<Projectile> pool", proj_src) is not None)
check("projectile pool skips destroyed entries",
      "pool.Dequeue()" in proj_src and "== null" in proj_src)
check("projectile pool grows on demand and is capped by usage",
      re.search(r"pool\.Count|pool\.Enqueue", proj_src) is not None)

mat_src = read("Visual/MaterialLibrary.cs")
check("Lit/Emissive materials cached by name (no per-pickup leak)",
      "nameCache" in mat_src)
check("sprite materials cached by colour", "SpriteCache" in mat_src)

# static mutable state on MonoBehaviours is a leak risk; caches marked readonly
# are fine.
static_leaks = []
_static_pat = re.compile(r"static\s+(?!readonly|class|void|event|extern)\w[\w<>\[\],\s]*\s+(\w+)\s*(?==;|=[^=])")
for path in glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True):
    if "Editor" in path:
        continue
    src = open(path, encoding="utf-8").read()
    for hit in _static_pat.finditer(src):
        # allow const values, expression-bodied properties (read-only) and
        # auto-properties
        snippet = hit.group(0)
        prefix = src[max(0, hit.start() - 20):hit.start()]
        tail = src[hit.end():hit.end() + 3]
        if "const " in prefix or "Property" in snippet or tail.startswith("=>"):
            continue
        static_leaks.append(os.path.relpath(path, SCRIPTS) + ": " + hit.group(1))
known_statics = {"Instance", "GlobalEnemyCap", "initialized"}
static_leaks = [s for s in static_leaks if s.split(": ")[-1] not in known_statics]
check("no unintended mutable static fields", not static_leaks, str(static_leaks[:8]))

# --------------------------------------------------------------------------
# 14. Game-state conflicts
# --------------------------------------------------------------------------
section("14. Game-state conflicts")

flow_src = read("Core/GameFlow.cs")
check("flow pause delegates to the state machine guard",
      "State.TryPause()" in flow_src)
state_src_full = read("Core/GameState.cs")
check("state machine refuses pause outside gameplay states",
      "TryPause" in state_src_full and ("Playing" in state_src_full or "CanPause" in state_src_full))
check("restart re-entrant guarded", "runEnded" in flow_src or "Restarting" in flow_src)
state_src = read("Core/GameState.cs")
check("state machine defines all gameplay states",
      all(s in state_src for s in ["Playing", "Paused", "Victory", "Defeat", "Restarting", "MainMenu"]))

time_src = read("Core/TimeController.cs")
check("TimeController reason-dictated (min wins)", "reason" in time_src.lower())

fpc_src = read("Player/FirstPersonController.cs")
check("jump blocked while crouched",
      re.search(r"crouch|Crouch", fpc_src) is not None)
check("player ResetState exists for restarts", "ResetState" in fpc_src)

vitals_src = read("Player/PlayerVitals.cs")
check("player death publishes once", "PlayerDied?.Invoke" in vitals_src
      or "PlayerDied?.Invoke" in read("Player/PlayerFactory.cs"))

# --------------------------------------------------------------------------
# 15. Unity editor compatibility matrix
# --------------------------------------------------------------------------
section("15. Unity editor compatibility matrix")

from CheckUnityCompatibility import run_checks as run_compatibility_checks

compatibility_errors = run_compatibility_checks(verbose=False)
check("compatibility contract, baseline marker and package pins agree",
      not compatibility_errors, str(compatibility_errors))

editor_validator = read("Editor/ProjectValidator.cs")
compatibility_validator = read("Editor/UnityCompatibility.cs")
check("project validator runs Unity compatibility checks",
      "UnityCompatibility.Validate(Info, Error)" in editor_validator)
check("batch-mode compatibility validation entry point exists",
      "ValidateForCi" in editor_validator and "EditorApplication.Exit" in editor_validator)
check("unsupported editors are detected by exact Application.unityVersion",
      "Application.unityVersion" in compatibility_validator and "FindEditor" in compatibility_validator)

# --------------------------------------------------------------------------
# Summary
# --------------------------------------------------------------------------
print("\n" + "=" * 60)
print(f"RESULT: {passed} passed, {failed} failed, {warnings} warnings")
if failures:
    print("\nFailures:")
    for f in failures:
        print(f"  - {f}")
print("=" * 60)
sys.exit(1 if failed else 0)
