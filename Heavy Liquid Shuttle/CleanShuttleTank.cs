using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using static RimWorld.PsychicRitualRoleDef;

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
            IntVec3 relative = target - origin;
            bool isTankA = false;
            bool isTankB = false;

            switch (rotation.AsInt)
            {
                // North
                case 0:
                    if (relative.x == 1 && relative.z == 1) isTankA = true;
                    else if (relative.x == 1 && relative.z == 3) isTankB = true;
                    break;
                // South
                case 2:
                    if (relative.x == 5 && relative.z == 3) isTankA = true;
                    else if (relative.x == 5 && relative.z == 1) isTankB = true;
                    break;
                // East
                case 1:
                    if (relative.x == 2 && relative.z == 3) isTankA = true;
                    else if (relative.x == 2 && relative.z == 1) isTankB = true;
                    break;
                // West
                case 3:
                    if (relative.x == 5 && relative.z == 1) isTankA = true;
                    else if (relative.x == 5 && relative.z == 3) isTankB = true;
                    break;
            }

            if (isTankA)
            {
                if (!jobFailed)
                    shuttle.TankA.IsContaminated = false;

                shuttle.CleaningTankA = false;
                return true;
            }
            else if (isTankB)
            {
                if (!jobFailed)
                    shuttle.TankB.IsContaminated = false;

                shuttle.CleaningTankB = false;
                return true;
            }

            return false;
        }
    }
}
