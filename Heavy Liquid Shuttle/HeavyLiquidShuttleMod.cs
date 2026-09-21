using HarmonyLib;
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

        public HeavyLiquidShuttleMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("b0arl0ck.heavyliquidshuttle");

            harmony.PatchAll();

            DubsBadHygieneActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.DubsBadHygiene");
            RimefellerActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.Rimefeller");

            if (DubsBadHygieneActive && RimefellerActive)
                DubsLibraryLoaders.SharedDubLoad();
            else if (DubsBadHygieneActive)
                DubsLibraryLoaders.DBHLoad();
            else if (RimefellerActive)
                DubsLibraryLoaders.RFLoad();

            Log.Message($"[HeavyLiquidShuttle] Initialization completed.");
        }
    }

    public static class DubsLibraryLoaders
    {
        public static void DBHLoad()
        {
            string coreAssemblyPath = typeof(DubsLibraryLoaders).Assembly.Location;
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
                Type integrationType = assembly.GetType("HeavyLiquidShuttleMod.DubsBadHygieneIntegration");
                MethodInfo initializeMethod = integrationType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Dubs Bad Hygiene integration: " + ex);
            }
        }

        public static void RFLoad()
        {
            string coreAssemblyPath = typeof(DubsLibraryLoaders).Assembly.Location;
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
                Type integrationType = assembly.GetType("HeavyLiquidShuttleMod.RimefellerIntegration");
                MethodInfo initializeMethod = integrationType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Rimefeller integration: " + ex);
            }
        }

        public static void SharedDubLoad()
        {
            string coreAssemblyPath = typeof(DubsLibraryLoaders).Assembly.Location;
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
                Type integrationType = assembly.GetType("HeavyLiquidShuttleMod.DubwiseSharedIntegration");
                MethodInfo initializeMethod = integrationType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Error("[HeavyLiquidShuttle] Failed to load Dubwise shared integration: " + ex);
            }
        }
    }
}
