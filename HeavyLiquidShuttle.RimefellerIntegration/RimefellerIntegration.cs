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
        public static Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>> AdjacentNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>>();
        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Application.focusChanged += OnApplicationFocusChanged;
            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.rimefeller");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Rimefeller integration loaded.");
        }

        private static void OnApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                return;

            Log.Message("[HeavyLiquidShuttle] Application lost focus. Halting transfers.");

            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.AllThings)
                {

                    HeavyLiquidShuttle? shuttle = thing.TryGetComp<HeavyLiquidShuttle>();

                    if (shuttle == null)
                        continue;

                    shuttle.TankA.TransferEnabled = false;
                    shuttle.TankA.IsTransferringFluid = false;
                    shuttle.TankA.ReceiveAllowance = 1.0;
                    shuttle.TankA.Counter = 0;

                    shuttle.TankB.TransferEnabled = false;
                    shuttle.TankB.IsTransferringFluid = false;
                    shuttle.TankB.ReceiveAllowance = 1.0;
                    shuttle.TankB.Counter = 0;
                }
            }
        }

        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {
            HashSet<PipelineNet> newNets = ShuttleOilSearch.CheckCellsAroundShuttle(shuttle);

            if (newNets.Count == 0)
            {
                AdjacentNetworks.Remove(shuttle);
                return;
            }

            AdjacentNetworks[shuttle] = newNets;

            if (shuttle.TankA.Content == TankState.StoredType.Oil)
            {
                if (shuttle.TankA.Counter < 2)
                    shuttle.TankA.Counter++;

                shuttle.TankA.ReceiveAllowance = 1.0;
            }
            if (shuttle.TankB.Content == TankState.StoredType.Oil)
            {
                if (shuttle.TankB.Counter < 2)
                    shuttle.TankB.Counter++;

                shuttle.TankB.ReceiveAllowance = 1.0;
            }
        }

        private static void OnTransferTick(HeavyLiquidShuttle shuttle)
        {
            if (!AdjacentNetworks.TryGetValue(shuttle, out HashSet<PipelineNet> nets))
                return;

            PipelineNet? validNet = null;

            foreach (PipelineNet net in nets)
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

        private static void TransferTank(TankState tank, PipelineNet net)
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
