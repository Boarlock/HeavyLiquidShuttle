using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class PushCrudeState
    {
        public PipelineNet? Instance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
        public RimefellerIntegration? Integration;
    }

    public class RimefellerIntegration
    {
        private static readonly HashSet<RimefellerIntegration> Instances = new HashSet<RimefellerIntegration>();
        private readonly HeavyLiquidShuttle shuttle;
        public RimefellerIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            Instances.Add(this);

            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;
            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;
        }

        static RimefellerIntegration()
        {
            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.rimefeller");

            MethodInfo pushOil = AccessTools.Method(typeof(PipelineNet), nameof(PipelineNet.PushCrude));
            MethodInfo prefix = AccessTools.Method(typeof(RimefellerIntegration), nameof(Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(RimefellerIntegration), nameof(Postfix));

            harmony.Patch(pushOil, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            Log.Message("[HeavyLiquidShuttle] Rimefeller integration loaded.");
        }

        public HashSet<PipelineNet> AdjacentNetworks = new HashSet<PipelineNet>();
        public HashSetQueue<PipelineNet> PendingNetworks = new HashSetQueue<PipelineNet>();

        public static void Prefix(PipelineNet __instance, out PushCrudeState __state)
        {
            __state = new PushCrudeState();

            RimefellerIntegration? integration = null;

            foreach (RimefellerIntegration instance in Instances)
            {
                if (instance.AdjacentNetworks.Contains(__instance))
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

            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.shuttle;
            __state.Integration = integration;
        }

        public static void Postfix(PushCrudeState __state, ref double __result)
        {
            // This PushCrude call was not associated with one of our shuttles.
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
                return;

            // If Rimefeller completely satisfied the request, nothing remains for us.
            if (__result <= 0.0)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                __state.Integration.PendingNetworks.Enqueue(__state.Instance);
                return;
            }

            if (__state.Integration.PendingNetworks.Count > 0)
            {
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingNetworks.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingNetworks.Peek() != __state.Instance)
                    return;

                // This network is now being served.
                __state.Integration.PendingNetworks.Dequeue();
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

        private void OnShuttleTick()
        {
            AdjacentNetworks = ShuttleOilSearch.CheckCellsAroundShuttle(shuttle);

            if (AdjacentNetworks.Count <= 0)
                return;

            if (shuttle.TankA.Content == StoredType.Oil)
            {
                if (shuttle.TankA.Counter < 2)
                    shuttle.TankA.Counter++;

                shuttle.TankA.ReceiveAllowance = 1.0;
            }
            if (shuttle.TankB.Content == StoredType.Oil)
            {
                if (shuttle.TankB.Counter < 2)
                    shuttle.TankB.Counter++;

                shuttle.TankB.ReceiveAllowance = 1.0;
            }
        }

        private void OnTransferTick()
        {
            if (AdjacentNetworks.Count <= 0)
                return;

            PipelineNet? validNet = null;

            foreach (PipelineNet net in AdjacentNetworks)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    if (storage.space >= 1f && !storage.DrainTank)
                    {
                        validNet = net;
                        break;
                    }
                }

                if (validNet != null)
                    break;
            }

            if (validNet == null)
                return;

            TransferTank(shuttle.TankA, validNet);
            TransferTank(shuttle.TankB, validNet);
        }

        private void TransferTank(TankState tank, PipelineNet net)
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
                double remaining = net.PushCrude(amount);
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

        private IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Oil && shuttle.TankA.TankStorage > 0f)
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
                if (shuttle.TankB.Content == StoredType.Oil && shuttle.TankB.TankStorage > 0f)
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

        public void StartOilSpill(float spilledAmount)
        {
            if (!shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(shuttle.OilConnectionAt, current + spilledAmount);
        }
    }
}
