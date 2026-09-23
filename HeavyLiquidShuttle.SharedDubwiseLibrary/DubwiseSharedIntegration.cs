/*using HarmonyLib;
using Rimefeller;
using DubsBadHygiene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class DubwiseSharedIntegration
    {
        private readonly HeavyLiquidShuttle shuttle;
        public DubwiseSharedIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;
        }

        public void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dubwiseshared");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Dubwise shared integration loaded.");
        }

        public static Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>> AdjacentOilNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PipelineNet>>();
        public static Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>> AdjacentWaterNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>>();
        
        private static void PrepareTankForReceiving(TankState tank)
        {
            if (tank.Counter < 2)
                tank.Counter++;

            tank.ReceiveAllowance = 1.0;
        }


        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {
            ShuttleSearch.CheckCellsAroundShuttle(shuttle, out HashSet<PlumbingNet> waterNets, out HashSet<PipelineNet> oilNets);

            // Water networks
            if (waterNets.Count == 0)
            {
                AdjacentWaterNetworks.Remove(shuttle);
            }
            else
            {
                AdjacentWaterNetworks[shuttle] = waterNets;

                if (shuttle.TankA.Content == StoredType.Water)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == StoredType.Water)
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

                if (shuttle.TankA.Content == StoredType.Oil)
                    PrepareTankForReceiving(shuttle.TankA);

                if (shuttle.TankB.Content == StoredType.Oil)
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
                    TransferToTank(shuttle, validWaterNet);
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
                    TransferToTank(shuttle, validOilNet);
                }
            }
        }

        private static void TransferToTank(HeavyLiquidShuttle shuttle, PlumbingNet waterNet)
        {
            TransferTank(shuttle, shuttle.TankA, waterNet);
            TransferTank(shuttle, shuttle.TankB, waterNet);
        }

        private static void TransferToTank(HeavyLiquidShuttle shuttle, PipelineNet oilNet)
        {
            TransferTank(shuttle, shuttle.TankA, oilNet);
            TransferTank(shuttle, shuttle.TankB, oilNet);
        }

        private static void TransferTank(HeavyLiquidShuttle shuttle, TankState tank, PlumbingNet waterNet)
        {
            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            if (tank.Content != StoredType.Water)
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
                    tank.IsContaminated = false;
                    tank.TankStorage = 0f;
                    tank.Content = StoredType.Empty;
                    tank.TransferEnabled = false;
                }
            }
        }

        private static void TransferTank(HeavyLiquidShuttle shuttle, TankState tank, PipelineNet oilNet)
        {
            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            if (tank.Content != StoredType.Oil)
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

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (AdjacentWaterNetworks.ContainsKey(shuttle) || AdjacentOilNetworks.ContainsKey(shuttle))
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

        public static void StartOilSpill(HeavyLiquidShuttle shuttle, float spilledAmount)
        {

            if (!shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(shuttle.OilConnectionAt, current + spilledAmount);
        }
    }
}*/
