using HarmonyLib;
using Rimefeller;
using DubsBadHygiene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class DubwiseSharedIntegration
    {
        public static Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>> AdjacentOilNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>>();
        public static Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>> AdjacentWaterNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>>();
        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Application.focusChanged += OnApplicationFocusChanged;
            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dubwiseshared");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Dubwise shared integration loaded.");
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
        private static void PrepareTankForReceiving(TankState tank)
        {
            if (tank.Counter < 2)
                tank.Counter++;

            tank.ReceiveAllowance = 1.0;
        }


        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {

            HashSet<PlumbingNet> waterNets;
            HashSet<PipelineNet> oilNets;

            ShuttleSearch.CheckCellsAroundShuttle(shuttle, out waterNets, out oilNets);

            Log.Message($"[HLS] TICK networks water={waterNets.Count} oil={oilNets.Count}");

            // Water networks
            if (waterNets.Count == 0)
            {
                AdjacentWaterNetworks.Remove(shuttle);
            }
            else
            {
                AdjacentWaterNetworks[shuttle] = waterNets;

                if (shuttle.TankA.Content == TankState.StoredType.Water)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == TankState.StoredType.Water)
                    PrepareTankForReceiving(shuttle.TankB);
            }

            // Oil networks
            if (oilNets.Count == 0)
            {
                AdjacentOilNetworks.Remove(shuttle);
            }
            else
            {
                AdjacentOilNetworks[shuttle] = oilNets;

                if (shuttle.TankA.Content == TankState.StoredType.Oil)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == TankState.StoredType.Oil)
                    PrepareTankForReceiving(shuttle.TankB);
            }
        }

        private static void OnTransferTick(HeavyLiquidShuttle shuttle)
        {
            // Water logic
            if (AdjacentWaterNetworks.TryGetValue(shuttle, out HashSet<PlumbingNet> waterNets))
            {
                PlumbingNet? validWaterNet = null;

                foreach (PlumbingNet net in waterNets)
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
                    Log.Message($"[HLS] WATER SELECT net={validWaterNet.GetHashCode()}");
                    TransferToTank(shuttle.TankA, validWaterNet, null);
                    TransferToTank(shuttle.TankB, validWaterNet, null);
                }
            }

            // Oil logic
            if (AdjacentOilNetworks.TryGetValue(shuttle, out HashSet<PipelineNet> oilNets))
            {
                PipelineNet? validOilNet = null;

                foreach (PipelineNet net in oilNets)
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
                    Log.Message($"[HLS] OIL SELECT net={validOilNet.GetHashCode()}");
                    TransferToTank(shuttle.TankA, null, validOilNet);
                    TransferToTank(shuttle.TankB, null, validOilNet);
                }
            }
        }

        private static void TransferToTank(TankState tank, PlumbingNet? waterNet, PipelineNet? oilNet)
        {
            Log.Message(
    $"[HLS] TRANSFER content={tank.Content} " +
    $"storage={tank.TankStorage:F2} enabled={tank.TransferEnabled}"
);

            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            if (waterNet != null && tank.Content != TankState.StoredType.Water)
                return;

            if (oilNet != null && tank.Content != TankState.StoredType.Oil)
                return;

            double amount = Math.Min(tank.TankStorage, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                double remaining;
                double transferred;

                if (waterNet != null)
                {
                    Log.Message($"[HLS] PUSH WATER amount={amount:F2}");
                    remaining = waterNet.PushWater((float)amount);
                    transferred = amount - remaining;
                }
                else if (oilNet != null)
                {
                    Log.Message($"[HLS] PUSH OIL amount={amount:F2}");
                    remaining = oilNet.PushCrude(amount);
                    transferred = amount - remaining;
                }
                else
                {
                    return;
                }

                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - (float)transferred);

                Log.Message(
    $"[HLS] TRANSFER RESULT remaining={remaining:F2} " +
    $"transferred={transferred:F2} " +
    $"storageAfter={tank.TankStorage:F2}"
);
            }
            finally
            {
                tank.IsTransferringFluid = false;

                if (tank.TankStorage <= 0f)
                {
                    if (tank.Content == TankState.StoredType.Water)
                        tank.IsContaminated = false;

                    tank.TankStorage = 0f;
                    tank.Content = TankState.StoredType.Empty;
                    tank.TransferEnabled = false;
                }
            }
        }

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (AdjacentWaterNetworks.ContainsKey(shuttle) || AdjacentOilNetworks.ContainsKey(shuttle))
            {
                if (shuttle.TankA.Content == TankState.StoredType.Water && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank A: Discharge water into the adjacent DBH plumbing network.",
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
                else if (shuttle.TankA.Content == TankState.StoredType.Oil && shuttle.TankA.TankStorage > 0f)
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

                if (shuttle.TankB.Content == TankState.StoredType.Water && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank B: Discharge water into the adjacent DBH plumbing network.",
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
                else if (shuttle.TankB.Content == TankState.StoredType.Oil && shuttle.TankB.TankStorage > 0f)
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
