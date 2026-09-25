using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class HeavyLiquidShuttleMod : Mod
    {
        public static bool DubsBadHygieneActive { get; private set; }
        public static bool RimefellerActive { get; private set; }
        public static bool VEChemfuelActive { get; private set; }
        public static bool VEHelixienActive { get; private set; }

        public HeavyLiquidShuttleMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("b0arl0ck.heavyliquidshuttle");
            harmony.PatchAll();

            DubsBadHygieneActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.DubsBadHygiene");
            RimefellerActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.Rimefeller");

            if (DubsBadHygieneActive && RimefellerActive)
                LibraryLoaders.SharedDubLoad();
            else if (DubsBadHygieneActive)
                LibraryLoaders.DBHLoad();
            else if (RimefellerActive)
                LibraryLoaders.RFLoad();

            VEChemfuelActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "VanillaExpanded.VChemfuelE");
            VEHelixienActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "VanillaExpanded.HelixienGas");

            if (VEChemfuelActive || VEHelixienActive)
                LibraryLoaders.VELoad();

            Application.focusChanged += HeavyLiquidShuttleManager.OnApplicationFocusChanged;

            HeavyLiquidShuttleManager.IsInitialized = true;

            Log.Message($"[HeavyLiquidShuttle] Initialization completed.");
        }
    }

    public static class HeavyLiquidShuttleManager
    {
        internal static bool IsInitialized = false;

        // Mass conversions for liquids and gas.
        public const float CrudeMassPerLiter = 0.85f;
        public const float DeepchemMassPerLiter = 1.2f;
        public const float HelixienMassPerLiter = 0.2f;

        internal static void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!IsInitialized)
                return;

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
                    shuttle.TankA.Counter2 = 0;

                    shuttle.TankB.TransferEnabled = false;
                    shuttle.TankB.IsTransferringFluid = false;
                    shuttle.TankB.ReceiveAllowance = 1.0;
                    shuttle.TankB.Counter = 0;
                    shuttle.TankA.Counter2 = 0;
                }
            }
        }
    }

    public enum StoredType
    {
        Empty,
        Water,
        Oil,
        Deepchem,
        Helixien
    }

    public class TankState
    {
        public float TankCapacity = 1250f;
        public float TankStorage = 0f;

        public int Counter = 0;
        public int Counter2 = 0;
        public bool IsLocked;
        public bool TransferEnabled;
        public bool IsContaminated = false;
        public bool IsTransferringFluid;
        public double ReceiveAllowance = 1.0;
        public StoredType Content = StoredType.Empty;
    }

    public static class LibraryLoaders
    {
        public static Type? DBHIntegrationType { get; private set; }
        public static Type? RimefellerIntegrationType { get; private set; }
        public static Type? DubwiseSharedIntegrationType { get; private set; }
        public static Type? VESharedIntegrationType { get; private set; }

        public static void DBHLoad()
        {
            string coreAssemblyPath = typeof(LibraryLoaders).Assembly.Location;
            string coreDirectory = Path.GetDirectoryName(coreAssemblyPath);

            string integrationPath = Path.Combine(coreDirectory, "..", "--optional", "DubsBadHygiene", "HeavyLiquidShuttle.DubsBadHygiene.dll");
            integrationPath = Path.GetFullPath(integrationPath);

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene detected, preparing to load.");

            if (!File.Exists(integrationPath))
            {
                Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene integration not found.");
                return;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(integrationPath);
                DBHIntegrationType = assembly.GetType("HeavyLiquidShuttleMod.DubsBadHygieneIntegration");
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Dubs Bad Hygiene integration: " + ex);
            }
        }

        public static void RFLoad()
        {
            string coreAssemblyPath = typeof(LibraryLoaders).Assembly.Location;
            string coreDirectory = Path.GetDirectoryName(coreAssemblyPath);

            string integrationPath = Path.Combine(coreDirectory, "..", "--optional", "Rimefeller", "HeavyLiquidShuttle.Rimefeller.dll");
            integrationPath = Path.GetFullPath(integrationPath);

            Log.Message("[HeavyLiquidShuttle] Rimefeller detected, preparing to load.");

            if (!File.Exists(integrationPath))
            {
                Log.Message("[HeavyLiquidShuttle] Rimefeller integration not found.");
                return;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(integrationPath);
                RimefellerIntegrationType = assembly.GetType("HeavyLiquidShuttleMod.RimefellerIntegration");
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Rimefeller integration: " + ex);
            }
        }

        public static void SharedDubLoad()
        {
            string coreAssemblyPath = typeof(LibraryLoaders).Assembly.Location;
            string coreDirectory = Path.GetDirectoryName(coreAssemblyPath);

            string integrationPath = Path.Combine(coreDirectory, "..", "--optional", "Dubwise", "HeavyLiquidShuttle.SharedDubwiseLibrary.dll");
            integrationPath = Path.GetFullPath(integrationPath);

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene and Rimefeller detected, preparing to load.");

            if (!File.Exists(integrationPath))
            {
                Log.Message("[HeavyLiquidShuttle] Dubwise shared integration not found.");
                return;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(integrationPath);
                DubwiseSharedIntegrationType = assembly.GetType("HeavyLiquidShuttleMod.DubwiseSharedIntegration");
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Dubwise shared integration: " + ex);
            }
        }

        public static void VELoad()
        {
            string coreAssemblyPath = typeof(LibraryLoaders).Assembly.Location;
            string coreDirectory = Path.GetDirectoryName(coreAssemblyPath);

            string integrationPath = Path.Combine(coreDirectory, "..", "--optional", "VE", "HeavyLiquidShuttle.VanillaExpandedIntegration.dll");
            integrationPath = Path.GetFullPath(integrationPath);

            Log.Message("[HeavyLiquidShuttle] Vanilla Chemfuel Expanded or Vanilla Helixien Gas Expanded detected, preparing to load.");

            if (!File.Exists(integrationPath))
            {
                Log.Message("[HeavyLiquidShuttle] VE shared integration not found.");
                return;
            }

            try
            {
                Assembly assembly = Assembly.LoadFrom(integrationPath);
                VESharedIntegrationType = assembly.GetType("HeavyLiquidShuttleMod.VanillaExpandedIntegration");
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load VE shared integration: " + ex);
            }
        }
    }
}
