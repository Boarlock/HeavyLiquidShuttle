using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public abstract class StateSingle<TNetwork1> where TNetwork1 : class
    {
        // Vars to track transfer state across Prefix to Postfix
        public TNetwork1? Instance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
    }

    public abstract class LiquidIntegrationSingle<TNetwork1> where TNetwork1 : class
    {
        protected readonly HeavyLiquidShuttle Shuttle;

        // Cleanup method when the Shuttle is destroyed to let all subscribers of the Tick events to unsubscribe themselves
        protected bool cleanedUp = false;
        public LiquidIntegrationSingle(HeavyLiquidShuttle shuttle)
        {
            Shuttle = shuttle;

            HeavyLiquidShuttleGameComponent.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration += OnTransferTick;
            Shuttle.GizmoIntegration += AddGizmos;
        }

        protected void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComponent.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration -= OnTransferTick;
            Shuttle.GizmoIntegration -= AddGizmos;

            OnCleanup();

            cleanedUp = true;
        }

        protected virtual void OnCleanup() { }

        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsReceive = new HashSetQueue<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsSupply = new HashSetQueue<TNetwork1>();

        protected abstract HashSet<TNetwork1> FindAdjacentNetworks();
        protected abstract void FindValidNet(out TNetwork1? net);
        protected abstract float TryPush(TNetwork1 net, float amount);
        protected abstract StoredType LiquidType { get; }

        // Prepare the Tanks for another receiving cycle
        protected virtual void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            AdjacentXNets = FindAdjacentNetworks();

            if (AdjacentXNets.Count <= 0)
                return;

            if (Shuttle.TankA.Content == LiquidType)
            {
                if (Shuttle.TankA.Counter < 2)
                    Shuttle.TankA.Counter++;

                Shuttle.TankA.ReceiveAllowance = 1.0;
            }
            if (Shuttle.TankB.Content == LiquidType)
            {
                if (Shuttle.TankB.Counter < 2)
                    Shuttle.TankB.Counter++;

                Shuttle.TankB.ReceiveAllowance = 1.0;
            }
        }

        // Method to find a "Valid Net", a network that's connected and isn't currently pushing to the Shuttle
        private void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (AdjacentXNets.Count <= 0)
                return;

            FindValidNet(out TNetwork1? validNet);

            if (validNet == null)
                return;

            TransferTank(Shuttle.TankA, validNet);
            TransferTank(Shuttle.TankB, validNet);
        }

        private void TransferTank(TankState tank, TNetwork1 net)
        {
            if (tank.IsLocked)
                return;

            if (tank.Content != LiquidType)
                return;

            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            float amount = Mathf.Min(tank.TankStorage, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                float remaining = TryPush(net, amount);
                float transferred = amount - remaining;

                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - transferred);
                MassPatch.NotifyLiquidMassChanged(Shuttle);
            }
            finally
            {
                tank.IsTransferringFluid = false;

                if (tank.TankStorage <= 0f)
                {
                    tank.TankStorage = 0f;
                    tank.Content = StoredType.Empty;
                    tank.TransferEnabled = false;
                    tank.IsContaminated = false;
                }
            }
        }

        // Gizmos for enabling transfer of water and oil from Shuttle Tanks
        private IEnumerable<Gizmo> AddGizmos()
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

    /*abstract class State<TNetwork1, TNetwork2> 
        where TNetwork1 : class 
        where TNetwork2 : class
    {
        // Vars to track transfer state across Prefix to Postfix
        public TNetwork1? XInstance;
        public TNetwork2? YInstance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
    }

    abstract class LiquidIntegration<TNetwork1, TNetwork2>
        where TNetwork1 : class
        where TNetwork2 : class
    {
        protected readonly HeavyLiquidShuttle Shuttle;

        protected bool cleanedUp = false;

        public LiquidIntegration(HeavyLiquidShuttle shuttle)
        {
            Shuttle = shuttle;

            HeavyLiquidShuttleGameComponent.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration += OnTransferTick;
            shuttle.GizmoIntegration += AddGizmos;
        }

        protected virtual void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComponent.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration -= OnTransferTick;
            Shuttle.GizmoIntegration -= AddGizmos;

            OnCleanup();

            cleanedUp = true;
        }

        protected virtual void OnCleanup()
        {
        }

        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsReceive = new HashSetQueue<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsSupply = new HashSetQueue<TNetwork1>();

        protected HashSet<TNetwork2> AdjacentYNets = new HashSet<TNetwork2>();
        protected HashSetQueue<TNetwork2> PendingYNetsReceive = new HashSetQueue<TNetwork2>();
        protected  HashSetQueue<TNetwork2> PendingYNetsSupply = new HashSetQueue<TNetwork2>();

    }
}*/
