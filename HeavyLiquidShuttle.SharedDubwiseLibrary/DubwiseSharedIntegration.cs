using DubsBadHygiene;
using Rimefeller;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class DubwiseSharedIntegration : LiquidIntegrationDouble<PlumbingNet, CompWaterStorage, PipelineNet, CompStorageTank>
    {
        public DubwiseSharedIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) => shuttle.OilSpillIntegration += StartOilSpill;
        protected override StoredType LiquidTypeX => StoredType.Water;
        protected override StoredType LiquidTypeY => StoredType.Oil;

        protected override void OnCleanup() => Shuttle.OilSpillIntegration -= StartOilSpill;

        protected override void FindAdjacentNetworksDouble(
            out HashSet<PlumbingNet> AdjacentXNets,
            out HashSet<PipelineNet> AdjacentYNets)

            => ShuttleSearch.CheckCellsAroundShuttle(Shuttle,
                out AdjacentXNets,
                out AdjacentYNets);

        protected override void FindValidStoragesX(
            out CompWaterStorage? validSupplyStorage,
            out CompWaterStorage? validReceiveStorage)
        {

            SupplyStorageCandidatesX.Clear();
            ReceiveStorageCandidatesX.Clear();

            foreach (PlumbingNet net in AdjacentXNets)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
                {
                    // Storages here are not marked for transfer and are valid receivers of the Shuttle
                    if (storage.space > 0f && !storage.DrainTank)
                        ReceiveStorageCandidatesX.Add(storage);

                    // Storages here are marked for transfer and are valid suppliers to the Shuttle
                    if (storage.WaterStorage > 0f && storage.DrainTank)
                        SupplyStorageCandidatesX.Add(storage);
                }
            }

            StorageSelectX(
                out validSupplyStorage,
                out validReceiveStorage);
        }

        protected override void FindValidStoragesY(
            out CompStorageTank? validSupplyStorage,
            out CompStorageTank? validReceiveStorage)
        {

            SupplyStorageCandidatesY.Clear();
            ReceiveStorageCandidatesY.Clear();

            foreach (PipelineNet net in AdjacentYNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    // Storages here are not marked for transfer and are valid receivers of the Shuttle
                    if (storage.space > 0f && !storage.DrainTank)
                        ReceiveStorageCandidatesY.Add(storage);

                    // Storages here are marked for transfer and are valid suppliers to the Shuttle
                    if (storage.Storage > 0f && storage.DrainTank)
                        SupplyStorageCandidatesY.Add(storage);
                }
            }

            StorageSelectY(
                out validSupplyStorage,
                out validReceiveStorage);
        }

        protected override float ModifyStorageX(CompWaterStorage storage, TankState tank, float amount, bool addTo)
        {
            float transferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.space);
                storage.WaterStorage += transferred;

                if (transferred > 0f && storage.WaterQuality.ToString() != tank.WaterQuality.ToString())
                    storage.WaterQuality = Enum.Parse<ContaminationLevel>(tank.WaterQuality.ToString());

                return transferred;
            }

            transferred = Mathf.Min(amount, storage.WaterStorage);
            storage.WaterStorage -= transferred;

            if (transferred <= 0f)
                return transferred;

            if (tank.IsContaminated)
                tank.WaterQuality = WaterState.Contaminated;
            else if (storage.WaterQuality.ToString() != tank.WaterQuality.ToString())
                tank.WaterQuality = Enum.Parse<WaterState>(storage.WaterQuality.ToString());

            return transferred;
        }

        protected override float ModifyStorageY(CompStorageTank storage, TankState tank, float amount, bool addTo)
        {
            float transferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.space);
                storage.Storage += transferred;
                return transferred;
            }

            transferred = Mathf.Min(amount, (float)storage.Storage);
            storage.Storage -= transferred;

            if (transferred > 0f && !tank.IsContaminated)
                tank.IsContaminated = true;

            return transferred;
        }

        private void StartOilSpill(float spilledAmount)
        {

            if (!Shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = Shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(Shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(Shuttle.OilConnectionAt, current + spilledAmount);
        }
    }
}
