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
    public abstract class LiquidIntegration<TNetwork1, TStorage1>
        where TNetwork1 : class
        where TStorage1 : class
    {
        protected readonly HeavyLiquidShuttle Shuttle;

        public LiquidIntegration(HeavyLiquidShuttle shuttle)
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

        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected List<TStorage1> SupplyStorageCandidatesX = new List<TStorage1>();
        protected List<TStorage1> ReceiveStorageCandidatesX = new List<TStorage1>();

        protected TStorage1? LastSuppliedX;
        protected TStorage1? LastReceivedX;

        protected abstract void FindAdjacentNetworks();
        protected abstract void FindValidStoragesX(
            out TStorage1? validSupplyStorage, 
            out TStorage1? validReceiveStorage);
        protected abstract float ModifyStorage(TStorage1 storage, float amount, bool addTo);
        protected virtual StoredType LiquidType { get; set; }


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

            TankState? tankSupply = Shuttle.GetTankForSupply(LiquidType);
            TankState? tankReceive = Shuttle.GetTankForReceive(LiquidType);

            if (tankSupply != null && validSupplyStorage != null && tankSupply.SupplyAllowance > 0f)
                TryPush(validSupplyStorage, tankSupply);

            if (tankReceive != null && validReceiveStorage != null && tankReceive.ReceiveAllowance > 0f)
                TryPull(validReceiveStorage, tankReceive);

        }

        protected void TryPush(TStorage1 storage, TankState tank)
        {
            if (tank.TankStorage <= 0f)
                return;

            if (!tank.TransferEnabled)
                return;

            float amount = Mathf.Min(tank.TankStorage, tank.SupplyAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorage(storage, amount, true);

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

        protected void TryPull(TStorage1 storage, TankState tank)
        {
            if (tank.TankStorage >= tank.TankCapacity)
                return;

            float amount = Mathf.Min(tank.TankCapacity - tank.TankStorage, tank.ReceiveAllowance, 1f);

            if (amount <= 0f)
                return;

            float transferred = ModifyStorage(storage, amount, false);

            if (transferred > 0f)
                tank.Content = LiquidType;

            tank.TankStorage += transferred;
            tank.ReceiveAllowance -= transferred;
            MassPatch.NotifyLiquidMassChanged(Shuttle);
        }

        protected virtual IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count > 0)
            {
                if (Shuttle.TankA.Content == LiquidType && Shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge " + LiquidType,
                        defaultDesc = "Tank A: Discharge into an adjacent " + LiquidType + " network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Unload" + LiquidType),
                        isActive = () =>
                        {
                            return Shuttle.TankA.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (Shuttle.TankA.TankStorage <= 0f)
                                return;

                            Shuttle.ToggleTransfer(Shuttle.TankA);
                        }
                    };
                }

                if (Shuttle.TankB.Content == LiquidType && Shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge " + LiquidType,
                        defaultDesc = "Tank B: Discharge into an adjacent " + LiquidType + " network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/Unload" + LiquidType),
                        isActive = () =>
                        {
                            return Shuttle.TankB.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (Shuttle.TankB.TankStorage <= 0f)
                                return;

                            Shuttle.ToggleTransfer(Shuttle.TankB);
                        }
                    };
                }
            }
        }
    }
}
