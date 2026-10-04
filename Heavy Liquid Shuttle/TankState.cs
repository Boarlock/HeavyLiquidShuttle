using System;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class TankState : IExposable
    {
        private float storage = 0f;

        public bool isLocked;
        public bool transferEnabled;
        public bool isContaminated = false;

        public float supplyAllowance = 1f;
        public float receiveAllowance = 1f;

        private StoredTypeDef? content;
        public StoredTypeDef? lastNetReceive;
        public StoredTypeDef? lastNetSupply;

        public long lastReceiveCycle = -1;
        public long lastSupplyCycle = -1;

        public WaterState waterQuality = WaterState.Untreated;
        public TransferState tankStatus = TransferState.Holding;

        // Upgrade state
        public int capacityLevel = 0;
        public float Capacity
        {
            get => 1250f + (capacityLevel * 250f);
            
        }

        public float Storage
        {
            get => storage;
            set => storage = Mathf.Max(0f, value);
        }

        public StoredTypeDef? Content
        {
            get => content;
            set
            {
                if (!CachedDefs.IsValid(value))
                    throw new ArgumentException($"Invalid StoredTypeDef assigned to TankState: {value!.defName}");

                content = value;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref storage, "storage", 0f);
            Scribe_Defs.Look(ref content, "Content");

            Scribe_Values.Look(ref isLocked, "isLocked", false);
            Scribe_Values.Look(ref isContaminated, "isContaminated", false);

            Scribe_Values.Look(ref capacityLevel, "capacityLevel", 0);

            Scribe_Values.Look(ref waterQuality, "waterQuality", WaterState.Untreated);
        }

        public enum WaterState
        {
            Treated,
            Untreated,
            Contaminated
        }

        public enum TransferState
        {
            Holding,
            Discharging,
            Receiving
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

    public class StoredTypeDef : Def
    {
        public float unitsPerLiter;
        public float density;

        public float unitsPerExplosionRadius;
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

        public static bool IsValid(StoredTypeDef? def)
        {
            return def == null ||
                   def == Water ||
                   def == Sewage ||
                   def == Oil ||
                   def == Chemfuel ||
                   def == Deepchem ||
                   def == Helixien ||
                   def == Scarlet ||
                   def == Oxygen ||
                   def == Astrofuel;
        }
    }
}
