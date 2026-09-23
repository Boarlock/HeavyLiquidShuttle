using HarmonyLib;
using PipeSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch]
    public static class DrawAmongStorageTestPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(PipeNet),
                nameof(PipeNet.DrawAmongStorage),
                new[]
                {
                    typeof(float),
                    typeof(float).MakeByRefType(),
                    typeof(List<CompResourceStorage>),
                    typeof(bool)
                });
        }

        public static void Postfix(
            float amount,
            ref float drawn,
            List<CompResourceStorage> storages)
        {
            Log.Message(
                $"[HeavyLiquidShuttle] DrawAmongStorage returned. " +
                $"Amount={amount}, Drawn={drawn}, " +
                $"Storages={(storages == null ? "NULL" : storages.Count.ToString())}");

            if (storages == null)
                return;

            foreach (CompResourceStorage storage in storages)
            {
                Log.Message(
                    $"[HeavyLiquidShuttle] Storage={storage}, " +
                    $"CanAccept={storage.AmountCanAccept}, " +
                    $"Stored={storage.AmountStored}");
            }
        }
    }
}