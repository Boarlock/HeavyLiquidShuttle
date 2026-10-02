using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static HeavyLiquidShuttleMod.TankState;

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

            TankA.transferEnabled = false;
            TankB.transferEnabled = false;

            if (TankA.content == CachedDefs.Helixien || TankB.content == CachedDefs.Helixien)
            {
                TankA.tankExplosiveness = TankA.GetHelixienState(out _);
                TankB.tankExplosiveness = TankB.GetHelixienState(out _);

                ShuttleExplosion explosion = parent.TryGetComp<ShuttleExplosion>();

                if (explosion != null)
                    explosion.UpdateExplosiveness();
            }
            else
            {
                TankA.tankExplosiveness = TankState.HelixienState.None;
                TankB.tankExplosiveness = TankState.HelixienState.None;
            }
        }

        private void CreateIntegrations()
        {
            bool dbhActive = HeavyLiquidShuttleMod.DubsBadHygieneActive;
            bool rimefellerActive = HeavyLiquidShuttleMod.RimefellerActive;
            bool vechemActive = HeavyLiquidShuttleMod.VEChemfuelActive;
            bool vehelixActive = HeavyLiquidShuttleMod.VEHelixienActive;
            bool vescarletActive = HeavyLiquidShuttleMod.VEScarletActive;
            bool vegravshipActive = HeavyLiquidShuttleMod.VEGravshipActive;

            if (dbhActive && LibraryLoaders.DBHIntegrationType != null)
                dbhIntegration = Activator.CreateInstance(LibraryLoaders.DBHIntegrationType, this);

            if (rimefellerActive && LibraryLoaders.RimefellerIntegrationType != null)
                rimefellerIntegration = Activator.CreateInstance(LibraryLoaders.RimefellerIntegrationType, this);

            if ((vechemActive || 
                vehelixActive || 
                vescarletActive || 
                vegravshipActive) && 
                LibraryLoaders.VESharedIntegrationType != null)
                veIntegration = Activator.CreateInstance(LibraryLoaders.VESharedIntegrationType, this);

            initialized = true;
        }

        // Integration instances
        internal object? sharedIntegration;
        internal object? dbhIntegration;
        internal object? rimefellerIntegration;
        internal object? veIntegration;


        // Integration events
        public event Func<IEnumerable<Gizmo>>? GizmoIntegration;
        public event Action<float, IntVec3>? OilSpillIntegration;
        public event Action<float, IntVec3>? SewageSpillIntegration;


        // Tank specific state data
        public TankState TankA = new TankState();
        public TankState TankB = new TankState();
        public bool CleaningTankA;
        public bool CleaningTankB;

        // Toggle for when a Shuttle Tank is actively transferring
        public void ToggleTransfer(TankState tank)
        {
            tank.transferEnabled = !tank.transferEnabled;
        }

        // Toggle for when a Shuttle Tank is actively locked
        public void ToggleLock(TankState tank)
        {
            tank.isLocked = !tank.isLocked;
        }

        // Used for Mod Integration special actions
        private Detonate? detonation;

        // Helpers for returning Shuttle Tanks
        public TankState? GetTankForReceive(StoredTypeDef def)
        {
            if (!TankA.isLocked)
            {
                if (TankA.content == null)
                    return TankA;

                if (TankA.content == def && TankA.tankStorage < TankA.props.physicalCapacity)
                    return TankA;
            }

            if (!TankB.isLocked)
            {
                if (TankB.content == null)
                    return TankB;

                if (TankB.content == def && TankB.tankStorage < TankB.props.physicalCapacity)
                    return TankB;
            }
            return null;
        }
        public TankState? GetTankForSupply(StoredTypeDef def)
        {
            if (TankA.content != null && 
                TankA.content == def && 
                TankA.tankStorage > 0f && 
                !TankA.isLocked)
                return TankA;

            if (TankB.content != null && 
                TankB.content == def && 
                TankB.tankStorage > 0f && 
                !TankB.isLocked)
                return TankB;
            
            return null;
        }

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
                isActive = () => this.TankA.isLocked,
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
                isActive = () => this.TankB.isLocked,
                toggleAction = () =>
                {
                    ToggleLock(this.TankB);
                }
            };

            if (TankA.tankStorage > 0f)
            {
                string drainIcon = TankA.content == CachedDefs.Helixien ? "UI/Gizmo/Vent" : "UI/Gizmo/Drain";

                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank A",
                    defaultDesc = "Drain Tank A of its contents.",
                    icon = ContentFinder<Texture2D>.Get(drainIcon),
                    action = () =>
                    {
                        if (TankA.content == CachedDefs.Water)
                        {
                            TankA.isContaminated = false;
                        }
                        else if (TankA.content == CachedDefs.Oil)
                        {
                            IntVec3 spillCell = GetTankCell(true);
                            OilSpillIntegration?.Invoke(TankA.tankStorage, spillCell);
                        }
                        else if (TankA.content == CachedDefs.Sewage)
                        {
                            IntVec3 spillCell = GetTankCell(true);
                            SewageSpillIntegration?.Invoke(TankA.tankStorage, spillCell);
                        }

                        TankA.content = null;
                        TankA.tankExplosiveness = TankState.HelixienState.None;
                        TankA.tankStorage = 0f;
                        TankA.transferEnabled = false;
                    }
                };
            }
            if (TankB.tankStorage > 0f)
            {
                string drainIcon = TankB.content == CachedDefs.Helixien ? "UI/Gizmo/Vent" : "UI/Gizmo/Drain";

                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank B",
                    defaultDesc = "Drain Tank B of its contents.",
                    icon = ContentFinder<Texture2D>.Get(drainIcon),
                    action = () =>
                    {
                        if (TankB.content == CachedDefs.Water)
                        {
                            TankB.isContaminated = false;
                        }
                        else if (TankB.content == CachedDefs.Oil)
                        {
                            IntVec3 spillCell = GetTankCell(false);
                            OilSpillIntegration?.Invoke(TankB.tankStorage, spillCell);
                        }
                        else if (TankB.content == CachedDefs.Sewage)
                        {
                            IntVec3 spillCell = GetTankCell(false);
                            SewageSpillIntegration?.Invoke(TankB.tankStorage, spillCell);
                        }

                        TankB.content = null;
                        TankB.tankExplosiveness = TankState.HelixienState.None;
                        TankB.tankStorage = 0f;
                        TankB.transferEnabled = false;
                    }
                };
            }

            if (TankA.isContaminated)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Clean Tank A",
                    defaultDesc = "Clean Tank A of its contamination.",
                    Disabled = TankA.content != null,
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
            if (TankB.isContaminated)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Clean Tank B",
                    defaultDesc = "Clean Tank B of its contamination.",
                    Disabled = TankB.content != null,
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

        public static Gizmo CreateDischargeGizmo(HeavyLiquidShuttle shuttle, bool handleTankA, StoredTypeDef def)
        {
            if (handleTankA)
            {
                return new Command_Toggle
                {
                    defaultLabel = "Discharge " + def.label,
                    defaultDesc = "Tank A: Discharge into an adjacent " + def.label + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankA.transferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankA.tankStorage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankA);
                    }
                };
            }

            else
            {
                return new Command_Toggle
                {
                    defaultLabel = "Discharge " + def.label,
                    defaultDesc = "Tank B: Discharge into an adjacent " + def.label + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankB.transferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankB.tankStorage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankB);
                    }
                };
            }
        }

        private void TryStartTankCleaning(bool handlingA)
        {
            IntVec3 refCell = this.parent.OccupiedRect().First();
            IntVec3 jobCell = GetTankCell(handlingA);

            if (!jobCell.IsValid)
            {
                if (handlingA) CleaningTankA = false;
                else CleaningTankB = false;
                return;
            }

            LocalTargetInfo jobTargetA = new LocalTargetInfo(jobCell);
            LocalTargetInfo jobTargetB = new LocalTargetInfo(parent);
            LocalTargetInfo jobTargetC = new LocalTargetInfo(refCell);

            Job job = JobMaker.MakeJob(CleanShuttleTank.CleanTank, jobTargetA, jobTargetB, jobTargetC);

            Pawn? closestColonist = null;
            float closestDistance = float.MaxValue;

            foreach (Pawn candidate in parent.Map.mapPawns.FreeColonistsSpawned)
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

        private IntVec3 GetTankCell(bool tankA)
        {
            Rot4 shuttleOrientation = parent.Rotation;
            int cell = 0;
            IntVec3 tankCell = new IntVec3(-1, -1, -1);

            if (tankA)
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
            else
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
                return tankCell;

            int i = 0;

            foreach (IntVec3 shuttleCell in this.parent.OccupiedRect())
            {
                if (cell == i)
                {
                    tankCell = shuttleCell;
                    break;
                }
                i++;
            }
            return tankCell;
        }

        // Displays tank contents
        public override string CompInspectStringExtra()
        {
            string tankA;
            string tankB;

            string contaminatedA = TankA.isContaminated ? " (Contaminated)" : "";
            string contaminatedB = TankB.isContaminated ? " (Contaminated)" : "";

            if (TankA.content == null)
                tankA = $"Tank A: Empty{contaminatedA} |  Capacity: 0 / {TankA.props.physicalCapacity} Liters\n";

            else if (TankA.content == CachedDefs.Water)
                tankA = $"Tank A: {TankA.content} ({TankA.waterQuality}) |  Capacity: {TankA.tankStorage:F0} / {TankA.props.physicalCapacity} Liters\n";
            
            else
                tankA = $"Tank A: {TankA.content} |  Capacity: {TankA.tankStorage:F0} / {TankA.props.physicalCapacity} Liters\n";

            if (TankB.content == null)
                tankB = $"Tank B: Empty{contaminatedB} |  Capacity: 0 / {TankB.props.physicalCapacity} Liters";

            else if (TankB.content == CachedDefs.Water)
                tankB = $"Tank B: {TankB.content} ({TankB.waterQuality}) |  Capacity: {TankB.tankStorage:F0} / {TankB.props.physicalCapacity} Liters";
            
            else
                tankB = $"Tank B: {TankB.content} |  Capacity: {TankB.tankStorage:F0} / {TankB.props.physicalCapacity} Liters";

            if (TankA.content == CachedDefs.Helixien || TankB.content == CachedDefs.Helixien)
            {
                int totalExplosiveness = TankA.GetExplosiveness() + TankB.GetExplosiveness();
                string explosiveness;

                if (totalExplosiveness >= 5)
                    explosiveness = "\nExplosiveness: High";
                else if (totalExplosiveness >= 3)
                    explosiveness = "\nExplosiveness: Moderate";
                else if (totalExplosiveness >= 1)
                    explosiveness = "\nExplosiveness: Low";
                else
                    explosiveness = "\nExplosiveness: None";

                return tankA + tankB + explosiveness;
            }

            return tankA + tankB;
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            ShuttleExplosion c = this.parent.TryGetComp<ShuttleExplosion>();

            if (c == null || c.explosiveness == TankState.HelixienState.None || parent.HitPoints / (float)parent.MaxHitPoints >
                c.Props.startWickHitPointsPercent || detonation != null)
                return;

            detonation = new Detonate(this);
            detonation.StartWick(dinfo.Instigator);
        }

        public override void CompTick()
        {
            detonation?.Tick();
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref TankA.tankStorage, "tankAStorage", 0f);
            Scribe_Values.Look(ref TankB.tankStorage, "tankBStorage", 0f);

            Scribe_Values.Look(ref TankA.content, "tankAContent", null);
            Scribe_Values.Look(ref TankB.content, "tankBContent", null);

            Scribe_Values.Look(ref TankA.isLocked, "tankALocked", false);
            Scribe_Values.Look(ref TankB.isLocked, "tankBLocked", false);

            Scribe_Values.Look(ref TankA.isContaminated, "tankAContaminated", false);
            Scribe_Values.Look(ref TankB.isContaminated, "tankBContaminated", false);

            Scribe_Values.Look(ref TankA.props.physicalCapacity, "tankACapacity", 1250f);
            Scribe_Values.Look(ref TankB.props.physicalCapacity, "tankBCapacity", 1250f);

            Scribe_Values.Look(ref TankA.props.pressureRating, "tankAPressureRating", 150f);
            Scribe_Values.Look(ref TankB.props.pressureRating, "tankBPressurRating", 150f);

            Scribe_Values.Look(ref TankA.waterQuality, "tankAWaterQuality", TankState.WaterState.Untreated);
            Scribe_Values.Look(ref TankB.waterQuality, "tankBWaterQuality", TankState.WaterState.Untreated);

            Scribe_Values.Look(ref TankA.tankExplosiveness, "tankAExplosiveness", TankState.HelixienState.None);
            Scribe_Values.Look(ref TankB.tankExplosiveness, "tankBExplosiveness", TankState.HelixienState.None);
        }
    }
}
