using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class RimefellerIntegration
    {
        public static readonly Dictionary<HeavyLiquidShuttle, PipelineNet> AdjacentNetworks = new Dictionary<HeavyLiquidShuttle, PipelineNet>();
        public static PipelineNet? shuttleCurrentlyPushingTo;
        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;

            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.rimefeller");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Rimefeller integration loaded.");
        }

        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {

            PipelineNet? newNet = ShuttleOilSearch.CheckCellsAroundShuttle(shuttle);

            if (newNet == null)
            {
                AdjacentNetworks.Remove(shuttle);
                return;
            }

            AdjacentNetworks[shuttle] = newNet;

            if (shuttle.TankA.Content == TankState.StoredType.Oil)
                shuttle.TankA.ReceiveAllowance = 1.0;

            if (shuttle.TankB.Content == TankState.StoredType.Oil)
                shuttle.TankB.ReceiveAllowance = 1.0;
        }

        private static void OnTransferTick(HeavyLiquidShuttle shuttle)
        {
            if (!AdjacentNetworks.TryGetValue(shuttle, out PipelineNet net))
                return;

            TransferTank(shuttle, shuttle.TankA, net);
            TransferTank(shuttle, shuttle.TankB, net);
        }

        private static void TransferTank(HeavyLiquidShuttle shuttle, TankState tank, PipelineNet net)
        {
            if (tank.Content != TankState.StoredType.Oil)
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
            }
            finally
            {
                tank.IsTransferringFluid = false;

                if (tank.TankStorage <= 0f)
                {
                    tank.TankStorage = 0f;
                    tank.Content = TankState.StoredType.Empty;
                    tank.TransferEnabled = false;
                    tank.IsContaminated = false;
                }
            }
        }

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (AdjacentNetworks.ContainsKey(shuttle))
            {
                if (shuttle.TankA.Content == TankState.StoredType.Oil && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank A: Discharge crude oil into the adjacent Rimefeller plumbing network.",
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
                if (shuttle.TankB.Content == TankState.StoredType.Oil && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank B: Discharge crude oil into the adjacent Rimefeller plumbing network.",
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

        public static void StartOilSpill(HeavyLiquidShuttle shuttle, float spilledAmount)
        {

            if (!shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(shuttle.OilConnectionAt, current + spilledAmount);
        }
    }
}
