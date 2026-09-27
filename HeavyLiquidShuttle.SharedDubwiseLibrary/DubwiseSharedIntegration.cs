using DubsBadHygiene;
using HarmonyLib;
using Rimefeller;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class DubwiseSharedIntegration : LiquidIntegration<PlumbingNet, CompWaterStorage>
    {
        // Constructor that registers tick events and Gizmo function from CompHeavyLiquidShuttle
        public DubwiseSharedIntegration(HeavyLiquidShuttle shuttle) : base(shuttle)
            => shuttle.OilSpillIntegration += StartOilSpill;

        protected override void OnCleanup()
            => Shuttle.OilSpillIntegration -= StartOilSpill;

        protected override void FindAdjacentNetworks() {  }

        protected override StoredType LiquidType => StoredType.Empty;
        private HashSet<PipelineNet> AdjacentYNets = new HashSet<PipelineNet>();
        private List<CompStorageTank> SupplyStorageCandidatesY = new List<CompStorageTank>();
        private List<CompStorageTank> ReceiveStorageCandidatesY = new List<CompStorageTank>();

        private CompStorageTank? LastSuppliedY;
        private CompStorageTank? LastReceivedY;

        protected override void OnShuttleTick()
        {
            if (Shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            ShuttleSearch.CheckCellsAroundShuttle(Shuttle, out AdjacentXNets, out AdjacentYNets);

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
                LiquidType = StoredType.Water;

                FindValidStoragesX(
                out CompWaterStorage? validSupplyStorage,
                out CompWaterStorage? validReceiveStorage);

                tankSupply = Shuttle.GetTankForSupply(LiquidType);
                tankReceive = Shuttle.GetTankForReceive(LiquidType);

                if (tankSupply != null && validSupplyStorage != null && tankSupply.SupplyAllowance > 0f)
                    TryPush(validSupplyStorage, tankSupply);

                if (tankReceive != null && validReceiveStorage != null && tankReceive.ReceiveAllowance > 0f)
                    TryPull(validReceiveStorage, tankReceive);
            }

            if (AdjacentYNets.Count > 0)
            {
                LiquidType = StoredType.Oil;

                FindValidStoragesY(
                out CompStorageTank? validSupplyStorage,
                out CompStorageTank? validReceiveStorage);

                tankSupply = Shuttle.GetTankForSupply(LiquidType);
                tankReceive = Shuttle.GetTankForReceive(LiquidType);

                if (tankSupply != null && validSupplyStorage != null && tankSupply.SupplyAllowance > 0f)
                    TryPush(validSupplyStorage, tankSupply);

                if (tankReceive != null && validReceiveStorage != null && tankReceive.ReceiveAllowance > 0f)
                    TryPull(validReceiveStorage, tankReceive);
            }
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

        private void FindValidStoragesY(
            out CompStorageTank? validSupplyStorage,
            out CompStorageTank? validReceivingStorage)
        {

            SupplyStorageCandidatesY.Clear();
            ReceiveStorageCandidatesY.Clear();

            validSupplyStorage = null;
            validReceivingStorage = null;

            int lastIndex;
            int nextIndex;

            foreach (PipelineNet net in AdjacentYNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                        SupplyStorageCandidatesY.Add(storage);

                    if (storage.Storage > 0f && storage.DrainTank)
                        ReceiveStorageCandidatesY.Add(storage);
                }
            }

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
                validReceivingStorage = LastReceivedY;
            }
        }

        protected override float ModifyStorage(CompWaterStorage storage, float amount, bool addTo)
        {
            float transferred;

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

        private float ModifyStorage(CompStorageTank storage, float amount, bool addTo)
        {
            float transferred;

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

        private void TryPush(CompStorageTank storage, TankState tank)
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

        private void TryPull(CompStorageTank storage, TankState tank)
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
