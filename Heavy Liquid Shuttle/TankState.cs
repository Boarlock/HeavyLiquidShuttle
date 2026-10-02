using Verse;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class TankState
    {
        // Keeping this in the constructor to be able to change these later with different research projects.
        public TankState()
        {
            props = new Storage();
        }

        public Storage props;
        public float tankStorage = 0f;

        public bool isLocked;
        public bool transferEnabled;
        public bool isContaminated = false;

        public float supplyAllowance = 1f;
        public float receiveAllowance = 1f;

        public StoredTypeDef? content;

        public WaterState waterQuality = WaterState.Untreated;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tankStorage, "tankStorage", 0f);
            Scribe_Defs.Look(ref content, "content");

            Scribe_Values.Look(ref isLocked, "isLocked", false);
            Scribe_Values.Look(ref isContaminated, "isContaminated", false);

            Scribe_Values.Look(ref props.physicalCapacity, "physicalCapacity", 1250f);

            Scribe_Values.Look(ref waterQuality, "waterQuality", WaterState.Untreated);
        }

        public class Storage
        {
            public float physicalCapacity = 1250f;
        }

        public class StoredTypeDef : Def
        {
            public float unitsPerLiter;
            public float density;

            public float unitsPerExplosionRadius;
        }

        public enum WaterState
        {
            Treated,
            Untreated,
            Contaminated
        }

        //Helpers to convert units to liters and vice versa
        public static float UnitsToLiters(float units, StoredTypeDef def)
        {
            if (def.unitsPerLiter <= 0f)
                return 0f;
            return units / def.unitsPerLiter;
        }

        public static float LitersToUnits(float liters, StoredTypeDef def)
        {
            return liters * def.unitsPerLiter;
        }
    }

    public static class CachedDefs
    {
        public static readonly StoredTypeDef Water = DefDatabase<StoredTypeDef>.GetNamed("Water");
        public static readonly StoredTypeDef Sewage = DefDatabase<StoredTypeDef>.GetNamed("Sewage");

        public static readonly StoredTypeDef Oil = DefDatabase<StoredTypeDef>.GetNamed("Oil");
        public static readonly StoredTypeDef Chemfuel = DefDatabase<StoredTypeDef>.GetNamed("Chemfuel");

        public static readonly StoredTypeDef Deepchem = DefDatabase<StoredTypeDef>.GetNamed("Deepchem");
        public static readonly StoredTypeDef Helixien = DefDatabase<StoredTypeDef>.GetNamed("Helixien");
        public static readonly StoredTypeDef Scarlet = DefDatabase<StoredTypeDef>.GetNamed("Scarlet");
        public static readonly StoredTypeDef Oxygen = DefDatabase<StoredTypeDef>.GetNamed("Oxygen");
        public static readonly StoredTypeDef Astrofuel = DefDatabase<StoredTypeDef>.GetNamed("Astrofuel");
    }
}
