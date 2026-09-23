using HarmonyLib;
using RimWorld;
using Verse;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch(typeof(CompTransporter), "MassUsage", MethodType.Getter)]
    public static class MassPatch
    {
        private static readonly AccessTools.FieldRef<CompTransporter, bool> MassUsageDirty = AccessTools.FieldRefAccess<CompTransporter, bool>("massUsageDirty");

        public static void NotifyLiquidMassChanged(HeavyLiquidShuttle shuttle)
        {
            CompTransporter transporter = shuttle.parent.TryGetComp<CompTransporter>();

            if (transporter != null)
                MassUsageDirty(transporter) = true;
        }

        public static void Postfix(CompTransporter __instance, ref float __result)
        {
            HeavyLiquidShuttle shuttle = __instance.parent.TryGetComp<HeavyLiquidShuttle>();

            if (shuttle != null)
            {
                __result += shuttle.CalculateMassFromTanks();
            }
        }
    } 
}
