using System;
using System.Collections.Generic;
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
        // Integration events.
        public static event Func<HeavyLiquidShuttle, IEnumerable<Gizmo>>? GizmoIntegration;
        public static event Action<HeavyLiquidShuttle>? TickIntegration;
        public static event Action<HeavyLiquidShuttle, float>? OilSpillIntegration;

        // Tank specific state fields.
        public TankState TankA = new TankState();
        public TankState TankB = new TankState();

        public const float CrudeMassPerLiter = 0.85f;
        public IntVec3 OilConnectionAt;

        public TankState? GetTankForContent(TankState.StoredType content)
        {
            if (TankA.Content == content && TankA.TankStorage < TankA.TankCapacity)
                return TankA;

            if (TankB.Content == content && TankB.TankStorage < TankB.TankCapacity)
                return TankB;

            if (TankA.Content == TankState.StoredType.Empty)
                return TankA;

            if (TankB.Content == TankState.StoredType.Empty)
                return TankB;

            return null;
        }

        public TankState? GetTankForTransfer(TankState.StoredType content)
        {
            if (TankA.Content == content && TankA.TankStorage > 0f)
                return TankA;

            if (TankB.Content == content && TankB.TankStorage > 0f)
                return TankB;

            return null;
        }

        public float CalculateMassFromTanks()
        {
            float totalMass = 0f;

            if (TankA.Content == TankState.StoredType.Water)
            {
                totalMass += TankA.TankStorage;
            }
            else
            {
                totalMass += TankA.TankStorage * CrudeMassPerLiter;
            }

            if (TankB.Content == TankState.StoredType.Water)
            {
                totalMass += TankB.TankStorage;
            }
            else
            {
                totalMass += TankB.TankStorage * CrudeMassPerLiter;
            }

            return totalMass;
        }

        public void ToggleTransfer(TankState tank)
        {
            tank.TransferEnabled = !tank.TransferEnabled;
        }

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
                        if (TankA.Content == TankState.StoredType.Water)
                        {
                            TankA.IsContaminated = false;
                        }
                        else if (TankA.Content == TankState.StoredType.Oil)
                        {
                            float amountToSpill = TankA.TankStorage;
                            OilSpillIntegration?.Invoke(this, amountToSpill);
                        }

                        TankA.Content = TankState.StoredType.Empty;
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
                        if (TankB.Content == TankState.StoredType.Water)
                        {
                            TankB.IsContaminated = false;
                        }
                        else if (TankB.Content == TankState.StoredType.Oil)
                        {
                            float amountToSpill = TankB.TankStorage;
                            OilSpillIntegration?.Invoke(this, amountToSpill);
                        }

                        TankB.Content = TankState.StoredType.Empty;
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

            if (TankA.IsContaminated && TankA.Content != TankState.StoredType.Oil)
                tankA = $"Tank A: {TankA.Content} (Contaminated) |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";
            else
                tankA = $"Tank A: {TankA.Content} |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";

            if (TankB.IsContaminated && TankB.Content != TankState.StoredType.Oil)
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

            TickIntegration?.Invoke(this);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref TankA.TankStorage, "tankAStorage", 0f);
            Scribe_Values.Look(ref TankB.TankStorage, "tankBStorage", 0f);
            Scribe_Values.Look(ref TankA.Content, "tankAContent", TankState.StoredType.Empty);
            Scribe_Values.Look(ref TankB.Content, "tankBContent", TankState.StoredType.Empty);
            Scribe_Values.Look(ref TankA.TransferEnabled, "tankATransferEnabled", false);
            Scribe_Values.Look(ref TankB.TransferEnabled, "tankBTransferEnabled", false);
        }
    }

    public class TankState
    {
        public enum StoredType
        {
            Empty,
            Water,
            Oil,
            Deepchem,
            Helixien
        }

        public float TankCapacity = 1250f;
        public float TankStorage = 0f;
        public StoredType Content = StoredType.Empty;
        public bool IsContaminated = false;
        public int Counter = 0;

        // Persistent player-controlled transfer state.
        public bool TransferEnabled;

        // Temporary operation guards.
        public bool IsTransferringFluid;

        // Maximum amount this tank can receive during the current transfer interval.
        public double ReceiveAllowance = 1.0;
    }
}
