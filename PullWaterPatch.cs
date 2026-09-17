using DubsBadHygiene;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttle
{
    public static class PullWaterPatch
    {
        public static bool Prefix(PlumbingNet __instance, 
            float waterUsed, 
            out ContaminationLevel contam, 
            List<CompWaterStorage> __shuffledWaterStorage, 
            List<CompWaterStorage> __WaterTowers,
            ref bool __result)
        {
            contam = ContaminationLevel.Treated;

            // DBH itself treats <= 0 as failure.
            if (waterUsed <= 0f)
            {
                __result = false;
                return false;
            }

            // Go through list of active shuttles to see if any are connected to a DBH net.
            HeavyLiquidShuttle? currentShuttle = null;

            foreach (HeavyLiquidShuttle shuttle in HeavyLiquidShuttle.ActiveShuttles)
            {
                if (shuttle.AdjacentPlumbingNet == __instance)
                {
                    currentShuttle = shuttle;
                }
            }

            if (currentShuttle == null)
                return true;

            // Seperate DBH water from our Shuttle water.
            float dbhWater = __WaterTowers.Sum(x => x.WaterStorage);
            float shuttleWater = currentShuttle.WaterStorage;

            // Let DBH retain its normal behavior.
            if (dbhWater >= waterUsed)
            {
                return true;
            }

            // Even DBH + shuttle can't satisfy the request.
            if (dbhWater + shuttleWater < waterUsed)
            {
                __result = false;
                return false;
            }

            float usageBuffer = waterUsed;

            __shuffledWaterStorage.Clear();
            __shuffledWaterStorage.AddRange(__WaterTowers);
            __shuffledWaterStorage.Shuffle();

            // Consume DBH water first.
            foreach (CompWaterStorage tower in __shuffledWaterStorage)
            {
                if (tower.WaterQuality > contam)
                {
                    contam = tower.WaterQuality;
                }
                float min = Mathf.Min(tower.WaterStorage, usageBuffer);
                tower.WaterStorage -= min;
                if (float.IsNaN(tower.WaterStorage))
                {
                    tower.WaterStorage = 0f;
                    Log.Error("NaN on WaterStorage in PullWater");
                }
                usageBuffer -= min;
                if (usageBuffer <= 0f)
                {
                    break;
                }
            }

            __shuffledWaterStorage.Clear();

            // Whatever DBH couldn't provide comes from the shuttle.
            if (usageBuffer > 0f)
            {
                currentShuttle.WaterStorage -= usageBuffer;
                usageBuffer = 0f;

                if ((ContaminationLevel)currentShuttle.WaterQuality > contam)
                {
                    contam = (ContaminationLevel)currentShuttle.WaterQuality;
                }
            }

            __result = true;
            return false;
        }
    }
}
