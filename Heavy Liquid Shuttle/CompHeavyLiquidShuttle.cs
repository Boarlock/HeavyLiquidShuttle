using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class CompProperties_HLShuttleCarrier : CompProperties_Shuttle
    {
        public CompProperties_HLShuttleCarrier()
        {
            compClass = typeof(HeavyLiquidShuttle);
        }
    }

    public class HeavyLiquidShuttle : CompShuttle
    {
        public new CompProperties_HLShuttleCarrier Props => (CompProperties_HLShuttleCarrier)props;

        private bool initialized = false;
        private bool cleanedUp = false;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!initialized)
            {
                CreateIntegrations();
            }

            TankA ??= new TankState();
            TankB ??= new TankState();

            TankA!.transferEnabled = false;
            TankB!.transferEnabled = false;

            HeavyLiquidShuttleGameComp.TickIntegration += TickCycle;
        }

        public const TransferState CachedHolding = TransferState.Holding;
        public const TransferState CachedDischarging = TransferState.Discharging;
        public const TransferState CachedReceiving = TransferState.Receiving;

        public long currentCycle = -1;
        public bool stateUpdated = false;

        private void TickCycle()
        {
            detonation?.Tick();

            bool tankASupply = TankA!.lastSupplyCycle != currentCycle;
            bool tankAReceive = TankA.lastReceiveCycle != currentCycle;

            if (tankASupply && tankAReceive)
            {
                TankA.tankStatus = CachedHolding;
            }
            else if (tankASupply)
            {
                TankA.tankStatus = CachedDischarging;
                TankA.lastNetReceive = null;
            }
            else if (tankAReceive)
            {
                TankA.tankStatus = CachedReceiving;
                TankA.lastNetSupply = null;
            }
            else
            {
                TankA.tankStatus = CachedHolding;
                TankA.lastNetReceive = null;
                TankA.lastNetSupply = null;
            }

            bool tankBSupply = TankB!.lastSupplyCycle != currentCycle;
            bool tankBReceive = TankB.lastReceiveCycle != currentCycle;

            if (tankBSupply && tankBReceive)
            {
                TankB.tankStatus = CachedHolding;
            }
            else if (tankBSupply)
            {
                TankB.tankStatus = CachedDischarging;
                TankB.lastNetReceive = null;
            }
            else if (tankBReceive)
            {
                TankB.tankStatus = CachedReceiving;
                TankB.lastNetSupply = null;
            }
            else
            {
                TankB.tankStatus = CachedHolding;
                TankB.lastNetReceive = null;
                TankB.lastNetSupply = null;
            }

            currentCycle++;
            TankA.lastSupplyCycle = currentCycle;
            TankA.lastReceiveCycle = currentCycle;
            TankB.lastSupplyCycle = currentCycle;
            TankB.lastReceiveCycle = currentCycle;

            stateUpdated = true;
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

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);

            TankA ??= new TankState();
            TankB ??= new TankState();

            explosionComponent = parent.TryGetComp<ShuttleExplosion>();
        }

        // Integration instances
        internal object? dbhIntegration;
        internal object? rimefellerIntegration;
        internal object? veIntegration;


        // Integration events
        public event Func<IEnumerable<Gizmo>>? GizmoIntegration;
        public event Action<float, IntVec3>? OilSpillIntegration;
        public event Action<float, IntVec3>? SewageSpillIntegration;


        // Explosion Component
        private ShuttleExplosion? explosionComponent;


        // Tank specific state data
        public TankState? TankA;
        public TankState? TankB;
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
        public bool chemfuelAllowance = false;

        // Helpers for returning Shuttle Tanks
        public TankState? GetTankForReceive(StoredTypeDef def)
        {
            if (TankA!.isLocked)
            {
                if (TankA.Content == null)
                    return TankA;

                if (TankA.Content == def && TankA.Storage < TankA.Capacity)
                    return TankA;
            }

            if (TankB!.isLocked)
            {
                if (TankB.Content == null)
                    return TankB;

                if (TankB.Content == def && TankB.Storage < TankB.Capacity)
                    return TankB;
            }
            return null;
        }
        public TankState? GetTankForSupply(StoredTypeDef def)
        {
            if (TankA!.Content != null && 
                TankA.Content == def && 
                TankA.Storage > 0f && 
                !TankA.isLocked)
                return TankA;

            if (TankB!.Content != null && 
                TankB.Content == def && 
                TankB.Storage > 0f && 
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
                isActive = () => TankA!.isLocked,
                toggleAction = () =>
                {
                    ToggleLock(TankA!);
                }
            };
            yield return new Command_Toggle
            {
                defaultLabel = "Lock Tank B",
                defaultDesc = "Tank B: Lock this tank so it cannot intake or output its contents.",
                icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Lock"),
                isActive = () => TankB!.isLocked,
                toggleAction = () =>
                {
                    ToggleLock(TankB!);
                }
            };

            if (TankA!.Storage > 0f)
            {
                string drainIcon = TankA!.Content == CachedDefs.Helixien ? "UI/Gizmo/Vent" : "UI/Gizmo/Drain";

                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank A",
                    defaultDesc = "Drain Tank A of its contents.",
                    icon = ContentFinder<Texture2D>.Get(drainIcon),
                    action = () =>
                    {
                        IntVec3 spillCell;

                        if (TankA.Content == CachedDefs.Oil)
                        {
                            spillCell = GetTankCell(true);
                            OilSpillIntegration?.Invoke(TankA.Storage, spillCell);
                        }
                        else if (TankA.Content == CachedDefs.Sewage)
                        {
                            spillCell = GetTankCell(true);
                            SewageSpillIntegration?.Invoke(TankA.Storage, spillCell);
                        }

                        TankA.Content = null;
                        TankA.Storage = 0f;
                        TankA.transferEnabled = false;
                        explosionComponent?.UpdateExplosiveness();
                    }
                };
            }
            if (TankB!.Storage > 0f)
            {
                string drainIcon = TankB.Content == CachedDefs.Helixien ? "UI/Gizmo/Vent" : "UI/Gizmo/Drain";

                yield return new Command_Action
                {
                    defaultLabel = "Drain Tank B",
                    defaultDesc = "Drain Tank B of its contents.",
                    icon = ContentFinder<Texture2D>.Get(drainIcon),
                    action = () =>
                    {
                        IntVec3 spillCell;

                        if (TankB.Content == CachedDefs.Oil)
                        {
                            spillCell = GetTankCell(false);
                            OilSpillIntegration?.Invoke(TankB.Storage, spillCell);
                        }
                        else if (TankB.Content == CachedDefs.Sewage)
                        {
                            spillCell = GetTankCell(false);
                            SewageSpillIntegration?.Invoke(TankB.Storage, spillCell);
                        }

                        TankB.Content = null;
                        TankB.Storage = 0f;
                        TankB.transferEnabled = false;
                        explosionComponent?.UpdateExplosiveness();
                    }
                };
            }

            if (TankA.isContaminated)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "Clean Tank A",
                    defaultDesc = "Clean Tank A of its contamination.",
                    Disabled = TankA.Content != null,
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
                    Disabled = TankB.Content != null,
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
                    defaultLabel = "Discharge " + def.defName,
                    defaultDesc = "Tank A: Discharge into an adjacent " + def.defName + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankA!.transferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankA!.Storage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankA);
                    }
                };
            }

            else
            {
                return new Command_Toggle
                {
                    defaultLabel = "Discharge " + def.defName,
                    defaultDesc = "Tank B: Discharge into an adjacent " + def.defName + " network.",
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Discharge"),
                    isActive = () =>
                    {
                        return shuttle.TankB!.transferEnabled;
                    },
                    toggleAction = () =>
                    {
                        if (shuttle.TankB!.Storage <= 0f)
                            return;

                        shuttle.ToggleTransfer(shuttle.TankB);
                    }
                };
            }
        }

        private void TryStartTankCleaning(bool handlingA)
        {
            IntVec3 refCell = parent.OccupiedRect().First();
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

            foreach (IntVec3 shuttleCell in parent.OccupiedRect())
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
            string explosiveness = "Explosiveness: None";

            string contaminatedA = TankA!.isContaminated ? " (Contaminated)" : "";
            string contaminatedB = TankB!.isContaminated ? " (Contaminated)" : "";

            if (TankA.Content == null)
                tankA = $"Tank A: Empty{contaminatedA} |  0 / {TankA.Capacity} Liters\n";
            else if (TankA.Content == CachedDefs.Water)
                tankA = $"Tank A: {TankA.Content} ({TankA.waterQuality}) |  {TankA.Storage:F0} / {TankA.Capacity} Liters\n";
            else
                tankA = $"Tank A: {TankA.Content} |  {TankA.Storage:F0} / {TankA.Capacity} Liters\n";

            if (TankB.Content == null)
                tankB = $"Tank B: Empty{contaminatedB} |  0 / {TankB.Capacity} Liters\n";
            else if (TankB.Content == CachedDefs.Water)
                tankB = $"Tank B: {TankB.Content} ({TankB.waterQuality}) |  {TankB.Storage:F0} / {TankB.Capacity} Liters\n";
            else
                tankB = $"Tank B: {TankB.Content} |  {TankB.Storage:F0} / {TankB.Capacity} Liters\n";

            if (explosionComponent != null)
            {
                int radius = explosionComponent.ExplosionRadius;
                explosiveness = "Explosiveness: ";

                if (radius > 15)
                    explosiveness += "Massive";
                else if (radius >= 9)
                    explosiveness += "Extreme";
                else if (radius >= 5)
                    explosiveness += "High";
                else if (radius >= 2)
                    explosiveness += "Moderate";
                else if (radius > 0)
                    explosiveness += "Small";
                else
                    explosiveness += "None";
            }

            string baseText = base.CompInspectStringExtra();

            if (!baseText.NullOrEmpty())
                return tankA + tankB + explosiveness + "\n" + baseText;

            return tankA + tankB + explosiveness;
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            if (explosionComponent == null ||
                explosionComponent.ExplosionRadius <= 0 ||
                detonation != null ||
                parent.HitPoints / (float)parent.MaxHitPoints > explosionComponent.Props.startWickHitPointsPercent)
                return;

            detonation = new Detonate(this);
            detonation.StartWick(dinfo.Instigator);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Deep.Look(ref TankA, "tankA");
            Scribe_Deep.Look(ref TankB, "tankB");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                TankA ??= new TankState();
                TankB ??= new TankState();

                // Old migration code for backwards compatibility
                Scribe_Values.Look(ref oldTankAStorage, "tankAStorage", 0f);
                Scribe_Values.Look(ref oldTankBStorage, "tankBStorage", 0f);
                Scribe_Values.Look(ref oldTankAContent, "tankAContent", StoredType.Empty);
                Scribe_Values.Look(ref oldTankBContent, "tankBContent", StoredType.Empty);
                Scribe_Values.Look(ref oldTankALocked, "tankALocked", false);
                Scribe_Values.Look(ref oldTankBLocked, "tankBLocked", false);
                Scribe_Values.Look(ref oldTankAContamination, "tankAContaminated", false);
                Scribe_Values.Look(ref oldTankBContamination, "tankBContaminated", false);
                Scribe_Values.Look(ref oldTankAWaterQuality, "tankAWaterQuality", TankState.WaterState.Untreated);
                Scribe_Values.Look(ref oldTankBWaterQuality, "tankBWaterQuality", TankState.WaterState.Untreated);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                TankA!.isLocked = oldTankALocked;
                TankB!.isLocked = oldTankBLocked;

                TankA.isContaminated = oldTankAContamination;
                TankB.isContaminated = oldTankBContamination;

                if (oldTankAContent != StoredType.Empty)
                    TankA.Content = ConvertOldStoredType(oldTankAContent);

                if (oldTankBContent != StoredType.Empty)
                    TankB.Content = ConvertOldStoredType(oldTankBContent);

                if (oldTankAStorage > 0f)
                    TankA.Storage = oldTankAStorage;

                if (oldTankBStorage > 0f)
                    TankB.Storage = oldTankBStorage;

                if (oldTankAWaterQuality != TankState.WaterState.Untreated)
                    TankA.waterQuality = oldTankAWaterQuality;

                if (oldTankBWaterQuality != TankState.WaterState.Untreated)
                    TankB.waterQuality = oldTankBWaterQuality;
            }
        }

        public enum StoredType
        {
            Empty,      // 0
            Water,      // 1
            Sewage,     // 2
            Oil,        // 3
            Deepchem,   // 4
            Helixien,   // 5
            Scarlet     // 6
        }

        private static StoredTypeDef? ConvertOldStoredType(StoredType oldType)
        {
            return oldType switch
            {
                StoredType.Empty => null,
                StoredType.Water => CachedDefs.Water,
                StoredType.Sewage => CachedDefs.Sewage,
                StoredType.Oil => CachedDefs.Oil,
                StoredType.Deepchem => CachedDefs.Deepchem,
                StoredType.Helixien => CachedDefs.Helixien,
                StoredType.Scarlet => CachedDefs.Scarlet,
                _ => null
            };
        }

        private float oldTankAStorage;
        private float oldTankBStorage;
        private StoredType oldTankAContent;
        private StoredType oldTankBContent;
        private bool oldTankAContamination;
        private bool oldTankBContamination;
        private TankState.WaterState oldTankAWaterQuality;
        private TankState.WaterState oldTankBWaterQuality;
        private bool oldTankALocked;
        private bool oldTankBLocked;
    }
}
