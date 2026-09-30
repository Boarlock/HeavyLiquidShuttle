using RimWorld;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class Detonate
    {
        private readonly HeavyLiquidShuttle shuttle;

        private CompProperties_ShuttleExplosive props;
        private ShuttleExplosion comp;

        private bool wickStarted;
        private int wickTicksLeft;
        private Thing? wickInstigator;

        public Detonate(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            comp = shuttle.parent.TryGetComp<ShuttleExplosion>();
            props = comp.Props;

        }

        public void StartWick(Thing? instigator)
        {
            if (wickStarted)
                return;

            wickStarted = true;
            wickInstigator = instigator;
            wickTicksLeft = props.wickTicks.RandomInRange;

            GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(shuttle.parent, DamageDefOf.Flame, null, instigator);
        }

        public void Tick()
        {
            if (!wickStarted)
                return;

            wickTicksLeft--;

            if (wickTicksLeft <= 0)
            {
                wickStarted = false;
                DetonateNow();
            }
        }

        public void DetonateNow()
        {
                GenExplosion.DoExplosion(
                    shuttle.parent.PositionHeld,
                    shuttle.parent.MapHeld,
                    comp.explosionRadius,
                    props.explosiveDamageType,
                    wickInstigator,
                    props.damageAmountBase,
                    props.armorPenetrationBase,
                    props.explosionSound,
                    postExplosionSpawnThingDef: props.postExplosionSpawnThingDef,
                    postExplosionSpawnChance: props.postExplosionSpawnChance,
                    postExplosionSpawnThingCount: props.postExplosionSpawnThingCount,
                    preExplosionSpawnThingDef: props.preExplosionSpawnThingDef,
                    preExplosionSpawnChance: props.preExplosionSpawnChance,
                    preExplosionSpawnThingCount: props.preExplosionSpawnThingCount,
                    applyDamageToExplosionCellsNeighbors: props.applyDamageToExplosionCellsNeighbors,
                    chanceToStartFire: props.chanceToStartFire,
                    damageFalloff: props.damageFalloff);
            
        }
    }
}
