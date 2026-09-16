using DubsBadHygiene;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace CargoShuttle
{
    public class CompProperties_CargoShuttle : CompProperties
    {
        public CompProperties_CargoShuttle()
        {
            compClass = typeof(CompCargoShuttle);
        }
    }

    public class CompCargoShuttle : ThingComp
    {
        public PlumbingNet? AdjacentPlumbingNet;
        private float waterStorage = 500f;
        private float waterCapacity = 500f;

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
            Log.Message("CARGO SHUTTLE CompGetGizmosExtra CALLED");

            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            Log.Message("CARGO SHUTTLE ABOUT TO YIELD WATER GIZMO");

            yield return new Command_Action
            {
                defaultLabel = "Discharge",
                defaultDesc = "Unload water into the adjacent DBH plumbing network.",
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
                Disabled = waterStorage <= 0f || AdjacentPlumbingNet == null,
                action = () => ShuttleWater.UnloadWater(parent)
            };
            
        }

        public override string CompInspectStringExtra()
        {
            return $"Stored Water: {waterStorage:0.##} / {waterCapacity:0.##}";
        }

        public override void CompTick()
        {
            base.CompTick();

            if (parent.IsHashIntervalTick(180))
            {
                AdjacentPlumbingNet = ShuttleWater.CheckCellsAroundShuttle(parent);
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref waterStorage, "waterStorage", 0f);
        }
    }
}
