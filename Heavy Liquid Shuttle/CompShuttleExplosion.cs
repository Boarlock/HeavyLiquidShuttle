using RimWorld;
using UnityEngine;
using System;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class CompProperties_ShuttleExplosive : CompProperties
    {
        // Explosion
        public DamageDef? explosiveDamageType;
        public int damageAmountBase = -1;
        public float armorPenetrationBase = -1f;
        public SoundDef? explosionSound;

        public bool doVisualEffects;
        public bool doSoundEffects;
        public float propagationSpeed;

        // Wick
        public float startWickHitPointsPercent;
        public IntRange wickTicks;

        // Pre/post explosion
        public ThingDef? preExplosionSpawnThingDef;
        public float preExplosionSpawnChance;
        public int preExplosionSpawnThingCount;

        public ThingDef? postExplosionSpawnThingDef;
        public float postExplosionSpawnChance;
        public int postExplosionSpawnThingCount;

        public bool applyDamageToExplosionCellsNeighbors;
        public float chanceToStartFire;
        public bool damageFalloff;

        public CompProperties_ShuttleExplosive()
        {
            compClass = typeof(ShuttleExplosion);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            if (explosiveDamageType == null)
                explosiveDamageType = DamageDefOf.Bomb;
        }
    }

    public class ShuttleExplosion : ThingComp
    {
        private int explosionRadius;
        public int ExplosionRadius => explosionRadius;

        public CompProperties_ShuttleExplosive Props => (CompProperties_ShuttleExplosive)props;
        public HeavyLiquidShuttle Shuttle => parent.GetComp<HeavyLiquidShuttle>();

        public void UpdateExplosiveness()
        {
            int tankAExplosionRadius = Mathf.RoundToInt(Shuttle.TankA.content != null ? (Shuttle.TankA.tankStorage / Shuttle.TankA.content.unitsPerExplosionRadius) : 0);
            int tankBExplosionRadius = Mathf.RoundToInt(Shuttle.TankB.content != null ? (Shuttle.TankB.tankStorage / Shuttle.TankB.content.unitsPerExplosionRadius) : 0);

            explosionRadius = tankAExplosionRadius + tankBExplosionRadius;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref explosionRadius, "explosionRadius", 0);
        }
    }
}
