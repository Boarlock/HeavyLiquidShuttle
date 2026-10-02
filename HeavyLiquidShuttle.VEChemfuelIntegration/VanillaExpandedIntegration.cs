using PipeSystem;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    class VEResource
    {
        public StoredTypeDef? def;

        public HashSet<PipeNet> adjacentNets = new HashSet<PipeNet>();
        public List<CompResourceStorage> supplyCandidates = new List<CompResourceStorage>();
        public List<CompResourceStorage> receiveCandidates = new List<CompResourceStorage>();

        public CompResourceStorage? lastSupplied;
        public CompResourceStorage? lastReceived;
    }

    public class VanillaExpandedIntegration
    {
        private readonly HeavyLiquidShuttle shuttle;
        private readonly List<VEResource> resources = new List<VEResource>();

        public VanillaExpandedIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            HeavyLiquidShuttleGameComp.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration += OnTransferTick;
            this.shuttle.GizmoIntegration += AddGizmos;

            if (HeavyLiquidShuttleMod.VEChemfuelActive)
            {
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Deepchem") });
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Chemfuel") });
            }

            if (HeavyLiquidShuttleMod.VEHelixienActive)
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Helixien") });

            if (HeavyLiquidShuttleMod.VEScarletActive)
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Scarlet") });

            if (HeavyLiquidShuttleMod.VEGravshipActive)
            {
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Oxygen") });
                resources.Add(new VEResource { def = DefDatabase<StoredTypeDef>.GetNamed("Astrofuel") });
            }
        }

        private bool cleanedUp = false;
        private void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComp.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration -= OnTransferTick;
            shuttle.GizmoIntegration -= AddGizmos;

            cleanedUp = true;
        }

        private static readonly FieldInfo markedForTransferField = typeof(PipeNet).GetField("markedForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo amountStoredField = typeof(CompResourceStorage).GetField("amountStored", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<CompResourceStorage> GetMarkedForTransfer(PipeNet net)
        {
            return (List<CompResourceStorage>)markedForTransferField.GetValue(net);
        }
        private void SetAmountStored(CompResourceStorage storage, float value)
        {
            amountStoredField.SetValue(storage, value);
        }

        private void OnShuttleTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            shuttle.TankA.receiveAllowance = 1f;
            shuttle.TankA.supplyAllowance = 1f;
            shuttle.TankB.receiveAllowance = 1f;
            shuttle.TankB.supplyAllowance = 1f;

            Dictionary<StoredTypeDef, HashSet<PipeNet>> nets =
                ShuttleVESearch.CheckCellsAroundShuttle(shuttle);

            foreach (VEResource resource in resources)
            {
                resource.adjacentNets.Clear();

                if (nets.TryGetValue(resource.def!, out HashSet<PipeNet>? adjacentNets))
                    resource.adjacentNets.UnionWith(adjacentNets);

                if (resource.adjacentNets.Count <= 0)
                {
                    resource.supplyCandidates.Clear();
                    resource.receiveCandidates.Clear();

                    resource.lastSupplied = null;
                    resource.lastReceived = null;
                }
            }
        }

        private void OnTransferTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (resources.Count <= 0)
                return;

            int i = 0;

            foreach (VEResource resource in resources)
            {
                FindValidStorages(
                resource,
                out CompResourceStorage? validSupplyStorage,
                out CompResourceStorage? validReceiveStorage);

                TankState? tankSupply = shuttle.GetTankForSupply(resource.def!);
                TankState? tankReceive = shuttle.GetTankForReceive(resource.def!);

                if (tankSupply != null && validReceiveStorage != null && tankSupply.supplyAllowance > 0f)
                    TryModify(resource, validReceiveStorage, tankSupply, true);

                if (tankReceive != null && validSupplyStorage != null && tankReceive.receiveAllowance > 0f)
                    TryModify(resource, validSupplyStorage, tankReceive, false);

                i++;
            }
        }

        private void FindValidStorages(
            VEResource resource,
            out CompResourceStorage? validSupplyStorage,
            out CompResourceStorage? validReceivingStorage)
        {

            resource.supplyCandidates.Clear();
            resource.receiveCandidates.Clear();

            foreach (PipeNet net in resource.adjacentNets)
            {
                List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                // Storages here are not marked for transfer and are valid receivers of the shuttle
                foreach (CompResourceStorage storage in net.storages)
                {
                    if (storage.AmountCanAccept > 0f && !storage.markedForTransfer)
                        resource.receiveCandidates.Add(storage);
                }

                // Storages here are marked for transfer and are valid suppliers to the shuttle
                foreach (CompResourceStorage storage in sourceStorages)
                {
                    if (storage.AmountStored > 0f)
                        resource.supplyCandidates.Add(storage);
                }
            }

            StorageSelect(
                resource,
                out validSupplyStorage,
                out validReceivingStorage);
        }

        private void StorageSelect(
            VEResource resource,
            out CompResourceStorage? validSupplyStorage,
            out CompResourceStorage? validReceiveStorage)
        {

            validSupplyStorage = null;
            validReceiveStorage = null;

            int lastIndex;
            int nextIndex;

            // If valid supply storages exist
            if (resource.supplyCandidates.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (resource.lastSupplied == null)
                {
                    resource.lastSupplied = resource.supplyCandidates[0];
                }
                else
                {
                    lastIndex = resource.supplyCandidates.IndexOf(resource.lastSupplied!);

                    // If IndexOf is -1 then lastSupplied isn't in the current list
                    if (lastIndex < 0)
                    {
                        resource.lastSupplied = resource.supplyCandidates[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= resource.supplyCandidates.Count)
                            nextIndex = 0;

                        resource.lastSupplied = resource.supplyCandidates[nextIndex];
                    }
                }
                validSupplyStorage = resource.lastSupplied;
            }

            // If valid receive storages exist
            if (resource.receiveCandidates.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (resource.lastReceived == null)
                {
                    resource.lastReceived = resource.receiveCandidates[0];
                }
                else
                {
                    lastIndex = resource.receiveCandidates.IndexOf(resource.lastReceived!);

                    // If IndexOf is -1 then lastReceived isn't in the current list
                    if (lastIndex < 0)
                    {
                        resource.lastReceived = resource.receiveCandidates[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= resource.receiveCandidates.Count)
                            nextIndex = 0;

                        resource.lastReceived = resource.receiveCandidates[nextIndex];
                    }
                }
                validReceiveStorage = resource.lastReceived;
            }
        }

        private void TryModify(VEResource resource, CompResourceStorage storage, TankState tank, bool addTo)
        {
            float amount; 
            float unitsTransferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                if (tank.tankStorage <= 0f)
                    return;

                if (!tank.transferEnabled)
                    return;

                amount = Mathf.Min(tank.tankStorage, tank.supplyAllowance, 1f);

                if (amount <= 0f)
                    return;

                // Amount to ask network to receive
                float unitsRequested = TankState.LitersToUnits(amount, resource.def!);

                unitsTransferred = ModifyStorage(resource,storage, tank, unitsRequested, true);

                // Calculate back what the net received
                float litersTransferred = TankState.UnitsToLiters(unitsTransferred, resource.def!);

                tank.tankStorage -= litersTransferred;
                tank.supplyAllowance -= litersTransferred;

                if (tank.tankStorage <= 0f)
                {
                    tank.tankStorage = 0f;
                    tank.content = null;
                    tank.transferEnabled = false;
                }
            }
            // Pulling from storage is adding to shuttle tanks
            else
            {
                if (tank.tankStorage >= tank.props.physicalCapacity)
                    return;

                amount = Mathf.Min(tank.props.physicalCapacity - tank.tankStorage, tank.receiveAllowance, 1f);

                if (amount <= 0f)
                    return;

                // Amount to ask network to receive
                float unitsRequested = TankState.LitersToUnits(amount, resource.def!);

                unitsTransferred = ModifyStorage(resource,storage, tank, unitsRequested, false);

                if (unitsTransferred > 0f)
                    tank.content = resource.def;

                // Calculate back what the net received
                float litersTransferred = TankState.UnitsToLiters(unitsTransferred, resource.def!);

                tank.tankStorage += litersTransferred;
                tank.receiveAllowance -= litersTransferred;
            }

            MassPatch.NotifyLiquidMassChanged(shuttle);

            if (resource.def == CachedDefs.Scarlet)
                return;
            
            ShuttleExplosion comp = shuttle.parent.TryGetComp<ShuttleExplosion>();

            if (comp != null)
                comp.UpdateExplosiveness();
            
        }

        private float ModifyStorage(VEResource resource, CompResourceStorage storage, TankState tank, float amount, bool addTo)
        {
            float transferred;
             
            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.AmountCanAccept);
                SetAmountStored(storage, storage.AmountStored + transferred);
                return transferred;
            }

            transferred = Mathf.Min(amount, storage.AmountStored);
            SetAmountStored(storage, storage.AmountStored - transferred);

            if (transferred > 0f && 
                !tank.isContaminated && 
                resource.def == CachedDefs.Deepchem ||
                resource.def == CachedDefs.Chemfuel ||
                resource.def == CachedDefs.Astrofuel)
                tank.isContaminated = true;

            return transferred;
        }

        private IEnumerable<Gizmo> AddGizmos()
        {
            foreach (VEResource resource in resources)
            {
                if (resource.adjacentNets.Count > 0)
                {
                    if (shuttle.TankA.content == resource.def && shuttle.TankA.tankStorage > 0f)
                        yield return HeavyLiquidShuttle.CreateDischargeGizmo(shuttle, true, resource.def!);

                    if (shuttle.TankB.content == resource.def && shuttle.TankB.tankStorage > 0f)
                        yield return HeavyLiquidShuttle.CreateDischargeGizmo(shuttle, false, resource.def!);
                }
            }
        }
    }
}
