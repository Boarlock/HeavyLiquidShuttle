using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

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
        private bool initialized = false;
        private bool cleanedUp = false;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!initialized)
            {
                CreateIntegrations();
            }

            TankA.TransferEnabled = false;
            TankB.TransferEnabled = false;
        }

        private void CreateIntegrations()
        {
            bool dbhActive = HeavyLiquidShuttleMod.DubsBadHygieneActive;
            bool rimefellerActive = HeavyLiquidShuttleMod.RimefellerActive;
            bool vechemActive = HeavyLiquidShuttleMod.VEChemfuelActive;
            bool vehelixActive = HeavyLiquidShuttleMod.VEHelixienActive;

            if (dbhActive && rimefellerActive && LibraryLoaders.DubwiseSharedIntegrationType != null)
            {
                Log.Message($"[HLS-Debug] {GetType().Name}: CREATED shuttle={this.parent?.LabelCap}");
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

            initialized = true;
        }

        // Integration instances
        internal object? sharedIntegration;
        internal object? dbhIntegration;
        internal object? rimefellerIntegration;
        internal object? veIntegration;


        // Integration events
        public event Func<IEnumerable<Gizmo>>? GizmoIntegration;
        public event Action<float>? OilSpillIntegration;


        // Tank specific state data
        public TankState TankA = new TankState();
        public TankState TankB = new TankState();
        public bool CleaningTankA;
        public bool CleaningTankB;

        // Toggle for when a Shuttle Tank is actively transferring
        public void ToggleTransfer(TankState tank)
        {
            tank.TransferEnabled = !tank.TransferEnabled;
        }

        // Toggle for when a Shuttle Tank is actively locked
        public void ToggleLock(TankState tank)
        {
            tank.IsLocked = !tank.IsLocked;
        }

        // Used for placing Oil Spills from Rimefeller
        public IntVec3 OilConnectionAt {  get; set; }

        // Helpers for returning Shuttle Tanks
        public TankState? GetTankForReceive(StoredType type)
        {
            if (!TankA.IsLocked)
            {
                if (TankA.Content == type && TankA.TankStorage < TankA.TankCapacity)
                    return TankA;

                if (TankA.Content == StoredType.Empty)
                    return TankA;
            }

            if (!TankB.IsLocked)
            {
                if (TankB.Content == type && TankB.TankStorage < TankB.TankCapacity)
                    return TankB;

                if (TankB.Content == StoredType.Empty)
                    return TankB;
            }
            return null;
        }
        public TankState? GetTankForSupply(StoredType type)
        {
            if (TankA.Content == type && TankA.TankStorage > 0f && !TankA.IsLocked)
                return TankA;

            if (TankB.Content == type && TankB.TankStorage > 0f && !TankB.IsLocked)
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

        // Gizmos for emptying both tanks, in the case of emptying an oil tank, it calls registered methods 
        // of OilSpillIntegration in RimefellerIntegration and DubwiseSharedIntegraation when present
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (cleanedUp)
                yield break;

            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            yield return new Command_Toggle
            {
                defaultLabel = "Lock Tank A",
                defaultDesc = "Tank A: Lock this tank so it cannot intake or output its contents.",
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Lock"),
                isActive = () => this.TankA.IsLocked,
                toggleAction = () =>
                {
                    ToggleLock(this.TankA);
                }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "Lock Tank B",
                defaultDesc = "Tank B: Lock this tank so it cannot intake or output its contents.",
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Lock"),
                isActive = () => this.TankB.IsLocked,
                toggleAction = () =>
                {
                    ToggleLock(this.TankB);
                }
            };

            if (TankA.TankStorage > 0f)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank A",
                    defaultDesc = "Drain Tank A of its contents.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Drain"),
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
                    }
                };
            }
            if (TankB.TankStorage > 0f)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank B",
                    defaultDesc = "Drain Tank B of its contents.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Drain"),
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
                    }
                };
            }

            if (TankA.IsContaminated)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Clean Tank A",
                    defaultDesc = "Clean Tank A of its contamination.",
                    Disabled = TankA.Content != StoredType.Empty,
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Clean"),
                    isActive = () => CleaningTankA,
                    toggleAction = () =>
                    {
                        CleaningTankA = !CleaningTankA;

                        if (CleaningTankA)
                        {
                            TryStartTankCleaning(true);
                        }
                    }
                };
            }
            if (TankB.IsContaminated)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Clean Tank B",
                    defaultDesc = "Clean Tank B of its contamination.",
                    Disabled = TankB.Content != StoredType.Empty,
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Clean"),
                    isActive = () => CleaningTankB,
                    toggleAction = () =>
                    {
                        CleaningTankB = !CleaningTankB;

                        if (CleaningTankB)
                        {
                            TryStartTankCleaning(false);
                        }
                    }
                };
            }

            if (GizmoIntegration != null)
            {
                foreach (Delegate subscriber in GizmoIntegration.GetInvocationList())
                {
                    Func<IEnumerable<Gizmo>> integration = (Func<IEnumerable<Gizmo>>)subscriber;

                    foreach (Gizmo gizmo in integration())
                        yield return gizmo;
                }
            }
        }

        public static Gizmo? CreateDischargeGizmo(HeavyLiquidShuttle shuttle, string tank, StoredType type)
        {
            if (tank == "Tank A")
            {
                return new Command_Toggle
                {
                    defaultLabel = "Discharge " + type,
                    defaultDesc = tank + ": Discharge into an adjacent " + type + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankA.TransferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankA.TankStorage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankA);
                    }
                };
            }

            else if (tank == "Tank B")
            {
                return new Command_Toggle
                {
                    defaultLabel = "Discharge " + type,
                    defaultDesc = tank + ": Discharge into an adjacent " + type + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankB.TransferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankB.TankStorage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankB);
                    }
                };
            }
            return null;
        }

        private void TryStartTankCleaning(bool handlingA)
        {
            Rot4 shuttleOrientation = this.parent.Rotation;
            int cell = 0;
            IntVec3 jobCell = new IntVec3(0, 0, 0);
            IntVec3 refCell = new IntVec3(0, 0, 0);

            if (handlingA)
            {
                switch (shuttleOrientation.AsInt)
                {
                    case 0:
                        cell = 3;
                        break;
                    case 2:
                        cell = 14;
                        break;
                    case 1:
                        cell = 13;
                        break;
                    case 3:
                        cell = 4;
                        break;
                }
            }
            else if (!handlingA)
            {
                switch (shuttleOrientation.AsInt)
                {
                    case 0:
                        cell = 5;
                        break;
                    case 2:
                        cell = 12;
                        break;
                    case 1:
                        cell = 1;
                        break;
                    case 3:
                        cell = 16;
                        break;
                }
            }

            if (cell == 0)
            {
                if (handlingA)
                    CleaningTankA = false;
                else
                    CleaningTankB = false;

                return;
            }

            int i = 0;

            foreach (IntVec3 shuttleCell in this.parent.OccupiedRect())
            {
                if (cell == i)
                {
                    jobCell = shuttleCell;
                }

                if (i == 0)
                {
                    refCell = shuttleCell;
                }

                i++;
            }
            

            if (!jobCell.IsValid)
            {
                if (handlingA) CleaningTankA = false;
                else CleaningTankB = false;
                return;
            }

            LocalTargetInfo jobTargetA = new LocalTargetInfo(jobCell);
            LocalTargetInfo jobTargetB = new LocalTargetInfo(this.parent);
            LocalTargetInfo jobTargetC = new LocalTargetInfo(refCell);

            Job job = JobMaker.MakeJob(CleanShuttleTank.CleanTank, jobTargetA, jobTargetB, jobTargetC);

            Pawn? closestColonist = null;
            float closestDistance = float.MaxValue;

            foreach (Pawn candidate in this.parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (candidate.Dead || candidate.Downed)
                    continue;

                if (candidate.CurJobDef == JobDefOf.LayDown)
                    continue;

                if (!candidate.CanReach(jobCell, PathEndMode.Touch, Danger.Some))
                    continue;

                float distance = candidate.Position.DistanceToSquared(jobCell);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestColonist = candidate;
                }
            }

            if (closestColonist == null)
            {
                if (handlingA) CleaningTankA = false;
                else CleaningTankB = false;
                return;
            }

            if (!closestColonist.jobs.TryTakeOrderedJob(job))
            {
                if (handlingA)
                    CleaningTankA = false;
                else
                    CleaningTankB = false;
            }
        }

        // Displays tank contents
        public override string CompInspectStringExtra()
        {
            string tankA;
            string tankB;

            if (TankA.Content == StoredType.Water)
                tankA = $"Tank A: {TankA.Content} ({TankA.WaterQuality}) |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";
            else if (TankA.Content == StoredType.Empty && TankA.IsContaminated)
                tankA = $"Tank A: Empty (Contaminated) |  Capacity: 0 / {TankA.TankCapacity} Liters\n";
            else
                tankA = $"Tank A: {TankA.Content} |  Capacity: {TankA.TankStorage:F0} / {TankA.TankCapacity} Liters\n";

            if (TankB.Content == StoredType.Water)
                tankB = $"Tank B: {TankB.Content} ({TankB.WaterQuality}) |  Capacity: {TankB.TankStorage:F0} / {TankB.TankCapacity} Liters";
            else if (TankB.Content == StoredType.Empty && TankB.IsContaminated)
                tankB = $"Tank B: Empty (Contaminated) |  Capacity: 0 / {TankB.TankCapacity} Liters";
            else
                tankB = $"Tank B: {TankB.Content} |  Capacity: {TankB.TankStorage:F0} / {TankB.TankCapacity} Liters";

            return tankA + tankB;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref TankA.TankStorage, "tankAStorage", 0f);
            Scribe_Values.Look(ref TankB.TankStorage, "tankBStorage", 0f);
            Scribe_Values.Look(ref TankA.Content, "tankAContent", StoredType.Empty);
            Scribe_Values.Look(ref TankB.Content, "tankBContent", StoredType.Empty);
            Scribe_Values.Look(ref TankA.IsLocked, "tankALocked", false);
            Scribe_Values.Look(ref TankB.IsLocked, "tankBLocked", false);
        }
    }
}
