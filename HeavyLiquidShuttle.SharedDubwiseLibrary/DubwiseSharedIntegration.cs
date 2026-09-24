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
    public class PushPatchstate
    {
        // Vars to track transfer state across Prefix to Postfix
        public PlumbingNet? WaterInstance;
        public PipelineNet? OilInstance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
        public DubwiseSharedIntegration? Integration;

        // To track what water storages recieved water from our shuttle if we pushed.
        public Dictionary<CompWaterStorage, float> WaterStorages = new Dictionary<CompWaterStorage, float>();
    }

    public class DubwiseSharedIntegration
    {
        // List of instances of all DBH Integrations
        private static readonly HashSet<DubwiseSharedIntegration> Instances = new HashSet<DubwiseSharedIntegration>();

        // shuttle specific to this instance of DBH Integration
        private readonly HeavyLiquidShuttle shuttle;

        // Constructor that registers tick events and Gizmo function from CompHeavyLiquidShuttle
        public DubwiseSharedIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            Instances.Add(this);

            HeavyLiquidShuttleGameComponent.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;
            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;
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

        // Cleanup method when the Shuttle is destroyed to let all subscribers of the Tick events to unsubscribe themselves
        private bool cleanedUp;
        private void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComponent.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration -= OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration -= AddGizmos;
            HeavyLiquidShuttle.OilSpillIntegration -= StartOilSpill;

            Instances.Remove(this);

            cleanedUp = true;
        }

        // HashSets for all adjacent network next to the shuttle and HashSetQueues for networks waiting to give content to the Shuttle
        public HashSet<PlumbingNet> AdjacentWaterNetworks = new HashSet<PlumbingNet>();
        public HashSetQueue<PlumbingNet> PendingWaterNetworks = new HashSetQueue<PlumbingNet>();
        public HashSet<PipelineNet> AdjacentOilNetworks = new HashSet<PipelineNet>();
        public HashSetQueue<PipelineNet> PendingOilNetworks = new HashSetQueue<PipelineNet>();

        public static void WaterPrefix(PlumbingNet __instance, out PushPatchstate __state)
        {
            __state = new PushPatchstate();

            DubwiseSharedIntegration? integration = null;

            foreach (DubwiseSharedIntegration instance in Instances)
            {
                if (instance.AdjacentWaterNetworks.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.shuttle.GetTankForContent(StoredType.Water);

            if (tank == null)
                return;

            __state.WaterInstance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.shuttle;
            __state.Integration = integration;

            foreach (CompWaterStorage waterTower in __instance.WaterTowers)
            {
                __state.WaterStorages[waterTower] = waterTower.WaterStorage;
            }
        }

        public static void WaterPostfix(PushPatchstate __state, ref float __result)
        {
            // This PushWater call was not associated with one of our shuttles.
            if (__state.WaterInstance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
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
                __state.Integration.PendingWaterNetworks.Enqueue(__state.WaterInstance);
                return;
            }

            if (__state.Integration.PendingWaterNetworks.Count > 0)
            {

                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingWaterNetworks.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingWaterNetworks.Peek() != __state.WaterInstance)
                    return;

                // This network is now being served.
                __state.Integration.PendingWaterNetworks.Dequeue();
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
            __state.Tank.IsContaminated = __state.WaterInstance.IsNetContaminated();

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }

        public static void OilPrefix(PipelineNet __instance, out PushPatchstate __state)
        {
            __state = new PushPatchstate();

            DubwiseSharedIntegration? integration = null;

            foreach (DubwiseSharedIntegration instance in Instances)
            {
                if (instance.AdjacentOilNetworks.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.shuttle.GetTankForContent(StoredType.Oil);

            if (tank == null)
                return;

            __state.OilInstance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.shuttle;
            __state.Integration = integration;
        }

        public static void OilPostfix(PushPatchstate __state, ref double __result)
        {
            // This PushCrude call was not associated with one of our shuttles.
            if (__state.OilInstance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
                return;

            // If Rimefeller completely satisfied the request, nothing remains for us.
            if (__result <= 0.0)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                __state.Integration.PendingOilNetworks.Enqueue(__state.OilInstance);
                return;
            }

            if (__state.Integration.PendingOilNetworks.Count > 0)
            {
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingOilNetworks.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingOilNetworks.Peek() != __state.OilInstance)
                    return;

                // This network is now being served.
                __state.Integration.PendingOilNetworks.Dequeue();
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

        // Smaller helper so OnShuttleTick isn't repeating this 4x times
        private void PrepareTankForReceiving(TankState tank)
        {
            if (tank.Counter < 2)
                tank.Counter++;

            tank.ReceiveAllowance = 1.0;
        }

        // Prepare the Tanks for another receiving cycle
        private void OnShuttleTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            ShuttleSearch.CheckCellsAroundShuttle(shuttle, out AdjacentWaterNetworks, out AdjacentOilNetworks);

            // Water networks
            if (AdjacentWaterNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Water)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == StoredType.Water)
                    PrepareTankForReceiving(shuttle.TankB);
            }

            // Oil networks
            if (AdjacentOilNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Oil)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == StoredType.Oil)
                    PrepareTankForReceiving(shuttle.TankB);
            }
        }

        // Method to find "Valid Nets", networks that are connected and aren't currently pushing to the Shuttle
        private void OnTransferTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            // Water logic
            if (AdjacentWaterNetworks.Count > 0)
            {
                PlumbingNet? validWaterNet = null;

                foreach (PlumbingNet net in AdjacentWaterNetworks)
                {
                    foreach (CompWaterStorage storage in net.WaterTowers)
                    {
                        if (storage.space >= 1f && !storage.DrainTank)
                        {
                            validWaterNet = net;
                            break;
                        }
                    }

                    if (validWaterNet != null)
                        break;
                }

                if (validWaterNet != null)
                {
                    TransferTank(shuttle.TankA, validWaterNet);
                    TransferTank(shuttle.TankB, validWaterNet);
                }
            }

            // Oil logic
            if (AdjacentOilNetworks.Count > 0)
            {
                PipelineNet? validOilNet = null;

                foreach (PipelineNet net in AdjacentOilNetworks)
                {
                    foreach (CompStorageTank storage in net.OilStorage)
                    {
                        if (storage.space >= 1f && !storage.DrainTank)
                        {
                            validOilNet = net;
                            break;
                        }
                    }

                    if (validOilNet != null)
                        break;
                }

                if (validOilNet != null)
                {
                    TransferTank(shuttle.TankA, validOilNet);
                    TransferTank(shuttle.TankB, validOilNet);
                }
            }
        }

        // Method to actually perform the transfer on water network and validate the transfer request
        private void TransferTank(TankState tank, PlumbingNet waterNet)
        {
            if (tank.Content != StoredType.Water)
                return;

            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            float amount = Mathf.Min(tank.TankStorage, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                float remaining = waterNet.PushWater(amount);
                float transferred = amount - remaining;

                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - transferred);
                MassPatch.NotifyLiquidMassChanged(shuttle);
            }
            finally
            {
                tank.IsTransferringFluid = false;

                if (tank.TankStorage <= 0f)
                {
                    tank.TankStorage = 0f;
                    tank.Content = StoredType.Empty;
                    tank.TransferEnabled = false;
                    tank.IsContaminated = false;
                }
            }
        }

        // Method to actually perform the transfer on oil network and validate the transfer request
        private void TransferTank(TankState tank, PipelineNet oilNet)
        {
            if (tank.Content != StoredType.Oil)
                return;

            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            double amount = Math.Min(tank.TankStorage, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                double remaining = oilNet.PushCrude(amount);
                double transferred = amount - remaining;
                
                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - (float)transferred);
                MassPatch.NotifyLiquidMassChanged(shuttle);
            }
            finally
            {
                tank.IsTransferringFluid = false;

                if (tank.TankStorage <= 0f)
                {
                    tank.TankStorage = 0f;
                    tank.Content = StoredType.Empty;
                    tank.TransferEnabled = false;
                }
            }
        }

        // Gizmos for enabling transfer of water and oil from Shuttle Tanks
        private IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentWaterNetworks.Count > 0 || AdjacentOilNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Water && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank A: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
                        isActive = () =>
                        {
                            return shuttle.TankA.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankA.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankA);
                        }
                    };
                }
                else if (shuttle.TankA.Content == StoredType.Oil && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank A: Discharge into an adjacent crude oil network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadOil"),
                        isActive = () =>
                        {
                            return shuttle.TankA.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankA.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankA);
                        }
                    };
                }

                if (shuttle.TankB.Content == StoredType.Water && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank B: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
                        isActive = () =>
                        {
                            return shuttle.TankB.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankB.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankB);
                        }
                    };
                }
                else if (shuttle.TankB.Content == StoredType.Oil && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank B: Discharge into an adjacent crude oil network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadOil"),
                        isActive = () =>
                        {
                            return shuttle.TankB.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankB.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankB);
                        }
                    };
                }
            }
        }

        // Small helper that starts Rimefeller's oil spill mechanic
        private void StartOilSpill(float spilledAmount)
        {

            if (!shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(shuttle.OilConnectionAt, current + spilledAmount);
        }
    }
}
