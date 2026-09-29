using DubsBadHygiene;
using System;
using UnityEngine;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class DubsBadHygieneIntegration : LiquidIntegrationSingle<PlumbingNet, CompWaterStorage>
    {
        public DubsBadHygieneIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) {  }

        protected override StoredType LiquidTypeX => StoredType.Water;

        protected override void FindAdjacentNetworks() => AdjacentXNets = ShuttleWaterSearch.CheckCellsAroundShuttle(Shuttle);

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
    }
}
