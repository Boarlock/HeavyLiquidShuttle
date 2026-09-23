using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
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
        public HeavyLiquidShuttle()
        {
            bool dbhActive = HeavyLiquidShuttleMod.DubsBadHygieneActive;
            bool rimefellerActive = HeavyLiquidShuttleMod.RimefellerActive;
            bool vechemActive = HeavyLiquidShuttleMod.VEChemfuelActive;
            bool vehelixActive = HeavyLiquidShuttleMod.VEHelixienActive;

            if (dbhActive && rimefellerActive && LibraryLoaders.DubwiseSharedIntegrationType != null)
            {
                sharedIntegration = Activator.CreateInstance(LibraryLoaders.DubwiseSharedIntegrationType, this);
            }
            else if (dbhActive && LibraryLoaders.DBHIntegrationType != null)
            {
                dbhIntegration = Activator.CreateInstance(LibraryLoaders.DBHIntegrationType, this);
            }
            else if (rimefellerActive && LibraryLoaders.RimefellerIntegrationType != null)
            {
                rimefellerIntegration = Activator.CreateInstance(LibraryLoaders.RimefellerIntegrationType, this);
            }
            
            if ((vechemActive || vehelixActive) && LibraryLoaders.VESharedIntegrationType != null)
            {
                veIntegration = Activator.CreateInstance(LibraryLoaders.VESharedIntegrationType, this);
            }
        }

        // Integration instances
        private readonly object? sharedIntegration;
        private readonly object? dbhIntegration;
        private readonly object? rimefellerIntegration;
        private readonly object? veIntegration;

        public object? SharedIntegration => sharedIntegration;
        public object? DBHIntegration => dbhIntegration;
        public object? RimefellerIntegration => rimefellerIntegration;
        public object? VEIntegration => veIntegration;


        // Integration events
        public static event Func<IEnumerable<Gizmo>>? GizmoIntegration;
        public static event Action? TickIntegration;
        public static event Action<float>? OilSpillIntegration;


        // Tank specific state data
        public TankState TankA = new TankState();
        public TankState TankB = new TankState();

        // toggle for when a Shuttle Tank is actively transferring
        public void ToggleTransfer(TankState tank)
        {
            tank.TransferEnabled = !tank.TransferEnabled;
        }

        // Used for placing Oil Spills from Rimefeller
        public IntVec3 OilConnectionAt {  get; set; }

        // Helper for returning Shuttle Tanks
        public TankState? GetTankForContent(StoredType content)
        {
            if (TankA.Content == content && TankA.TankStorage < TankA.TankCapacity)
                return TankA;

            if (TankB.Content == content && TankB.TankStorage < TankB.TankCapacity)
                return TankB;

            if (TankA.Content == StoredType.Empty)
                return TankA;

            if (TankB.Content == StoredType.Empty)
                return TankB;

            return null;
        }

        public float CalculateMassFromTanks()
        {
            float totalMass = 0f;

            switch (TankA.Content)
            {
                case StoredType.Water:
                    totalMass += TankA.TankStorage;
                    break;
                case StoredType.Oil:
                    totalMass += TankA.TankStorage * HeavyLiquidShuttleManager.CrudeMassPerLiter;
                    break;
                case StoredType.Deepchem:
                    totalMass += TankA.TankStorage * HeavyLiquidShuttleManager.DeepchemMassPerLiter;
                    break;
                case StoredType.Helixien:
                    totalMass += TankA.TankStorage * HeavyLiquidShuttleManager.HelixienMassPerLiter;
                    break;
            }

            switch (TankB.Content)
            {
                case StoredType.Water:
                    totalMass += TankB.TankStorage;
                    break;
                case StoredType.Oil:
                    totalMass += TankB.TankStorage * HeavyLiquidShuttleManager.CrudeMassPerLiter;
                    break;
                case StoredType.Deepchem:
                    totalMass += TankB.TankStorage * HeavyLiquidShuttleManager.DeepchemMassPerLiter;
                    break;
                case StoredType.Helixien:
                    totalMass += TankB.TankStorage * HeavyLiquidShuttleManager.HelixienMassPerLiter;
                    break;
            }

            return totalMass;
        }

        // Gizmo IEnumerable
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (TankA.TankStorage > 0f)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank A",
                    defaultDesc = "Drain Tank A of its contents.",
                    icon = ContentFinder<Texture2D>.Get("DBH/UI/drainOut"),
                    action = () =>
                    {
                        if (TankA.Content == StoredType.Water)
                        {
                            TankA.IsContaminated = false;
                        }
                        else if (TankA.Content == StoredType.Oil)
                        {
                            float amountToSpill = TankA.TankStorage;
                            OilSpillIntegration?.Invoke(amountToSpill);
                        }

                        TankA.Content = StoredType.Empty;
                        TankA.TankStorage = 0f;
                        TankA.TransferEnabled = false;
                        TankA.IsTransferringFluid = false;
                        TankA.ReceiveAllowance = 1.0;
                    }
                };
            }
            if (TankB.TankStorage > 0f)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank B",
                    defaultDesc = "Drain Tank B of its contents.",
                    icon = ContentFinder<Texture2D>.Get("DBH/UI/drainOut"),
                    action = () =>
                    {
                        if (TankB.Content == StoredType.Water)
                        {
                            TankB.IsContaminated = false;
                        }
                        else if (TankB.Content == StoredType.Oil)
                        {
                            float amountToSpill = TankB.TankStorage;
                            OilSpillIntegration?.Invoke(amountToSpill);
                        }

                        TankB.Content = StoredType.Empty;
                        TankB.TankStorage = 0f;
                        TankB.TransferEnabled = false;
                        TankB.IsTransferringFluid = false;
                        TankB.ReceiveAllowance = 1.0;
                    }
                };
            }

            if (GizmoIntegration != null)
            {
                foreach (Delegate subscriber in GizmoIntegration.GetInvocationList())
                {
                    Func<HeavyLiquidShuttle, IEnumerable<Gizmo>> integration = (Func<HeavyLiquidShuttle, IEnumerable<Gizmo>>)subscriber;

                    foreach (Gizmo gizmo in integration(this))
                        yield return gizmo;
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            string tankA = "";
            string tankB = "";

            if (TankA.IsContaminated && TankA.Content == StoredType.Water)
                tankA = $"Tank A: {TankA.Content} (Contaminated) |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";
            else
                tankA = $"Tank A: {TankA.Content} |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";

            if (TankB.IsContaminated && TankB.Content == StoredType.Water)
                tankB = $"Tank B: {TankB.Content} (Contaminated) |  Capacity: {TankB.TankStorage:F0} / {TankB.TankCapacity} Liters";
            else
                tankB = $"Tank B: {TankB.Content} |  Capacity: {TankB.TankStorage:F0} / {TankB.TankCapacity} Liters";

            return tankA + tankB;

        }

        public override void CompTick()
        {
            base.CompTick();

            if (!parent.IsHashIntervalTick(10))
                return;

            TickIntegration?.Invoke();
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref TankA.TankStorage, "tankAStorage", 0f);
            Scribe_Values.Look(ref TankB.TankStorage, "tankBStorage", 0f);
            Scribe_Values.Look(ref TankA.Content, "tankAContent", StoredType.Empty);
            Scribe_Values.Look(ref TankB.Content, "tankBContent", StoredType.Empty);
            Scribe_Values.Look(ref TankA.TransferEnabled, "tankATransferEnabled", false);
            Scribe_Values.Look(ref TankB.TransferEnabled, "tankBTransferEnabled", false);
        }
    }
}
