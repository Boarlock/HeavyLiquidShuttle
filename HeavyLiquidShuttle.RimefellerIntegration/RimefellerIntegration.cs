using Rimefeller;
using UnityEngine;
using Verse;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class RimefellerIntegration : LiquidIntegrationSingle<PipelineNet, CompStorageTank>
    {
        public RimefellerIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) => Shuttle.OilSpillIntegration += StartOilSpill;
        protected override void OnCleanup() => Shuttle.OilSpillIntegration -= StartOilSpill;
        protected override StoredTypeDef LiquidTypeX => CachedDefs.Oil;
        protected override void FindAdjacentNetworks() => AdjacentXNets = ShuttleOilSearch.CheckCellsAroundShuttle(Shuttle);

        protected override void FindValidStoragesX(
            out CompStorageTank? validSupplyStorage,
            out CompStorageTank? validReceiveStorage)
        {

            SupplyStorageCandidatesX.Clear();
            ReceiveStorageCandidatesX.Clear();

            foreach (PipelineNet net in AdjacentXNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    // Storages here are not marked for transfer and are valid receivers of the Shuttle
                    if (storage.space > 0f && !storage.DrainTank)
                        ReceiveStorageCandidatesX.Add(storage);

                    // Storages here are marked for transfer and are valid suppliers to the Shuttle
                    if (storage.Storage > 0f && storage.DrainTank)
                        SupplyStorageCandidatesX.Add(storage);
                }
            }

            StorageSelectX(
                out validSupplyStorage,
                out validReceiveStorage);
        }

        protected override float ModifyStorageX(CompStorageTank storage, TankState tank, float amount, bool addTo)
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

            if (transferred > 0f && !tank.isContaminated)
                tank.isContaminated = true;

            return transferred;
        }

        private void StartOilSpill(float spilledAmount, IntVec3 spillCell)
        {
            if (!spillCell.IsValid)
                return;

            MapComponent_Rimefeller comp = Shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(spillCell);

            comp.OilSpillGrid.SetAt(spillCell, current + spilledAmount);
        }
    }
}
