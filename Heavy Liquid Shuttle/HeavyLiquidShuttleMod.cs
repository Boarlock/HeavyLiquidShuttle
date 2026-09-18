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

        public HeavyLiquidShuttleMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("b0arl0ck.heavyliquidshuttle");

            harmony.PatchAll();

            DubsBadHygieneActive = LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == "Dubwise.DubsBadHygiene");

            if (DubsBadHygieneActive)
                DubsBadHygieneLoader.Load();

            Log.Message($"[HeavyLiquidShuttle] Initialization completed.");
        }
    }

    public static class DubsBadHygieneLoader
    {
        public static void Load()
        {
            string coreAssemblyPath = typeof(DubsBadHygieneLoader).Assembly.Location;
            string coreDirectory = Path.GetDirectoryName(coreAssemblyPath);

            string integrationPath = Path.Combine(coreDirectory, "..", "--optional", "DubsBadHygiene", "HeavyLiquidShuttle.DubsBadHygiene.dll");

            integrationPath = Path.GetFullPath(integrationPath);

            Log.Message("[HeavyLiquidShuttle] Looking for DBH integration at: " + integrationPath);

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
    }
}
