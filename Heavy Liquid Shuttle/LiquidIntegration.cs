using RimWorld;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static HeavyLiquidShuttleMod.TankState;

namespace HeavyLiquidShuttleMod
{
    public abstract class LiquidIntegrationSingle<TNetwork1, TStorage1>
        where TNetwork1 : class
        where TStorage1 : class
    {
        protected readonly HeavyLiquidShuttle Shuttle;

        public LiquidIntegrationSingle(HeavyLiquidShuttle shuttle)
        {

            Shuttle = shuttle;

            HeavyLiquidShuttleGameComp.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration += OnTransferTick;
            Shuttle.GizmoIntegration += AddGizmos;
        }

        protected bool cleanedUp = false;
        protected void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComp.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComp.TickIntegration -= OnTransferTick;
            Shuttle.GizmoIntegration -= AddGizmos;

            OnCleanup();

            cleanedUp = true;
        }

        protected virtual void OnCleanup() { }

        protected virtual StoredTypeDef? LiquidTypeX { get; }
        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected List<TStorage1> SupplyStorageCandidatesX = new List<TStorage1>();
        protected List<TStorage1> ReceiveStorageCandidatesX = new List<TStorage1>();
        protected TStorage1? LastSuppliedX;
        protected TStorage1? LastReceivedX;

        protected abstract void FindAdjacentNetworks();
        protected abstract void FindValidStoragesX(
            out TStorage1? validSupplyStorage, 
            out TStorage1? validReceiveStorage);
        protected abstract float ModifyStorageX(TStorage1 storage, TankState tank, float amount, bool addTo);

        protected virtual void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            Shuttle.TankA.receiveAllowance = 1f;
            Shuttle.TankA.supplyAllowance = 1f;
            Shuttle.TankB.receiveAllowance = 1f;
            Shuttle.TankB.supplyAllowance = 1f;

            FindAdjacentNetworks();

            if (AdjacentXNets.Count <= 0)
            {
                SupplyStorageCandidatesX.Clear();
                ReceiveStorageCandidatesX.Clear();

                LastSuppliedX = null;
                LastReceivedX = null;

                return;
            }
        }

        protected virtual void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (AdjacentXNets.Count <= 0)
                return;

            FindValidStoragesX(
                out TStorage1? validSupplyStorage,
                out TStorage1? validReceiveStorage);

            TankState? tankSupply = Shuttle.GetTankForSupply(LiquidTypeX!);
            TankState? tankReceive = Shuttle.GetTankForReceive(LiquidTypeX!);


            // TryPush pushes from Tank Supply to Network Receive
            if (tankSupply != null && validReceiveStorage != null && tankSupply.supplyAllowance > 0f)
                TryPushX(validReceiveStorage, tankSupply);

            // TryPull pulls from Network Supply to Tank Receive
            if (tankReceive != null && validSupplyStorage != null && tankReceive.receiveAllowance > 0f)
                TryPullX(validSupplyStorage, tankReceive);

        }

        protected void StorageSelectX(
            out TStorage1? validSupplyStorage,
            out TStorage1? validReceiveStorage)
        {

            validSupplyStorage = null;
            validReceiveStorage = null;

            int lastIndex;
            int nextIndex;

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
                validReceiveStorage = LastReceivedX;
            }
        }

        protected void TryPushX(TStorage1 storage, TankState tank)
        {
            if (tank.tankStorage <= 0f)
                return;

            if (!tank.transferEnabled)
                return;

            float amount = Mathf.Min(tank.tankStorage, tank.supplyAllowance, 1f);

            if (amount <= 0f)
                return;

            // Amount to ask network to receive
            float unitsRequested = TankState.LitersToUnits(amount, LiquidTypeX!);

            float unitsTransferred = ModifyStorageX(storage, tank, unitsRequested, true);

            // Calculate back what the net received
            float litersTransferred = TankState.UnitsToLiters(unitsTransferred, LiquidTypeX!);

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

        protected void TryPullX(TStorage1 storage, TankState tank)
        {
            if (tank.tankStorage >= tank.props.physicalCapacity)
                return;

            float amount = Mathf.Min(tank.props.physicalCapacity - tank.tankStorage, tank.receiveAllowance, 1f);

            if (amount <= 0f)
                return;

            // Amount to ask network to receive
            float unitsRequested = TankState.LitersToUnits(amount, LiquidTypeX!);

            float unitsTransferred = ModifyStorageX(storage, tank, unitsRequested, false);

            if (unitsTransferred > 0f)
                tank.content = LiquidTypeX;

            // Calculate back what the net gave us
            float litersTransferred = TankState.UnitsToLiters(unitsTransferred, LiquidTypeX!);

            tank.tankStorage += litersTransferred;
            tank.receiveAllowance -= litersTransferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);
        }

        protected virtual IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count <= 0)
                yield break;

            if (Shuttle.TankA.content == LiquidTypeX && Shuttle.TankA.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, true, LiquidTypeX!);

            if (Shuttle.TankB.content == LiquidTypeX && Shuttle.TankB.tankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, false, LiquidTypeX!);
        }
    }
}

