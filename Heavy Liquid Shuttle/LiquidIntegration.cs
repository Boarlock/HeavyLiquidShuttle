using HarmonyLib;
using HeavyLiquidShuttleMod;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public abstract class StateSingle<TNetwork1> where TNetwork1 : class
    {
        public TNetwork1? Instance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
    }

    public abstract class LiquidIntegrationSingle<TNetwork1> where TNetwork1 : class
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

        protected HashSet<TNetwork1> AdjacentXNets = new HashSet<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsReceive = new HashSetQueue<TNetwork1>();
        protected HashSetQueue<TNetwork1> PendingXNetsSupply = new HashSetQueue<TNetwork1>();

        protected int ReceiveNetCounter = 0;
        protected int SupplyNetCounter = 0;

        protected abstract void FindAdjacentNetworks();
        protected abstract void FindValidNet(out TNetwork1? validNet, TankState tank);
        protected abstract float TryPush(TNetwork1 net, float amount);
        protected virtual StoredType LiquidType { get; set; }


        protected virtual void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            FindAdjacentNetworks();

            if (AdjacentXNets.Count <= 0)
            {
                PendingXNetsReceive.Clear();
                PendingXNetsSupply.Clear();
                ReceiveNetCounter = 0;
                SupplyNetCounter = 0;

                return;
            }

            if (ReceiveNetCounter < 2)
                ReceiveNetCounter++;

            if (SupplyNetCounter < 2)
                SupplyNetCounter++;
        }

        protected virtual void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (AdjacentXNets.Count <= 0)
                return;

            TankState? tank = Shuttle.GetTankForSupply(LiquidType);

            if (tank == null)
                return;

            FindValidNet(out TNetwork1? validNet, tank);

            if (validNet == null)
                return;

            TransferTank(tank, validNet);
        }

        protected virtual void TransferTank(TankState tank, TNetwork1 net)
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

                    if (LiquidType == StoredType.Water)
                        tank.IsContaminated = false;
                }
            }
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


   public abstract class StateDouble<TNetwork1, TNetwork2> : StateSingle<TNetwork1>
        where TNetwork1 : class 
        where TNetwork2 : class
    {
        // Vars to track transfer state across Prefix to Postfix
        public TNetwork1? XInstance;
        public TNetwork2? YInstance;
    }

    public abstract class LiquidIntegrationDouble<TNetwork1, TNetwork2> : LiquidIntegrationSingle<TNetwork1>
        where TNetwork1 : class
        where TNetwork2 : class
    {
        public LiquidIntegrationDouble(HeavyLiquidShuttle shuttle) : base(shuttle) { }

        protected HashSet<TNetwork2> AdjacentYNets = new HashSet<TNetwork2>();
        protected HashSetQueue<TNetwork2> PendingYNetsReceive = new HashSetQueue<TNetwork2>();
        protected HashSetQueue<TNetwork2> PendingYNetsSupply = new HashSetQueue<TNetwork2>();

        protected abstract void FindValidNet(out TNetwork2? validNet, TankState tank);
        protected abstract float TryPush(TNetwork2 net, float amount);

        protected override void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            FindAdjacentNetworks();

            if (AdjacentXNets.Count <= 0 && AdjacentYNets.Count <= 0)
            {
                PendingXNetsReceive.Clear();
                PendingXNetsSupply.Clear();

                PendingYNetsReceive.Clear();
                PendingYNetsSupply.Clear();

                ReceiveNetCounter = 0;
                SupplyNetCounter = 0;

                return;
            }

            if (ReceiveNetCounter < 2)
                ReceiveNetCounter++;

            if (SupplyNetCounter < 2)
                SupplyNetCounter++;
        }

        protected override void OnTransferTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            if (AdjacentXNets.Count <= 0)
                return;

            if (AdjacentXNets.Count > 0)
            {
                LiquidType = StoredType.Water;

                TankState? tank = Shuttle.GetTankForSupply(LiquidType);

                if (tank == null)
                    return;

                FindValidNet(out TNetwork1? validWaterNet, tank);

                if (validWaterNet == null)
                    return;

                TransferTank(tank, validWaterNet);
            }

            if (AdjacentYNets.Count > 0)
            {
                LiquidType = StoredType.Oil;

                TankState? tank = Shuttle.GetTankForSupply(LiquidType);

                if (tank == null)
                    return;

                FindValidNet(out TNetwork2? validOilNet, tank);

                if (validOilNet == null)
                    return;

                TransferTank(tank, validOilNet);
            }
        }

        protected override void TransferTank(TankState tank, TNetwork1 waterNet)
        {
            if (tank.IsLocked)
                return;

            if (tank.Content != StoredType.Water)
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
                float remaining = TryPush(waterNet, amount);
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

        protected void TransferTank(TankState tank, TNetwork2 oilNet)
        {
            if (tank.IsLocked)
                return;

            if (tank.Content != StoredType.Oil)
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
                float remaining = TryPush(oilNet, amount);
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
                }
            }
        }

        protected override IEnumerable<Gizmo> AddGizmos()
        {
            if (AdjacentXNets.Count > 0 || AdjacentYNets.Count > 0)
            {
                if (Shuttle.TankA.Content == StoredType.Water && Shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank A: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
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
                else if (Shuttle.TankA.Content == StoredType.Oil && Shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank A: Discharge into an adjacent crude oil network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadOil"),
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

                if (Shuttle.TankB.Content == StoredType.Water && Shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank B: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
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
                else if (Shuttle.TankB.Content == StoredType.Oil && Shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Crude",
                        defaultDesc = "Tank B: Discharge into an adjacent crude oil network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadOil"),
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
