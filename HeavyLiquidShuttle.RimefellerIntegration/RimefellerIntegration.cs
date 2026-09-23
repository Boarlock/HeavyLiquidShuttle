/*using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class RimefellerIntegration
    {
        private readonly HeavyLiquidShuttle shuttle;
        public RimefellerIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;
            HeavyLiquidShuttle.OilSpillIntegration += StartOilSpill;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.rimefeller");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Rimefeller integration loaded.");
        }

        public HashSet<PipelineNet> AdjacentNetworks = new HashSet<PipelineNet>();

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

        private IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
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
}*/
