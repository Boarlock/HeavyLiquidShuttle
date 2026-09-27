using HarmonyLib;
using HeavyLiquidShuttleMod;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

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

        protected virtual StoredType LiquidTypeX { get; }
        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected List<TStorage1> SupplyStorageCandidatesX = new List<TStorage1>();
        protected List<TStorage1> ReceiveStorageCandidatesX = new List<TStorage1>();
        protected TStorage1? LastSuppliedX;
        protected TStorage1? LastReceivedX;

        protected abstract void FindAdjacentNetworks();
        protected abstract void FindValidStoragesX(
            out TStorage1? validSupplyStorage, 
            out TStorage1? validReceiveStorage);
        protected abstract float ModifyStorageX(TStorage1 storage, float amount, bool addTo);

        protected virtual void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            Shuttle.TankA.ReceiveAllowance = 1f;
            Shuttle.TankA.SupplyAllowance = 1f;
            Shuttle.TankB.ReceiveAllowance = 1f;
            Shuttle.TankB.SupplyAllowance = 1f;

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

            TankState? tankSupply = Shuttle.GetTankForSupply(LiquidTypeX);
            TankState? tankReceive = Shuttle.GetTankForReceive(LiquidTypeX);


            // TryPush pushes from Tank Supply to Network Receive
            if (tankSupply != null && validReceiveStorage != null && tankSupply.SupplyAllowance > 0f)
                TryPushX(validReceiveStorage, tankSupply);

            // TryPull pulls from Network Supply to Tank Receive
            if (tankReceive != null && validSupplyStorage != null && tankReceive.ReceiveAllowance > 0f)
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
            if (tank.TankStorage <= 0f)
                return;

            if (!tank.TransferEnabled)
                return;

            float amount = Mathf.Min(tank.TankStorage, tank.SupplyAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorageX(storage, amount, true);

            tank.TankStorage -= transferred;

            tank.SupplyAllowance -= transferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);

            if (tank.TankStorage <= 0f)
            {
                tank.TankStorage = 0f;
                tank.Content = StoredType.Empty;
                tank.TransferEnabled = false;
            }
        }

        protected void TryPullX(TStorage1 storage, TankState tank)
        {
            if (tank.TankStorage >= tank.TankCapacity)
                return;

            float amount = Mathf.Min(tank.TankCapacity - tank.TankStorage, tank.ReceiveAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorageX(storage, amount, false);

            if (transferred > 0f)
                tank.Content = LiquidTypeX;

            tank.TankStorage += transferred;
            tank.ReceiveAllowance -= transferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);
        }

        protected virtual IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count <= 0)
                yield break;

            if (Shuttle.TankA.Content == LiquidTypeX && Shuttle.TankA.TankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank A", LiquidTypeX)!;

            if (Shuttle.TankB.Content == LiquidTypeX && Shuttle.TankB.TankStorage > 0f)
                yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank B", LiquidTypeX)!;
        }
    }

    public abstract class LiquidIntegrationDouble<TNetwork1, TStorage1, TNetwork2, TStorage2> : LiquidIntegrationSingle<TNetwork1, TStorage1>
        where TNetwork1 : class
        where TStorage1 : class
        where TNetwork2 : class
        where TStorage2 : class
    {
        public LiquidIntegrationDouble(HeavyLiquidShuttle shuttle) : base(shuttle) {  }

        protected virtual StoredType LiquidTypeY { get; }
        protected HashSet<TNetwork2> AdjacentYNets = new HashSet<TNetwork2>();
        protected List<TStorage2> SupplyStorageCandidatesY = new List<TStorage2>();
        protected List<TStorage2> ReceiveStorageCandidatesY = new List<TStorage2>();
        protected TStorage2? LastSuppliedY;
        protected TStorage2? LastReceivedY;

        protected override void FindAdjacentNetworks() { }
        protected abstract void FindAdjacentNetworksDouble(
            out HashSet<TNetwork1> adjacentXNets, 
            out HashSet<TNetwork2> adjacentYNets);
        protected abstract void FindValidStoragesY(
            out TStorage2? validSupplyStorage,
            out TStorage2? validReceiveStorage);
        protected abstract float ModifyStorageY(TStorage2 storage, float amount, bool addTo);

        protected override void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            FindAdjacentNetworksDouble(out AdjacentXNets, out AdjacentYNets);

            Shuttle.TankA.ReceiveAllowance = 1f;
            Shuttle.TankA.SupplyAllowance = 1f;
            Shuttle.TankB.ReceiveAllowance = 1f;
            Shuttle.TankB.SupplyAllowance = 1f;

            if (AdjacentXNets.Count <= 0 && AdjacentYNets.Count <= 0)
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

            TankState? tankSupply;
            TankState? tankReceive;

            if (AdjacentXNets.Count > 0)
            {
                FindValidStoragesX(
                out TStorage1? validSupplyStorageX,
                out TStorage1? validReceiveStorageX);

                tankSupply = Shuttle.GetTankForSupply(LiquidTypeX);
                tankReceive = Shuttle.GetTankForReceive(LiquidTypeX);

                if (tankSupply != null && validReceiveStorageX != null && tankSupply.SupplyAllowance > 0f)
                    TryPushX(validReceiveStorageX, tankSupply);

                if (tankReceive != null && validSupplyStorageX != null && tankReceive.ReceiveAllowance > 0f)
                    TryPullX(validSupplyStorageX, tankReceive);
            }

            if (AdjacentYNets.Count > 0)
            {
                FindValidStoragesY(
                out TStorage2? validSupplyStorageY,
                out TStorage2? validReceiveStorageY);

                tankSupply = Shuttle.GetTankForSupply(LiquidTypeY);
                tankReceive = Shuttle.GetTankForReceive(LiquidTypeY);

                if (tankSupply != null && validReceiveStorageY != null && tankSupply.SupplyAllowance > 0f)
                    TryPushY(validReceiveStorageY, tankSupply);

                if (tankReceive != null && validSupplyStorageY != null && tankReceive.ReceiveAllowance > 0f)
                    TryPullY(validSupplyStorageY, tankReceive);
            }
        }

        protected void StorageSelectY(
            out TStorage2? validSupplyStorage,
            out TStorage2? validReceiveStorage)
        {

            validSupplyStorage = null;
            validReceiveStorage = null;

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
                validSupplyStorage = LastSuppliedY;
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
                validReceiveStorage = LastReceivedY;
            }
        }

        private void TryPushY(TStorage2 storage, TankState tank)
        {
            if (tank.TankStorage <= 0f)
                return;

            if (!tank.TransferEnabled)
                return;

            float amount = Mathf.Min(tank.TankStorage, tank.SupplyAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorageY(storage, amount, true);

            tank.TankStorage -= transferred;

            tank.SupplyAllowance -= transferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);

            if (tank.TankStorage <= 0f)
            {
                tank.TankStorage = 0f;
                tank.Content = StoredType.Empty;
                tank.TransferEnabled = false;
            }
        }

        private void TryPullY(TStorage2 storage, TankState tank)
        {
            if (tank.TankStorage >= tank.TankCapacity)
                return;

            float amount = Mathf.Min(tank.TankCapacity - tank.TankStorage, tank.ReceiveAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorageY(storage, amount, false);

            if (transferred > 0f)
                tank.Content = LiquidTypeY;

            tank.TankStorage += transferred;
            tank.ReceiveAllowance -= transferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);
        }

        protected override IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count > 0)
            {
                if (Shuttle.TankA.Content == LiquidTypeX && Shuttle.TankA.TankStorage > 0f)
                    yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank A", LiquidTypeX)!;

                if (Shuttle.TankB.Content == LiquidTypeX && Shuttle.TankB.TankStorage > 0f)
                    yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank B", LiquidTypeX)!;
            }

            if (AdjacentYNets.Count > 0)
            {
                if (Shuttle.TankA.Content == LiquidTypeY && Shuttle.TankA.TankStorage > 0f)
                    yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank A", LiquidTypeY)!;

                if (Shuttle.TankB.Content == LiquidTypeY && Shuttle.TankB.TankStorage > 0f)
                    yield return HeavyLiquidShuttle.CreateDischargeGizmo(Shuttle, "Tank B", LiquidTypeY)!;
            }
        }
    }
}

