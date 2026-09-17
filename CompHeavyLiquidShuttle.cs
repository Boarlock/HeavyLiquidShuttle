using DubsBadHygiene;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttle
{
    public class CompProperties_HLShuttleCarrier : CompProperties
    {
        public CompProperties_HLShuttleCarrier()
        {
            compClass = typeof(HeavyLiquidShuttle);
        }
    }

    public class HeavyLiquidShuttle : ThingComp
    {
        public static List<HeavyLiquidShuttle> ActiveShuttles = new List<HeavyLiquidShuttle>();
        public PlumbingNet? AdjacentPlumbingNet;
        private float waterStorage;
        private float waterCapacity = 2500f; // Liters
        public float waterMass => waterStorage;
        public ContaminationLevel WaterQuality { get; set; } = ContaminationLevel.Treated;

        // Values intentionally mirror DBH's ContaminationLevel.
        public enum ContaminationLevel
        {
            Treated,
            Untreated,
            Contaminated
        }

        public float WaterStorage
        {
            get
            {
                return waterStorage;
            }
            set
            {
                waterStorage = value;
            }
        }
        public float WaterCapacity
        {
            get
            {
                return waterCapacity;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {

            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (HeavyLiquidShuttleMod.DubsBadHygieneActive)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Discharge",
                    defaultDesc = "Unload water into the adjacent DBH plumbing network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
                    Disabled = waterStorage <= 0f || AdjacentPlumbingNet == null,
                    action = () => ShuttleWater.UnloadWater(parent)
                };
            }
            
        }

        public override string CompInspectStringExtra()
        {
            return $"Stored Water: {waterStorage:0.##} / {waterCapacity:0.##}";
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!HeavyLiquidShuttleMod.DubsBadHygieneActive)
                return;

            if (!parent.IsHashIntervalTick(180))
                return;

            PlumbingNet? newNet = ShuttleWater.CheckCellsAroundShuttle(parent);

            if (newNet != AdjacentPlumbingNet)
            {
                if (AdjacentPlumbingNet != null)
                    ActiveShuttles.Remove(this);

                AdjacentPlumbingNet = newNet;

                if (AdjacentPlumbingNet != null && !ActiveShuttles.Contains(this))
                    ActiveShuttles.Add(this);
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref waterStorage, "waterStorage", 0f);
        }
    }
}
