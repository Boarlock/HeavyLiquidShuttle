using HarmonyLib;
using System;
using Verse;

namespace CargoShuttle
{
    public class CargoShuttleMod : Mod
    {
        public CargoShuttleMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("b0arl0ck.cargoshuttle");

            harmony.PatchAll();

            Log.Message($"[CargoShuttle] Initialization completed.");
        }
    }
}
