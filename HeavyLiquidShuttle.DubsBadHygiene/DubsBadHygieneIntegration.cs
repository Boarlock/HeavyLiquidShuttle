using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class DubsBadHygieneIntegration
    {
        public static readonly Dictionary<HeavyLiquidShuttle, PlumbingNet> AdjacentNetworks = new Dictionary<HeavyLiquidShuttle, PlumbingNet>();
        public static PlumbingNet? shuttleCurrentlyPushingTo;
        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnDrainTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dbh");
            harmony.PatchAll();

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene integration loaded.");
        }

        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {
            PlumbingNet? newNet = ShuttleWater.CheckCellsAroundShuttle(shuttle);

            if (newNet == null)
            {
                AdjacentNetworks.Remove(shuttle);
                return;
            }

            AdjacentNetworks[shuttle] = newNet;
        }

        private static void OnDrainTick(HeavyLiquidShuttle shuttle)
        {
            if (!shuttle.IsDraining)
                return;

            if (shuttle.IsReceivingFluid || shuttle.IsDispensingFluid)
                return;

            if (!DubsBadHygieneIntegration.AdjacentNetworks.TryGetValue(shuttle, out PlumbingNet net))
                return;

            float amount = Mathf.Min(shuttle.WaterStorage, 1f);

            if (amount <= 0f)
                return;

            shuttle.BeginDispensingFluid();

            try
            {
                float remaining = net.PushWater(amount);

                float transferred = amount - remaining;
                shuttle.WaterStorage -= transferred;
            }
            finally
            {
                shuttle.EndDispensingFluid();
            }
        }

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            yield return new Command_Action
            {
                defaultLabel = "Discharge",
                defaultDesc = "Unload water into the adjacent DBH plumbing network.",
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
                Disabled = shuttle.WaterStorage <= 0f || !AdjacentNetworks.ContainsKey(shuttle),
                action = () => ShuttleWater.UnloadWater(shuttle.parent)
            };
            yield return new Command_Action
            {
                defaultLabel = "DrainTank".Translate(),
                defaultDesc = "DrainTankDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("DBH/UI/drainOut"),
                Disabled = shuttle.WaterStorage <= 0f,
                action = () => DrainDesignation(shuttle)

            };
            yield return new Command_Toggle
            {
                defaultLabel = "TransferStorageTank".Translate(),
                defaultDesc = "TransferStorageTankDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("DBH/UI/Valve"),
                Disabled = shuttle.WaterStorage <= 0f || !AdjacentNetworks.ContainsKey(shuttle),
                isActive = () => shuttle.IsDraining,
                toggleAction = shuttle.DrainToggle
                
            };
        }

        public static void DrainDesignation(HeavyLiquidShuttle shuttle)
        {
            DesignationManager designationManager = shuttle.parent.Map.designationManager;

            Designation existing = designationManager.DesignationOn(shuttle.parent, DubDef.drainOutDes);

            if (existing != null)
            {
                existing.Delete();
            }
            else
            {
                designationManager.AddDesignation(new Designation(shuttle.parent, DubDef.drainOutDes));
            }
        }
    }
}
