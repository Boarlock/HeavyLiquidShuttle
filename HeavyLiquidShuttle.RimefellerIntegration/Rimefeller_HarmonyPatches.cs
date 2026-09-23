/*using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch(typeof(PipelineNet), nameof(PipelineNet.PushCrude))]
    public static class Rimefeller_HarmonyPatches
    {
        private static readonly Dictionary<HeavyLiquidShuttle, HashSetQueue<PipelineNet>> PendingNetworks = new Dictionary<HeavyLiquidShuttle, HashSetQueue<PipelineNet>>();

        private static void EnqueueNetwork(HeavyLiquidShuttle shuttle, PipelineNet net)
        {
            if (!PendingNetworks.TryGetValue(shuttle, out HashSetQueue<PipelineNet> networks))
            {
                networks = new HashSetQueue<PipelineNet>();
                PendingNetworks[shuttle] = networks;
            }

            networks.Enqueue(net);
        }

        public static void Prefix(PipelineNet __instance, out PushCrudeState __state)
        {
            __state = new PushCrudeState();

            // See if our shuttle is connected to this Net.
            HeavyLiquidShuttle? shuttle = null;

            foreach (KeyValuePair<HeavyLiquidShuttle, HashSet<PipelineNet>> entry in RimefellerIntegration.AdjacentNetworks)
            {
                foreach (PipelineNet net in entry.Value)
                {
                    if (net != __instance)
                        continue;

                    shuttle = entry.Key;
                    break;
                }
            }

            if (shuttle == null)
                return;

            TankState? tank = shuttle.GetTankForContent(StoredType.Oil);

            if (tank == null)
                return;

            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = shuttle;
        }
        public static void Postfix(PushCrudeState __state, ref double __result)
        {
            // This PushCrude call was not associated with one of our shuttles.
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null)
                return;

            // If Rimefeller completely satisfied the request, nothing remains for us.
            if (__result <= 0.0)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                EnqueueNetwork(__state.Shuttle, __state.Instance);
                return;
            }

            if (PendingNetworks.TryGetValue(__state.Shuttle, out HashSetQueue<PipelineNet> networks) && networks.Count > 0)
            {
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    networks.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (networks.Peek() != __state.Instance)
                    return;

                // This network is now being served.
                networks.Dequeue();
            }

            //Reset the Queue counter
            __state.Tank.Counter = 0;

            // Safer way to update storage so this method only gives what was taken.
            double freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0.0)
                return;

            double accepted = Math.Min(__result, Math.Min(freeCapacity, __state.Tank.ReceiveAllowance));

            if (accepted <= 0.0)
                return;

            // Update shuttle's mass and oil storage.
            __state.Tank.Content = StoredType.Oil;
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
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
    }
}*/