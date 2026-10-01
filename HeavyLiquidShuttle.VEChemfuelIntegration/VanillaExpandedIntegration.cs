using PipeSystem;
using System;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    class VEResource
    {
        public StoredType Type;

        public HashSet<PipeNet> AdjacentNets = new HashSet<PipeNet>();
        public List<CompResourceStorage> SupplyCandidates = new List<CompResourceStorage>();
        public List<CompResourceStorage> ReceiveCandidates = new List<CompResourceStorage>();

        public CompResourceStorage? LastSupplied;
        public CompResourceStorage? LastReceived;
    }

    public class VanillaExpandedIntegration
    {
        private readonly HeavyLiquidShuttle Shuttle;
        private readonly List<VEResource> Resources = new List<VEResource>();

        public VanillaExpandedIntegration(HeavyLiquidShuttle shuttle)
        {
            Shuttle = shuttle;

            HeavyLiquidShuttleGameComp.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration += OnTransferTick;
            Shuttle.GizmoIntegration += AddGizmos;

            if (HeavyLiquidShuttleMod.VEChemfuelActive)
                Resources.Add(new VEResource { Type = StoredType.Deepchem });

            if (HeavyLiquidShuttleMod.VEHelixienActive)
                Resources.Add(new VEResource { Type = StoredType.Helixien });

            if (HeavyLiquidShuttleMod.VEScarletActive)
                Resources.Add(new VEResource { Type = StoredType.Scarlet });
        }

        private bool cleanedUp = false;
        private void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComp.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration -= OnTransferTick;
            Shuttle.GizmoIntegration -= AddGizmos;

            cleanedUp = true;
        }

        private static readonly FieldInfo MarkedForTransferField = typeof(PipeNet).GetField("markedForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AmountStoredField = typeof(CompResourceStorage).GetField("amountStored", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<CompResourceStorage> GetMarkedForTransfer(PipeNet net)
        {
            return (List<CompResourceStorage>)MarkedForTransferField.GetValue(net);
        }
        private void SetAmountStored(CompResourceStorage storage, float value)
        {
            AmountStoredField.SetValue(storage, value);
        }

        private void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            Shuttle.TankA.ReceiveAllowance = 1f;
            Shuttle.TankA.SupplyAllowance = 1f;
            Shuttle.TankB.ReceiveAllowance = 1f;
            Shuttle.TankB.SupplyAllowance = 1f;

            ShuttleVESearch.CheckCellsAroundShuttle(
                Shuttle, 
                out HashSet<PipeNet>? aNets, 
                out HashSet<PipeNet>? bNets,
                out HashSet<PipeNet>? cNets);

            foreach (VEResource resource in Resources)
            {
                resource.AdjacentNets.Clear();

                switch (resource.Type)
                {
                    case StoredType.Deepchem:
                        if (aNets != null)
                            resource.AdjacentNets.UnionWith(aNets);
                        break;

                    case StoredType.Helixien:
                        if (bNets != null)
                            resource.AdjacentNets.UnionWith(bNets);
                        break;

                    case StoredType.Scarlet:
                        if (cNets != null)
                            resource.AdjacentNets.UnionWith(cNets);
                        break;
                }

                if (resource.AdjacentNets.Count <= 0)
                {
                    resource.SupplyCandidates.Clear();
                    resource.ReceiveCandidates.Clear();

                    resource.LastSupplied = null;
                    resource.LastReceived = null;
                }
            }
        }

        private void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (Resources.Count <= 0)
                return;

            int i = 0;

            foreach (VEResource resource in Resources)
            {
                FindValidStorages(
                resource,
                out CompResourceStorage? validSupplyStorage,
                out CompResourceStorage? validReceiveStorage);

                TankState? tankSupply = Shuttle.GetTankForSupply(resource.Type);
                TankState? tankReceive = Shuttle.GetTankForReceive(resource.Type);

                if (tankSupply != null && validReceiveStorage != null && tankSupply.SupplyAllowance > 0f)
                    TryModify(resource, validReceiveStorage, tankSupply, true);

                if (tankReceive != null && validSupplyStorage != null && tankReceive.ReceiveAllowance > 0f)
                    TryModify(resource, validSupplyStorage, tankReceive, false);

                i++;
            }
        }

        private  void FindValidStorages(
            VEResource resource,
            out CompResourceStorage? validSupplyStorage,
            out CompResourceStorage? validReceivingStorage)
        {

            resource.SupplyCandidates.Clear();
            resource.ReceiveCandidates.Clear();

            foreach (PipeNet net in resource.AdjacentNets)
            {
                List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                // Storages here are not marked for transfer and are valid receivers of the Shuttle
                foreach (CompResourceStorage storage in net.storages)
                {
                    if (storage.AmountCanAccept > 0f && !storage.markedForTransfer)
                        resource.ReceiveCandidates.Add(storage);
                }

                // Storages here are marked for transfer and are valid suppliers to the Shuttle
                foreach (CompResourceStorage storage in sourceStorages)
                {
                    if (storage.AmountStored > 0f)
                        resource.SupplyCandidates.Add(storage);
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
            if (resource.SupplyCandidates.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (resource.LastSupplied == null)
                {
                    resource.LastSupplied = resource.SupplyCandidates[0];
                }
                else
                {
                    lastIndex = resource.SupplyCandidates.IndexOf(resource.LastSupplied!);

                    // If IndexOf is -1 then LastSupplied isn't in the current list
                    if (lastIndex < 0)
                    {
                        resource.LastSupplied = resource.SupplyCandidates[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= resource.SupplyCandidates.Count)
                            nextIndex = 0;

                        resource.LastSupplied = resource.SupplyCandidates[nextIndex];
                    }
                }
                validSupplyStorage = resource.LastSupplied;
            }

            // If valid receive storages exist
            if (resource.ReceiveCandidates.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (resource.LastReceived == null)
                {
                    resource.LastReceived = resource.ReceiveCandidates[0];
                }
                else
                {
                    lastIndex = resource.ReceiveCandidates.IndexOf(resource.LastReceived!);

                    // If IndexOf is -1 then LastReceived isn't in the current list
                    if (lastIndex < 0)
                    {
                        resource.LastReceived = resource.ReceiveCandidates[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= resource.ReceiveCandidates.Count)
                            nextIndex = 0;

                        resource.LastReceived = resource.ReceiveCandidates[nextIndex];
                    }
                }
                validReceiveStorage = resource.LastReceived;
            }
        }

        private void TryModify(VEResource resource, CompResourceStorage storage, TankState tank, bool addTo)
        {
            float amount; 
            float transferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                if (tank.TankStorage <= 0f)
                    return;

                if (!tank.TransferEnabled)
                    return;

                amount = Mathf.Min(tank.TankStorage, tank.SupplyAllowance, 1f);

                if (amount <= 0f)
                    return;

                transferred = ModifyStorage(resource,storage, tank, amount, true);
                tank.TankStorage -= transferred;
                tank.SupplyAllowance -= transferred;

                if (tank.TankStorage <= 0f)
                {
                    tank.TankStorage = 0f;
                    tank.Content = StoredType.Empty;
                    tank.TransferEnabled = false;
                }
            }
            // Pulling from storage is adding to shuttle tanks
            else
            {
                if (tank.TankStorage >= tank.TankCapacity)
                    return;

                amount = Mathf.Min(tank.TankCapacity - tank.TankStorage, tank.ReceiveAllowance, 1f);

                if (amount <= 0f)
                    return;

                transferred = ModifyStorage(resource,storage, tank, amount, false);
                tank.TankStorage += transferred;
                tank.ReceiveAllowance -= transferred;

                if (transferred > 0f)
                    tank.Content = resource.Type;
            }
            
            HandleHelixienTank(tank);
            MassPatch.NotifyLiquidMassChanged(Shuttle);
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

            if (transferred > 0f && !tank.IsContaminated && resource.Type == StoredType.Deepchem)
                tank.IsContaminated = true;

            return transferred;
        }

        private void HandleHelixienTank(TankState tank)
        {
            tank.TankExplosiveness = tank.GetHelixienState(out bool stateChanged);

            if (!stateChanged)
                return;

            ShuttleExplosion explosion = Shuttle.parent.TryGetComp<ShuttleExplosion>();

            if (explosion != null)
                explosion.UpdateExplosiveness();
        }

        private IEnumerable<Gizmo> AddGizmos()
        {
            foreach (VEResource resource in Resources)
            {
                if (resource.AdjacentNets.Count > 0)
                {
                    if (Shuttle.TankA.Content == resource.Type && Shuttle.TankA.TankStorage > 0f)
                        yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank A", resource.Type)!;

                    if (Shuttle.TankB.Content == resource.Type && Shuttle.TankB.TankStorage > 0f)
                        yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank B", resource.Type)!;
                }
            }
        }
    }
}
