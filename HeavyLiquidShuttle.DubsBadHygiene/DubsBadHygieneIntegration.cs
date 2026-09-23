using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class DubsBadHygieneIntegration
    {
        public static Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>> AdjacentNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PlumbingNet>>();
        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Application.focusChanged += OnApplicationFocusChanged;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dbh");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene integration loaded.");
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
            HashSet<PlumbingNet> newNets = ShuttleWaterSearch.CheckCellsAroundShuttle(shuttle);

            if (newNets.Count == 0)
            {
                AdjacentNetworks.Remove(shuttle);
                return;
            }

            AdjacentNetworks[shuttle] = newNets;

            if (shuttle.TankA.Content == StoredType.Water)
            {
                if (shuttle.TankA.Counter < 2)
                    shuttle.TankA.Counter++;

                shuttle.TankA.ReceiveAllowance = 1.0;
            }
            if (shuttle.TankB.Content == StoredType.Water)
            {
                if (shuttle.TankB.Counter < 2)
                    shuttle.TankB.Counter++;

                shuttle.TankB.ReceiveAllowance = 1.0;
            }
        }

        private static void OnTransferTick(HeavyLiquidShuttle shuttle)
        {
            if (!AdjacentNetworks.TryGetValue(shuttle, out HashSet<PlumbingNet> nets))
                return;

            PlumbingNet? validNet = null;

            foreach (PlumbingNet net in nets)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
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

        private static void TransferTank(TankState tank, PlumbingNet net)
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
                float remaining = net.PushWater(amount);
                float transferred = amount - remaining;

                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - transferred);
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

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (AdjacentNetworks.ContainsKey(shuttle))
            {
                if (shuttle.TankA.Content == StoredType.Water && shuttle.TankA.TankStorage > 0f)
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
                if (shuttle.TankB.Content == StoredType.Water && shuttle.TankB.TankStorage > 0f)
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
            }
        }
    }
}
