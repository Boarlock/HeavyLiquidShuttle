using DubsBadHygiene;
using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class PushPatchstate : StateDouble<PlumbingNet, PipelineNet>
    {
        public DubwiseSharedIntegration? Integration;

        // To track what water storages recieved water from our shuttle if we pushed.
        public Dictionary<CompWaterStorage, float> WaterStorages = new Dictionary<CompWaterStorage, float>();
    }

    public class DubwiseSharedIntegration : LiquidIntegrationDouble<PlumbingNet, PipelineNet>
    {
        // List of instances of all DBH Integrations
        private static readonly HashSet<DubwiseSharedIntegration> Instances = new HashSet<DubwiseSharedIntegration>();

        // Constructor that registers tick events and Gizmo function from CompHeavyLiquidShuttle
        public DubwiseSharedIntegration(HeavyLiquidShuttle shuttle) : base(shuttle)
        {
            Instances.Add(this);
            shuttle.OilSpillIntegration += StartOilSpill;
        }

        protected override void OnCleanup()
        {
            Instances.Remove(this);
            Shuttle.OilSpillIntegration -= StartOilSpill;
        }

        // Static constructor for all DBH instacnes to patch the relevant DBH, and Rimefeller method
        static DubwiseSharedIntegration()
        {
            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dubwiseshared");

            // Method target for PushWater and PushCrude
            MethodInfo pushWater = AccessTools.Method(typeof(PlumbingNet), nameof(PlumbingNet.PushWater));
            MethodInfo pushOil = AccessTools.Method(typeof(PipelineNet), nameof(PipelineNet.PushCrude));

            // Seperate Prefix and Postfix identifiers for both PushWater and PushCrude
            MethodInfo waterPrefix = AccessTools.Method(typeof(DubwiseSharedIntegration), nameof(WaterPrefix));
            MethodInfo waterPostfix = AccessTools.Method(typeof(DubwiseSharedIntegration), nameof(WaterPostfix));
            MethodInfo oilPrefix = AccessTools.Method(typeof(DubwiseSharedIntegration), nameof(OilPrefix));
            MethodInfo oilPostfix = AccessTools.Method(typeof(DubwiseSharedIntegration), nameof(OilPostfix));

            harmony.Patch(pushWater, prefix: new HarmonyMethod(waterPrefix), postfix: new HarmonyMethod(waterPostfix));
            harmony.Patch(pushOil, prefix: new HarmonyMethod(oilPrefix), postfix: new HarmonyMethod(oilPostfix));

            Log.Message("[HeavyLiquidShuttle] Dubwise shared integration loaded.");
        }

        protected override StoredType LiquidType => StoredType.Empty;
        protected override void FindAdjacentNetworks()
        {
            ShuttleSearch.CheckCellsAroundShuttle(Shuttle, out AdjacentXNets, out AdjacentYNets);
        }

        protected override void FindValidNet(out PlumbingNet? validWaterNet, TankState tank)
        {
            bool alreadySupplied = false;
            validWaterNet = null;

            foreach (PlumbingNet net in AdjacentXNets)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                    {
                        PendingXNetsSupply.Enqueue(net);

                        if (SupplyNetCounter >= 2)
                        {
                            // Network in Queue has become stale.
                            PendingXNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                        else if (PendingXNetsSupply.Count > 0 && PendingXNetsSupply.Peek() == net && !alreadySupplied)
                        {
                            alreadySupplied = true;
                            PendingXNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                    }
                }
            }
        }

        protected override void FindValidNet(out PipelineNet? validOilNet, TankState tank)
        {
            bool alreadySupplied = false;
            validOilNet = null;

            foreach (PipelineNet net in AdjacentYNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                    {
                        PendingYNetsSupply.Enqueue(net);

                        if (SupplyNetCounter >= 2)
                        {
                            // Network in Queue has become stale.
                            PendingYNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                        else if (PendingYNetsSupply.Count > 0 && PendingYNetsSupply.Peek() == net && !alreadySupplied)
                        {
                            alreadySupplied = true;
                            PendingYNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                    }
                }
            }
        }

        protected override float TryPush(PlumbingNet net, float amount)
        {
            return net.PushWater(amount);
        }

        protected override float TryPush(PipelineNet net, float amount)
        {
            return (float)net.PushCrude(amount);
        }

        private void StartOilSpill(float spilledAmount)
        {

            if (!Shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = Shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(Shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(Shuttle.OilConnectionAt, current + spilledAmount);
        }

        public static void WaterPrefix(PlumbingNet __instance, out PushPatchstate __state)
        {
            __state = new PushPatchstate();

            DubwiseSharedIntegration? integration = null;

            foreach (DubwiseSharedIntegration instance in Instances)
            {
                if (instance.AdjacentXNets.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.Shuttle.GetTankForReceive(StoredType.Water);

            if (tank == null || tank.IsLocked)
                return;

            __state.XInstance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.Shuttle;
            __state.Integration = integration;

            foreach (CompWaterStorage waterTower in __instance.WaterTowers)
            {
                __state.WaterStorages[waterTower] = waterTower.WaterStorage;
            }
        }

        public static void WaterPostfix(PushPatchstate __state, ref float __result)
        {
            // This PushWater call was not associated with one of our shuttles.
            if (__state.XInstance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
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
                __state.Integration.PendingXNetsReceive.Enqueue(__state.XInstance);
                return;
            }

            if (__state.Integration.PendingXNetsReceive.Count > 0)
            {

                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingXNetsReceive.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingXNetsReceive.Peek() != __state.XInstance)
                    return;

                // This network is now being served.
                __state.Integration.PendingXNetsReceive.Dequeue();
            }

            //Reset the Queue counter
            __state.Tank.Counter = 0;

            // Safer way to update storage so this method only gives what was taken.
            float freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0f)
                return;

            float accepted = Mathf.Min(__result, (float)__state.Tank.ReceiveAllowance, freeCapacity);

            if (accepted <= 0f)
                return;

            // Update shuttle's mass and water storage.
            __state.Tank.Content = StoredType.Water;
            __state.Tank.TankStorage += accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = __state.XInstance.IsNetContaminated();

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }

        public static void OilPrefix(PipelineNet __instance, out PushPatchstate __state)
        {
            __state = new PushPatchstate();

            DubwiseSharedIntegration? integration = null;

            foreach (DubwiseSharedIntegration instance in Instances)
            {
                if (instance.AdjacentYNets.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.Shuttle.GetTankForReceive(StoredType.Oil);

            if (tank == null || tank.IsLocked)
                return;

            __state.YInstance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.Shuttle;
            __state.Integration = integration;
        }

        public static void OilPostfix(PushPatchstate __state, ref double __result)
        {
            // This PushCrude call was not associated with one of our shuttles.
            if (__state.YInstance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
                return;

            // If Rimefeller completely satisfied the request, nothing remains for us.
            if (__result <= 0.0)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                __state.Integration.PendingYNetsReceive.Enqueue(__state.YInstance);
                return;
            }

            if (__state.Integration.PendingYNetsReceive.Count > 0)
            {
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingYNetsReceive.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingYNetsReceive.Peek() != __state.YInstance)
                    return;

                // This network is now being served.
                __state.Integration.PendingYNetsReceive.Dequeue();
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
}
