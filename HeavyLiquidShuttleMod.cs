using DubsBadHygiene;
using HarmonyLib;
using System;
using Verse;

namespace HeavyLiquidShuttle
{
    public class HeavyLiquidShuttleMod : Mod
    {
        public static bool DubsBadHygieneActive { get; private set; }

        public HeavyLiquidShuttleMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("b0arl0ck.heavyliquidshuttle");

            harmony.PatchAll();

            DubsBadHygieneActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.DubsBadHygiene");

            // DBH compatibility patch.
            if (DubsBadHygieneActive)
            {
                harmony.Patch(AccessTools.Method(typeof(PlumbingNet), nameof(PlumbingNet.PullWater)),
                    prefix: new HarmonyMethod(typeof(PullWaterPatch), nameof(PullWaterPatch.Prefix)));
            }

            Log.Message($"[HeavyLiquidShuttle] Initialization completed.");
        }
    }
}
