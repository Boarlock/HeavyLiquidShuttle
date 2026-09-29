using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HeavyLiquidShuttleMod
{
    [DefOf]
    public class CleanShuttleTank
    {
        public static JobDef? CleanTank;
    }

    public class JobDriver_CleanShuttleTank : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            yield return Toils_Reserve.Reserve(TargetIndex.A);

            yield return Toils_Goto.GotoThing(
                TargetIndex.A, 
                PathEndMode.Touch);

            Toil cleaning = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = 300
            };

            cleaning.AddFinishAction(() =>
            {
                HeavyLiquidShuttle shuttle = job.GetTarget(TargetIndex.B).Thing.TryGetComp<HeavyLiquidShuttle>();

                if (shuttle == null)
                    return;

                TankState? tank = null;
                int i = 0;

                foreach (IntVec3 shuttleCell in shuttle.parent.OccupiedRect())
                {
                    if (shuttleCell == job.GetTarget(TargetIndex.A).Cell)
                        break;

                    i++;
                }

                if (i == 3 || i == 4 || i == 13 || i == 14)
                    tank = shuttle.TankA;

                if (i == 1 || i == 5 || i == 12 || i == 16)
                    tank = shuttle.TankB;

                if (tank == null)
                    return;

                tank.IsContaminated = false;
            });

            yield return cleaning;
        }
    }
}
