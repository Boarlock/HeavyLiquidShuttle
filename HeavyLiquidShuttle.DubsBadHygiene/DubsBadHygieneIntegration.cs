using DubsBadHygiene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public class DubsBadHygieneIntegration : LiquidIntegrationSingle<PlumbingNet, CompWaterStorage>
    {
        public DubsBadHygieneIntegration(HeavyLiquidShuttle shuttle) : base(shuttle) => Shuttle.SewageSpillIntegration += StartSewageSpill;
        protected override void OnCleanup() => Shuttle.SewageSpillIntegration -= StartSewageSpill;

        protected override StoredTypeDef LiquidTypeX => CachedDefs.Water;
        private StoredTypeDef LiquidTypeY => CachedDefs.Sewage;

        protected override void FindAdjacentNetworks() { }

        protected List<CompSewageHandler> SupplyStorageCandidatesY = new List<CompSewageHandler>();
        protected List<CompSewageHandler> ReceiveStorageCandidatesY = new List<CompSewageHandler>();

        protected CompSewageHandler? LastSuppliedY;
        protected CompSewageHandler? LastReceivedY;

        protected override void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            Shuttle.TankA!.receiveAllowance = 1f;
            Shuttle.TankA.supplyAllowance = 1f;
            Shuttle.TankB!.receiveAllowance = 1f;
            Shuttle.TankB.supplyAllowance = 1f;

            AdjacentXNets = ShuttleWaterSearch.CheckCellsAroundShuttle(Shuttle);

            if (AdjacentXNets.Count <= 0)
            {
                SupplyStorageCandidatesX.Clear();
                ReceiveStorageCandidatesX.Clear();
                SupplyStorageCandidatesY.Clear();
                ReceiveStorageCandidatesY.Clear();

                LastSuppliedX = null;
                LastReceivedX = null;
                LastSuppliedY = null;
                LastReceivedY = null;

                return;
            }
        }

        protected override void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (AdjacentXNets.Count <= 0)
                return;

            CompWaterStorage? validWaterSupply = null;
            CompWaterStorage? validWaterReceive = null;

            CompSewageHandler? validSewageSupply = null;
            CompSewageHandler? validSewageReceive = null;

            TankState? tankSupply;
            TankState? tankReceive;

            if (AdjacentXNets.Count > 0)
            {
                FindValidStoragesX(
                out validWaterSupply,
                out validWaterReceive);

                FindValidStoragesY(
                out validSewageSupply,
                out validSewageReceive);
            }

            if (validWaterSupply != null || validWaterReceive != null)
            {
                tankSupply = Shuttle.GetTankForSupply(LiquidTypeX);
                tankReceive = Shuttle.GetTankForReceive(LiquidTypeX);

                // TryPush pushes from Tank Supply to Network Receive
                if (tankSupply != null && validWaterReceive != null && tankSupply.supplyAllowance > 0f)
                    TryPushX(validWaterReceive, tankSupply);

                // TryPull pulls from Network Supply to Tank Receive
                if (tankReceive != null && validWaterSupply != null && tankReceive.receiveAllowance > 0f)
                    TryPullX(validWaterSupply, tankReceive);
            }

            if (validSewageSupply != null || validSewageReceive != null)
            {
                tankSupply = Shuttle.GetTankForSupply(LiquidTypeY);
                tankReceive = Shuttle.GetTankForReceive(LiquidTypeY);

                // TryPush pushes from Tank Supply to Network Receive
                if (tankSupply != null && validSewageReceive != null && tankSupply.supplyAllowance > 0f)
                    TryPushY(validSewageReceive, tankSupply);

                // TryPull pulls from Network Supply to Tank Receive
                if (tankReceive != null && validSewageSupply != null && tankReceive.receiveAllowance > 0f)
                    TryPullY(validSewageSupply, tankReceive);
            }
        }

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

        private void FindValidStoragesY(
            out CompSewageHandler? validSupplyStorage,
            out CompSewageHandler? validReceiveStorage)
        {

            SupplyStorageCandidatesY.Clear();
            ReceiveStorageCandidatesY.Clear();

            foreach (PlumbingNet net in AdjacentXNets)
            {
                foreach (CompSewageHandler sewer in net.Sewers)
                {
                    CompProperties_SewageHandler sewerProps = (CompProperties_SewageHandler)sewer.props;

                    // Sewers here are valid receivers of the Shuttle
                    if (sewer.sewageBuffer < sewerProps.capacity)
                        ReceiveStorageCandidatesY.Add(sewer);

                    // Sewers here are valid suppliers to the Shuttle
                    if (sewer.sewageBuffer > 0f)
                        SupplyStorageCandidatesY.Add(sewer);
                }
            }

            StorageSelectY(
                out validSupplyStorage,
                out validReceiveStorage);
        }

        private void StorageSelectY(
            out CompSewageHandler? validSewageSupply,
            out CompSewageHandler? validSewageReceive)
        {

            validSewageSupply = null;
            validSewageReceive = null;

            int lastIndex;
            int nextIndex;

            // If valid supply storages exist
            if (SupplyStorageCandidatesY.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (LastSuppliedY == null)
                {
                    LastSuppliedY = SupplyStorageCandidatesY[0];
                }
                else
                {
                    lastIndex = SupplyStorageCandidatesY.IndexOf(LastSuppliedY);

                    // If IndexOf is -1 then LastSupplied isn't in the current list
                    if (lastIndex < 0)
                    {
                        LastSuppliedY = SupplyStorageCandidatesY[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= SupplyStorageCandidatesY.Count)
                            nextIndex = 0;

                        LastSuppliedY = SupplyStorageCandidatesY[nextIndex];
                    }
                }
                validSewageSupply = LastSuppliedY;
            }

            // If valid receive storages exist
            if (ReceiveStorageCandidatesY.Count > 0)
            {
                // If this list hasn't been set yet then get the first element
                if (LastReceivedY == null)
                {
                    LastReceivedY = ReceiveStorageCandidatesY[0];
                }
                else
                {
                    lastIndex = ReceiveStorageCandidatesY.IndexOf(LastReceivedY);

                    // If IndexOf is -1 then LastReceived isn't in the current list
                    if (lastIndex < 0)
                    {
                        LastReceivedY = ReceiveStorageCandidatesY[0];
                    }
                    else
                    {
                        nextIndex = lastIndex + 1;

                        // If next index exceeds length of list reset to 0
                        if (nextIndex >= ReceiveStorageCandidatesY.Count)
                            nextIndex = 0;

                        LastReceivedY = ReceiveStorageCandidatesY[nextIndex];
                    }
                }
                validSewageReceive = LastReceivedY;
            }
        }

        private void TryPushY(CompSewageHandler sewer, TankState tank)
        {
            if (tank.tankStorage <= 0f)
                return;

            if (!tank.transferEnabled)
                return;

            float amount = Mathf.Min(tank.tankStorage, tank.supplyAllowance, 1f);

            if (amount <= 0f)
                return;

            // Amount to ask network to receive
            float unitsRequested = TankState.LitersToUnits(amount, CachedDefs.Sewage);

            float unitsTransferred = ModifyStorageY(sewer, tank, unitsRequested, true);

            // Calculate back what the net received
            float litersTransferred = TankState.UnitsToLiters(unitsTransferred, CachedDefs.Sewage);

            tank.tankStorage -= litersTransferred;
            tank.supplyAllowance -= litersTransferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);

            if (tank.tankStorage <= 0f)
            {
                tank.tankStorage = 0f;
                tank.content = null;
                tank.transferEnabled = false;
            }
        }

        private void TryPullY(CompSewageHandler sewer, TankState tank)
        {
            if (tank.tankStorage >= tank.props.physicalCapacity)
                return;

            float amount = Mathf.Min(tank.props.physicalCapacity - tank.tankStorage, tank.receiveAllowance, 1f);

            if (amount <= 0f)
                return;

            // Amount to ask network to supply
            float unitsRequested = TankState.LitersToUnits(amount, CachedDefs.Sewage);

            float unitsTransferred = ModifyStorageY(sewer, tank, unitsRequested, false);

            if (unitsTransferred > 0f)
                tank.content = LiquidTypeY;

            // Calculate back what the net gave us
            float litersTransferred = TankState.UnitsToLiters(unitsTransferred, CachedDefs.Sewage);

            tank.tankStorage += litersTransferred;
            tank.receiveAllowance -= litersTransferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);
        }

        protected override float ModifyStorageX(CompWaterStorage storage, TankState tank, float amount, bool addTo)
        {
            float transferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                transferred = Mathf.Min(amount, storage.space);
                storage.WaterStorage += transferred;

                if (transferred > 0f && storage.WaterQuality.ToString() != tank.waterQuality.ToString())
                    storage.WaterQuality = Enum.Parse<ContaminationLevel>(tank.waterQuality.ToString());

                return transferred;
            }

            transferred = Mathf.Min(amount, storage.WaterStorage);
            storage.WaterStorage -= transferred;

            if (transferred <= 0f)
                return transferred;

            if (tank.isContaminated)
                tank.waterQuality = WaterState.Contaminated;
            else if (storage.WaterQuality.ToString() != tank.waterQuality.ToString())
                tank.waterQuality = Enum.Parse<WaterState>(storage.WaterQuality.ToString());

            return transferred;
        }

        private float ModifyStorageY(CompSewageHandler sewer, TankState tank, float amount, bool addTo)
        {
            CompProperties_SewageHandler sewerProps = (CompProperties_SewageHandler)sewer.props;
            float transferred;

            // Adding to storage is subtracting from shuttle tanks
            if (addTo)
            {
                transferred = Mathf.Min(amount, sewerProps.capacity - sewer.sewageBuffer);
                sewer.sewageBuffer += transferred;
                return transferred;
            }

            transferred = Mathf.Min(amount, sewer.sewageBuffer);
            sewer.sewageBuffer -= transferred;

            if (transferred <= 0f)
                return transferred;

            if (!tank.isContaminated)
                tank.isContaminated = true;

            return transferred;
        }

        protected override IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count <= 0)
                yield break;

            if (Shuttle.TankA!.content == LiquidTypeX && Shuttle.TankA.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, true, LiquidTypeX)!;

            else if (Shuttle.TankA.content == LiquidTypeY && Shuttle.TankA.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, true, LiquidTypeY)!;

            if (Shuttle.TankB!.content == LiquidTypeX && Shuttle.TankB.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, false, LiquidTypeX)!;

            else if (Shuttle.TankB.content == LiquidTypeY && Shuttle.TankB.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, false, LiquidTypeY)!;

        }

        private void StartSewageSpill(float spilledAmount, IntVec3 spillCell)
        {
            if (!spillCell.IsValid)
                return;

            MapComponent_Hygiene comp = Shuttle.parent.Map.GetComponent<MapComponent_Hygiene>();

            comp.SewageGrid.AddAt(spillCell, spilledAmount);
        }
    }
}
