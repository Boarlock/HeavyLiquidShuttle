using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch(typeof(PipelineNet), nameof(PipelineNet.PushCrude))]
    public static class RimefellerPatches
    {
        public static void Prefix(double amount, PipelineNet __instance, out PushCrudeState __state)
        {

            __state = new PushCrudeState();

            foreach (KeyValuePair<HeavyLiquidShuttle, PipelineNet> entry in RimefellerIntegration.AdjacentNetworks)
            {
                if (entry.Value != __instance)
                    continue;

                HeavyLiquidShuttle shuttle = entry.Key;

                TankState? tank = shuttle.GetTankForContent(TankState.StoredType.Oil);

                if (tank == null)
                    return;

                __state.Instance = __instance;
                __state.AmountBefore = amount;
                __state.Tank = tank;
                __state.Shuttle = shuttle;

                return;
            }
        }

        public static void Postfix(PushCrudeState __state, ref double __result)
        {

            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null)
                return;

            if (__result <= 0.0)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0.0)
                return;

            double freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0.0)
                return;

            double accepted = Math.Min(__result, Math.Min(freeCapacity, __state.Tank.ReceiveAllowance));

            if (accepted <= 0.0)
                return;

            __state.Tank.Content = TankState.StoredType.Oil;
            __state.Tank.TankStorage += (float)accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = true;

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }
    }

    public class PushCrudeState
    {
        public PipelineNet? Instance;
        public double AmountBefore;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
    }
}