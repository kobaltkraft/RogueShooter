using UnityEngine;
namespace RogueArena.Weapons
{
    [CreateAssetMenu(menuName="Rogue Arena/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject { public string displayName="AR-1"; public float damage=24,headshotMultiplier=2,fireRate=9,range=100,spread=.009f,reloadTime=1.5f,recoil=1.1f; public int magazineSize=30,reserveAmmo=120; }
}
