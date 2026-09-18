using DubsBadHygiene;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch(typeof(PlumbingNet), nameof(PlumbingNet.PushWater))]
    public static class DubsPatches
    {
        public static void Postfix(float waterBufferToDistribute, PlumbingNet __instance, ref float __result)
        {
            if (__result <= 0f)
                return;

            HeavyLiquidShuttle? shuttle = null;

            foreach (KeyValuePair<HeavyLiquidShuttle, PlumbingNet> entry in DubsBadHygieneIntegration.AdjacentNetworks)
            {
                if (entry.Value != __instance)
                    continue;

                if (entry.Key.WaterStorage >= entry.Key.WaterCapacity)
                    continue;

                shuttle = entry.Key;
                break;
            }

            if (shuttle == null)
                return;

            if (shuttle.IsDispensingFluid || shuttle.IsReceivingFluid)
                return;

            shuttle.BeginReceivingFluid();

            try
            {
                // Safer way to update storage so this method only gives what was taken.
                float freeCapacity = shuttle.WaterCapacity - shuttle.WaterStorage;
                float accepted = Mathf.Min(__result, freeCapacity);

                // Update shuttle's mass and water storage.
                shuttle.WaterStorage += accepted;
                MassPatch.NotifyLiquidMassChanged(shuttle);

                __result -= accepted;
            }
            finally
            {
                shuttle.EndReceivingFluid();
            }
        }
    }

    [HarmonyPatch]
    public static class DrainWaterJobPatch
    {
        public static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("DubsBadHygiene.JobDriver_DrainWater");

            return AccessTools.Method(type, "<MakeNewToils>b__1_0");
        }

        public static bool Prefix(JobDriver __instance)
        {
            Thing target = __instance.job.targetA.Thing;

            HeavyLiquidShuttle shuttle = __instance.job.targetA.Thing.TryGetComp<HeavyLiquidShuttle>();

            if (shuttle == null)
                return true;

            target.Map.designationManager.DesignationOn(__instance.job.targetA.Thing, DubDef.drainOutDes)?.Delete();

            shuttle.WaterStorage = 0f;
            MassPatch.NotifyLiquidMassChanged(shuttle);

            return false;
        }
    }
}
