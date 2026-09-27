using DubsBadHygiene;
using UnityEngine;

namespace HeavyLiquidShuttleMod
{
    public class DubsBadHygieneIntegration : LiquidIntegration<PlumbingNet, CompWaterStorage>
    {
        public DubsBadHygieneIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) {  }

        protected override StoredType LiquidType => StoredType.Water;

        protected override void FindAdjacentNetworks()
        {
            AdjacentXNets = ShuttleWaterSearch.CheckCellsAroundShuttle(Shuttle);
        }

        protected override float ModifyStorage(CompWaterStorage storage, float amount, bool addTo)
        {
            float transferred = 0f;

            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.space);
                storage.WaterStorage += transferred;
                return transferred;
            }

            transferred = Mathf.Min(amount, storage.WaterStorage);
            storage.WaterStorage -= transferred;
            return transferred;
        }

        protected override void FindValidStoragesX(
            out CompWaterStorage? validSupplyStorage,
            out CompWaterStorage? validReceivingStorage)
        {

            SupplyStorageCandidatesX.Clear();
            ReceiveStorageCandidatesX.Clear();

            validSupplyStorage = null;
            validReceivingStorage = null;

            int lastIndex;
            int nextIndex;

            foreach (PlumbingNet net in AdjacentXNets)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                        SupplyStorageCandidatesX.Add(storage);

                    if (storage.WaterStorage > 0f && storage.DrainTank)
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
    }
}
