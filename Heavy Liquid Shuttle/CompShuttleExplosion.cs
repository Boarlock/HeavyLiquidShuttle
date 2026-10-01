using RimWorld;
using System;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class CompProperties_ShuttleExplosive : CompProperties
    {
        // Explosion
        public DamageDef explosiveDamageType;
        public int damageAmountBase = -1;
        public float armorPenetrationBase = -1f;
        public SoundDef explosionSound;

        public bool doVisualEffects = true;
        public bool doSoundEffects = true;
        public float propagationSpeed = 1f;

        // Wick
        public float startWickHitPointsPercent = 0.2f;
        public IntRange wickTicks = new IntRange(140, 150);

        // Pre/post explosion
        public ThingDef preExplosionSpawnThingDef;
        public float preExplosionSpawnChance;
        public int preExplosionSpawnThingCount = 1;

        public ThingDef postExplosionSpawnThingDef;
        public float postExplosionSpawnChance;
        public int postExplosionSpawnThingCount = 1;

        public bool applyDamageToExplosionCellsNeighbors;
        public float chanceToStartFire;
        public bool damageFalloff;

        // Helixien explosiveness tiers
        public int lowExplosiveRadius = 4;
        public int moderateExplosiveRadius = 7;
        public int highExplosiveRadius = 10;

        public int lowDestroyThingOnExplosionSize;
        public int moderateDestroyThingOnExplosionSize;
        public int highDestroyThingOnExplosionSize;

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
        public TankState.HelixienState explosiveness;
        public int explosionRadius;
        public int destroyThingOnExplosionSize;

        public CompProperties_ShuttleExplosive Props => (CompProperties_ShuttleExplosive)props;
        public HeavyLiquidShuttle Shuttle => parent.GetComp<HeavyLiquidShuttle>();

        public void UpdateExplosiveness()
        {
            int totalExplosiveness = Shuttle.TankA.GetExplosiveness() + Shuttle.TankB.GetExplosiveness();

            if (totalExplosiveness <= 0)
            {
                explosiveness = TankState.HelixienState.None;
                destroyThingOnExplosionSize = 0;
                explosionRadius = 0;
                return;
            }

            if (totalExplosiveness >= 5)
            {
                explosiveness = TankState.HelixienState.High;
                destroyThingOnExplosionSize = Props.highDestroyThingOnExplosionSize;
                explosionRadius = Props.highExplosiveRadius;
            }
            else if (totalExplosiveness >= 3)
            {
                explosiveness = TankState.HelixienState.Moderate;
                destroyThingOnExplosionSize = Props.moderateDestroyThingOnExplosionSize;
                explosionRadius = Props.moderateExplosiveRadius;
            }
            else if (totalExplosiveness >= 1)
            {
                explosiveness = TankState.HelixienState.Low;
                destroyThingOnExplosionSize = Props.lowDestroyThingOnExplosionSize;
                explosionRadius = Props.lowExplosiveRadius;
            }
        }
    }
}
