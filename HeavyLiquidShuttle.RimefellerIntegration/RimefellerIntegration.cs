using Rimefeller;
using UnityEngine;

namespace HeavyLiquidShuttleMod
{
    public class RimefellerIntegration : LiquidIntegration<PipelineNet, CompStorageTank>
    {
        public RimefellerIntegration(HeavyLiquidShuttle shuttle) : base(shuttle)
            => Shuttle.OilSpillIntegration += StartOilSpill;

        protected override void OnCleanup()
            => Shuttle.OilSpillIntegration -= StartOilSpill;

        protected override StoredType LiquidType => StoredType.Oil;

        protected override void FindAdjacentNetworks()
        {
            AdjacentXNets = ShuttleOilSearch.CheckCellsAroundShuttle(Shuttle);
        }

        protected override float ModifyStorage(CompStorageTank storage, float amount, bool addTo)
        {
            float transferred = 0f;

            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.space);
                storage.Storage += transferred;
                return transferred;
            }

            transferred = Mathf.Min(amount, (float)storage.Storage);
            storage.Storage -= transferred;
            return transferred;
        }

        protected override void FindValidStoragesX(
            out CompStorageTank? validSupplyStorage,
            out CompStorageTank? validReceivingStorage)
        {

            SupplyStorageCandidatesX.Clear();
            ReceiveStorageCandidatesX.Clear();

            validSupplyStorage = null;
            validReceivingStorage = null;

            int lastIndex;
            int nextIndex;

            foreach (PipelineNet net in AdjacentXNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                        SupplyStorageCandidatesX.Add(storage);

                    if (storage.Storage > 0f && storage.DrainTank)
                        ReceiveStorageCandidatesX.Add(storage);
                }
            }

            // If valid supply storages exist
            if (SupplyStorageCandidatesX.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (LastSuppliedX == null)
                {
                    LastSuppliedX = SupplyStorageCandidatesX[0];
                }
                else
                {
                    lastIndex = SupplyStorageCandidatesX.IndexOf(LastSuppliedX);

                    // If IndexOf is -1 then LastSupplied isn't in the current list
                    if (lastIndex < 0)
                    {
                        LastSuppliedX = SupplyStorageCandidatesX[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= SupplyStorageCandidatesX.Count)
                            nextIndex = 0;

                        LastSuppliedX = SupplyStorageCandidatesX[nextIndex];
                    }
                }
                validSupplyStorage = LastSuppliedX;
            }

            // If valid receive storages exist
            if (ReceiveStorageCandidatesX.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (LastReceivedX == null)
                {
                    LastReceivedX = ReceiveStorageCandidatesX[0];
                }
                else
                {
                    lastIndex = ReceiveStorageCandidatesX.IndexOf(LastReceivedX);

                    // If IndexOf is -1 then LastReceived isn't in the current list
                    if (lastIndex < 0)
                    {
                        LastReceivedX = ReceiveStorageCandidatesX[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= ReceiveStorageCandidatesX.Count)
                            nextIndex = 0;

                        LastReceivedX = ReceiveStorageCandidatesX[nextIndex];
                    }
                }
                validReceivingStorage = LastReceivedX;
            }
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
