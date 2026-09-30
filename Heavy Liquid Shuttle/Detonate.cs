using RimWorld;
using Verse;
using Verse.Sound;

namespace HeavyLiquidShuttleMod
{
    public class Detonate
    {
        private readonly HeavyLiquidShuttle shuttle;

        private CompProperties_ShuttleExplosive Props;
        private ShuttleExplosion Comp;

        private bool wickStarted;
        private int wickTicksLeft;
        private Thing? wickInstigator;
        private Sustainer? wickSoundSustainer;

        public Detonate(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            Comp = shuttle.parent.TryGetComp<ShuttleExplosion>();
            Props = Comp.Props;

        }

        public void StartWick(Thing? instigator)
        {
            if (wickStarted)
                return;

            wickStarted = true;
            wickInstigator = instigator;
            wickTicksLeft = Props.wickTicks.RandomInRange;

            SoundDefOf.MetalHitImportant.PlayOneShot(new TargetInfo(shuttle.parent.Position, shuttle.parent.Map));
            SoundInfo info = SoundInfo.InMap(shuttle.parent, MaintenanceType.PerTick);
            wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);

            GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(shuttle.parent, DamageDefOf.Flame, null, instigator);
        }

        public void Tick()
        {
            if (!wickStarted)
                return;

            if (wickSoundSustainer == null)
            {
                SoundInfo info = SoundInfo.InMap(shuttle.parent, MaintenanceType.PerTick);

                wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
            }
            else
            {
                wickSoundSustainer.Maintain();
            }

            wickTicksLeft--;

            if (wickTicksLeft <= 0)
                DetonateNow();
        }

        public void DetonateNow()
        {
            wickStarted = false;

            if (wickSoundSustainer != null)
            {
                wickSoundSustainer.End();
                wickSoundSustainer = null;
            }

            GenExplosion.DoExplosion(
                    shuttle.parent.PositionHeld,
                    shuttle.parent.MapHeld,
                    Comp.explosionRadius,
                    Props.explosiveDamageType,
                    wickInstigator,
                    Props.damageAmountBase,
                    Props.armorPenetrationBase,
                    Props.explosionSound,
                    postExplosionSpawnThingDef: Props.postExplosionSpawnThingDef,
                    postExplosionSpawnChance: Props.postExplosionSpawnChance,
                    postExplosionSpawnThingCount: Props.postExplosionSpawnThingCount,
                    preExplosionSpawnThingDef: Props.preExplosionSpawnThingDef,
                    preExplosionSpawnChance: Props.preExplosionSpawnChance,
                    preExplosionSpawnThingCount: Props.preExplosionSpawnThingCount,
                    applyDamageToExplosionCellsNeighbors: Props.applyDamageToExplosionCellsNeighbors,
                    chanceToStartFire: Props.chanceToStartFire,
                    damageFalloff: Props.damageFalloff);
            
        }
    }
}
