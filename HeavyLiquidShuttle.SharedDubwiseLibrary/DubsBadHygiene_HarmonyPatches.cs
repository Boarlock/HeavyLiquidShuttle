using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    [HarmonyPatch(typeof(PlumbingNet), nameof(PlumbingNet.PushWater))]
    public static class DubsBadHygiene_HarmonyPatches

    {
        private static readonly Dictionary<HeavyLiquidShuttle, PendingWaterState>  PendingWaterNetworks = new Dictionary<HeavyLiquidShuttle, PendingWaterState>();

        private static void EnqueueNetwork(HeavyLiquidShuttle shuttle, PlumbingNet net)
        {
            if (!PendingWaterNetworks.TryGetValue(shuttle, out PendingWaterState state))
            {
                state = new PendingWaterState();
                PendingWaterNetworks[shuttle] = state;
            }

            if (state.Set.Add(net))
                state.Queue.Enqueue(net);
        }

        public static void Prefix(PlumbingNet __instance, out PushWaterState __state)
        {
            __state = new PushWaterState();

            // See if our shuttle is connected to this Net.
            HeavyLiquidShuttle? shuttle = null;

            foreach (KeyValuePair<HeavyLiquidShuttle, HashSet<PlumbingNet>> entry in DubwiseSharedIntegration.AdjacentWaterNetworks)
            {
                foreach (PlumbingNet net in entry.Value)
                {
                    if (net != __instance)
                        continue;

                    Log.Message($"[HLS] WATER INPUT net={__instance.GetHashCode()}");
                    shuttle = entry.Key;
                    break;
                }
            }

            if (shuttle == null)
                return;

            TankState? tank = shuttle.GetTankForContent(TankState.StoredType.Water);

            if (tank == null)
                return;

            Log.Message(
    $"[HLS] WATER INPUT tank={tank.Content} " +
    $"storage={tank.TankStorage:F2} allowance={tank.ReceiveAllowance:F2}"
);
            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = shuttle;

            foreach (CompWaterStorage waterTower in __instance.WaterTowers)
            {
                __state.WaterStorages[waterTower] = waterTower.WaterStorage;
            }
            

        }
        public static void Postfix(PushWaterState __state, ref float __result)
        {
            // This PushWater call was not associated with one of our shuttles.
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null)
                return;

            // See which DBH towers actually received water from this shuttle.
            foreach (KeyValuePair<CompWaterStorage, float> entry in __state.WaterStorages)
            {
                CompWaterStorage waterTower = entry.Key;
                float before = entry.Value;

                if (waterTower.WaterStorage > before && __state.Tank.IsContaminated && __state.Tank.IsTransferringFluid)
                {
                    waterTower.WaterQuality = ContaminationLevel.Contaminated;
                }
            }

            // If DBH completely satisfied the request, nothing remains for us.
            if (__result <= 0f)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                EnqueueNetwork(__state.Shuttle, __state.Instance);
                return;
            }

            if (PendingWaterNetworks.TryGetValue(__state.Shuttle, out PendingWaterState state) && state.Queue.Count > 0)
            {
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    PlumbingNet staleNetwork = state.Queue.Dequeue();

                    state.Set.Remove(staleNetwork);

                    return;
                }

                // Check current call against next item in the Queue
                if (state.Queue.Peek() != __state.Instance)
                    return;

                // This network is now being served.
                state.Queue.Dequeue();

                state.Set.Remove(__state.Instance);
            }

            //Reset the Queue counter
            __state.Tank.Counter = 0;

            // Safer way to update storage so this method only gives what was taken.
            float freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0f)
                return;

            float accepted = Mathf.Min(__result, (float)__state.Tank.ReceiveAllowance, freeCapacity);
            Log.Message(
    $"[HLS] WATER ACCEPT net={__state.Instance.GetHashCode()} " +
    $"accepted={accepted:F2}"
);

            if (accepted <= 0f)
                return;

            // Update shuttle's mass and water storage.
            __state.Tank.Content = TankState.StoredType.Water;
            __state.Tank.TankStorage += accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = __state.Instance.IsNetContaminated();

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }
    }

    public class PendingWaterState
    {
        public Queue<PlumbingNet> Queue = new Queue<PlumbingNet>();
        public HashSet<PlumbingNet> Set = new HashSet<PlumbingNet>();
    }

    public class PushWaterState
    {
        // Vars to track transfer state across Prefix to Postfix
        public PlumbingNet? Instance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;

        // To track what water storages recieved water from our shuttle if we pushed.
        public Dictionary<CompWaterStorage, float> WaterStorages = new Dictionary<CompWaterStorage, float>();

    }
}
