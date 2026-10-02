using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using System;

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
                defaultDuration = 900
            };

            // Provide a progress getter for the progress bar 
            cleaning.WithProgressBar(TargetIndex.A, () => 1f - (float)ticksLeftThisToil / cleaning.defaultDuration);

            this.AddFinishAction(condition =>
            {
                HeavyLiquidShuttle shuttle = TargetB.Thing.TryGetComp<HeavyLiquidShuttle>();

                if (shuttle == null)
                    return;

                Rot4 rotation = shuttle.parent.Rotation;
                IntVec3 target = (IntVec3)TargetA;
                IntVec3 origin = (IntVec3)TargetC;

                bool jobFailed = condition != JobCondition.Succeeded;

                if (!FinishTank(shuttle, target, origin, rotation, jobFailed))
                {
                    shuttle.CleaningTankA = false;
                    shuttle.CleaningTankB = false;
                }
            });

            yield return cleaning;
        }

        private bool FinishTank(
            HeavyLiquidShuttle shuttle, 
            IntVec3 target, 
            IntVec3 origin, 
            Rot4 rotation,
            bool jobFailed = false)
        {
            int relativeX = Math.Abs(target.x - origin.x);
            int relativeZ = Math.Abs(target.z - origin.z);
            bool isTankA = false;
            bool isTankB = false;

            switch (rotation.AsInt)
            {
                // North
                case 0:
                    if (relativeX == 0 && relativeZ == 1) isTankA = true;
                    else if (relativeX == 2 && relativeZ == 1) isTankB = true;
                    break;
                // South
                case 2:
                    if (relativeX == 2 && relativeZ == 4) isTankA = true;
                    else if (relativeX == 0 && relativeZ == 4) isTankB = true;
                    break;
                // East
                case 1:
                    if (relativeX == 1 && relativeZ == 2) isTankA = true;
                    else if (relativeX == 1 && relativeZ == 0) isTankB = true;
                    break;
                // West
                case 3:
                    if (relativeX == 4 && relativeZ == 0) isTankA = true;
                    else if (relativeX == 4 && relativeZ == 2) isTankB = true;
                    break;
            }

            if (isTankA)
            {
                if (!jobFailed)
                    shuttle.TankA.isContaminated = false;

                shuttle.CleaningTankA = false;
                return true;
            }
            else if (isTankB)
            {
                if (!jobFailed)
                    shuttle.TankB.isContaminated = false;

                shuttle.CleaningTankB = false;
                return true;
            }

            return false;
        }
    }
}
