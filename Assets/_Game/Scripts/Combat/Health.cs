using System;
using UnityEngine;
namespace RogueArena.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] float maxHealth=100; float current;
        public float Current=>current; public float Maximum=>maxHealth; public bool IsAlive=>current>0;
        public event Action<float,float> Changed; public event Action<DamageInfo> Died;
        void Awake()=>current=maxHealth;
        public void Configure(float value) { maxHealth=value; current=value; Changed?.Invoke(current,maxHealth); }
        public void TakeDamage(DamageInfo info) { if(!IsAlive)return; current=Mathf.Max(0,current-info.Amount); Changed?.Invoke(current,maxHealth); if(current<=0) Died?.Invoke(info); }
    }
}
