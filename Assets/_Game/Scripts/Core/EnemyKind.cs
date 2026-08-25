namespace RogueArena.Core
{
    /// <summary>All enemy archetypes. Shared by waves, factories, score and challenges.</summary>
    public enum EnemyKind
    {
        Grunt,      // the original prototype drone: melee chaser
        Rusher,     // fast, fragile, closes distance quickly
        Soldier,    // balanced ranged fighter
        Assault,    // balanced fighter that uses cover and flanks
        Shotgunner, // aggressive close-range brawler
        Sniper,     // long range, repositions after firing
        Medic,      // heals allies, avoids combat
        Explosive,  // lobs explosive projectiles from medium range
        Heavy,      // slow, tough, powerful weapon
        Tank,       // very tough, very slow, knockback resistant
        Drone,      // flying ranged unit
        Boss,       // boss archetype
    }
}
