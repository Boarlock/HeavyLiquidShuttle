using System;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
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
        // Gizmos for the Shuttle and Tick Integration action.
        public static event Func<HeavyLiquidShuttle, IEnumerable<Gizmo>>? GizmoIntegration;
        public static event Action<HeavyLiquidShuttle>? TickIntegration;

        // Bools for global fluid dispensing and recieving state.
        public bool IsDraining { get; private set; }
        public bool IsReceivingFluid { get; private set; }
        public bool IsDispensingFluid { get; private set; }
        public void BeginReceivingFluid()
        {
            IsReceivingFluid = true;
        }
        public void EndReceivingFluid()
        {
            IsReceivingFluid = false;
        }
        public void BeginDispensingFluid()
        {
            IsDispensingFluid = true;
        }
        public void EndDispensingFluid()
        {
            IsDispensingFluid = false;
        }

        // Dubs water fields.
        private float waterStorage;
        private float waterCapacity = 2500f; // Liters

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

            if (GizmoIntegration != null)
            {
                foreach (Gizmo gizmo in GizmoIntegration(this))
                    yield return gizmo;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!parent.IsHashIntervalTick(10))
                return;

            TickIntegration?.Invoke(this);
        }

        public void DrainToggle()
        {
            IsDraining = !IsDraining;
        }

        public override string CompInspectStringExtra()
        {
            return $"Water Stored {waterStorage:0.##} / {waterCapacity:0.##} L";
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref waterStorage, "waterStorage", 0f);
        }
    }
}
