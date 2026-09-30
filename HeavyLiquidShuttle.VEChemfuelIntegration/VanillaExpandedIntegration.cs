using PipeSystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class VanillaExpandedIntegration : LiquidIntegrationDouble<PipeNet, CompResourceStorage, PipeNet, CompResourceStorage>
    {
        public VanillaExpandedIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) { }
        protected override StoredType LiquidTypeX => StoredType.Deepchem;
        protected override StoredType LiquidTypeY => StoredType.Helixien;
        protected override void FindAdjacentNetworks() { }

        // Static fields gathered through reflection for VE's markedForTransfer/amountStored fields and helpers get/set them
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

        protected override void FindAdjacentNetworksDouble(
            out HashSet<PipeNet> AdjacentXNets,
            out HashSet<PipeNet> AdjacentYNets)

            => ShuttleVESearch.CheckCellsAroundShuttle(Shuttle,
                out AdjacentXNets,
                out AdjacentYNets);

        protected override void FindValidStoragesX(
            out CompResourceStorage? validSupplyStorage,
            out CompResourceStorage? validReceivingStorage)
        {

            SupplyStorageCandidatesX.Clear();
            ReceiveStorageCandidatesX.Clear();

            foreach (PipeNet net in AdjacentXNets)
            {
                List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                // Storages here are not marked for transfer and are valid receivers of the Shuttle
                foreach (CompResourceStorage storage in net.storages)
                {
                    if (storage.AmountCanAccept > 0f && !storage.markedForTransfer)
                        ReceiveStorageCandidatesX.Add(storage);
                }

                // Storages here are marked for transfer and are valid suppliers to the Shuttle
                foreach (CompResourceStorage storage in sourceStorages)
                {
                    if (storage.AmountStored > 0f)
                        SupplyStorageCandidatesX.Add(storage);
                }
            }

            StorageSelectX(
                out validSupplyStorage,
                out validReceivingStorage);
        }

        protected override void FindValidStoragesY(
            out CompResourceStorage? validSupplyStorage,
            out CompResourceStorage? validReceivingStorage)
        {

            SupplyStorageCandidatesY.Clear();
            ReceiveStorageCandidatesY.Clear();

            foreach (PipeNet net in AdjacentYNets)
            {
                List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                // Storages here are not marked for transfer and are valid receivers of the Shuttle
                foreach (CompResourceStorage storage in net.storages)
                {
                    if (storage.AmountCanAccept > 0f && !storage.markedForTransfer)
                        ReceiveStorageCandidatesY.Add(storage);
                }

                // Storages here are marked for transfer and are valid suppliers to the Shuttle
                foreach (CompResourceStorage storage in sourceStorages)
                {
                    if (storage.AmountStored > 0f)
                        SupplyStorageCandidatesY.Add(storage);
                }
            }

            StorageSelectY(
                out validSupplyStorage,
                out validReceivingStorage);
        }

        protected override float ModifyStorageX(CompResourceStorage storage, TankState tank, float amount, bool addTo)
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

            if (transferred > 0f && !tank.IsContaminated)
                tank.IsContaminated = true;

            return transferred;
        }

        protected override float ModifyStorageY(CompResourceStorage storage, TankState _, float amount, bool addTo)
        {
            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                float transferred = Mathf.Min(amount, storage.AmountCanAccept);
                SetAmountStored(storage, storage.AmountStored + transferred);
                return transferred;
            }

            float removed = Mathf.Min(amount, storage.AmountStored);
            SetAmountStored(storage, storage.AmountStored - removed);
            return removed;
        }

        protected override void ExplosiveCompSet()
        {
            ShuttleExplosion c = Shuttle.parent.TryGetComp<ShuttleExplosion>();

            if (c != null)
                c.ExplosiveCompSet();
        }
    }
}
