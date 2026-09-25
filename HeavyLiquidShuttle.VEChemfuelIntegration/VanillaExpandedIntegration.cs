using PipeSystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class VanillaExpandedIntegration
    {
        // shuttle specific to this instance of DBH Integration
        private readonly HeavyLiquidShuttle shuttle;

        // Constructor that registers tick events and Gizmo function from CompHeavyLiquidShuttle
        public VanillaExpandedIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            HeavyLiquidShuttleGameComponent.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration += OnTransferTick;
            shuttle.GizmoIntegration += AddGizmos;

            Log.Message("[HeavyLiquidShuttle] VE shared integration loaded.");
        }

        // Cleanup method when the Shuttle is destroyed to let all subscribers of the Tick events to unsubscribe themselves
        private bool cleanedUp;
        private void Cleanup()
        {
            if (cleanedUp)
                return;

            HeavyLiquidShuttleGameComponent.TickIntegration -= OnShuttleTick;
            HeavyLiquidShuttleGameComponent.TickIntegration -= OnTransferTick;
            shuttle.GizmoIntegration -= AddGizmos;

            cleanedUp = true;
        }

        // HashSets for all adjacent network next to the shuttle and HashSetQueues for networks waiting to give content to the Shuttle
        private HashSet<PipeNet> DeepchemNetworks = new HashSet<PipeNet>();
        private HashSet<PipeNet> HelixienNetworks = new HashSet<PipeNet>();
        private HashSetQueue<PipeNet> DeepchemPendingNets = new HashSetQueue<PipeNet>();
        private HashSetQueue<PipeNet> HelixienPendingNets = new HashSetQueue<PipeNet>();

        // Static field gathered through reflection for VE's markedForTransfer field and helper method to get it
        private static readonly FieldInfo MarkedForTransferField = typeof(PipeNet).GetField("markedForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);
        private List<CompResourceStorage> GetMarkedForTransfer(PipeNet net)
        {
            return (List<CompResourceStorage>)MarkedForTransferField.GetValue(net);
        }

        // Only thing to do for VE is to check around the shuttle or call Cleanup if the Shuttle is Destroyed
        private void OnShuttleTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            ShuttleVESearch.CheckCellsAroundShuttle(shuttle, out DeepchemNetworks, out HelixienNetworks);
        }

        // Method to find "Valid Nets", networks that are connected and aren't currently pushing to the Shuttle
        // This method also handles queueing for receiving network and calling of the method responsible
        private void OnTransferTick()
        {
            if (shuttle.parent.Destroyed)
                Cleanup();

            if (cleanedUp)
                return;

            StoredType type;
            PipeNet? validNet = null;
            bool alreadySupplied = false;
            bool foundValidNet = false;

            if (shuttle.TankA.Counter2 < 2)
                shuttle.TankA.Counter2++;

            if (shuttle.TankB.Counter2 < 2)
                shuttle.TankB.Counter2++;

            // Deepchem logic
            if (DeepchemNetworks.Count > 0)
            {
                type = StoredType.Deepchem;

                foreach (PipeNet net in DeepchemNetworks)
                {
                    List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                    // Storages willing to receive.
                    foreach (CompResourceStorage storage in net.storages)
                    {
                        if (storage.AmountCanAccept >= 1f && !storage.markedForTransfer && !foundValidNet)
                        {
                            validNet = net;
                            foundValidNet = true;
                            break;
                        }

                        if (foundValidNet == true)
                            break;
                    }
                    // Storages marked for transfer.
                    foreach (CompResourceStorage storage in sourceStorages)
                    {
                        if (storage.AmountStored > 1f)
                        {
                            DeepchemPendingNets.Enqueue(net);
                            
                            TankState? tank = shuttle.GetTankForContent(StoredType.Deepchem);

                            if (tank == null)
                                break;

                            if (DeepchemPendingNets.Count > 0 && DeepchemPendingNets.Peek() == net && !alreadySupplied)
                            {
                                TransferFromNetwork(tank, net, type, sourceStorages);

                                alreadySupplied = true;
                                DeepchemPendingNets.Dequeue();
                                tank.Counter2 = 0;
                                break;
                            }
                            else if (tank.Counter2 >= 2)
                            {
                                // Network in Queue has become stale.
                                DeepchemPendingNets.Dequeue();
                                tank.Counter2 = 0;
                            }
                        }
                    }
                }
                if (validNet != null)
                {
                    TransferToTank(shuttle.TankA, validNet, type);
                    TransferToTank(shuttle.TankB, validNet, type);
                }
            }

            validNet = null;
            alreadySupplied = false;
            foundValidNet = false;

            // Helixien logic
            if (HelixienNetworks.Count > 0)
            {
                type = StoredType.Helixien;

                foreach (PipeNet net in HelixienNetworks)
                {
                    List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                    // Storages willing to receive.
                    foreach (CompResourceStorage storage in net.storages)
                    {
                        if (storage.AmountCanAccept >= 1f && !storage.markedForTransfer && !foundValidNet)
                        {
                            validNet = net;
                            foundValidNet = true;
                            break;
                        }

                        if (foundValidNet == true)
                            break;
                    }
                    // Storages marked for transfer.
                    foreach (CompResourceStorage storage in sourceStorages)
                    {
                        if (storage.AmountStored > 1f)
                        {
                            HelixienPendingNets.Enqueue(net);

                            TankState? tank = shuttle.GetTankForContent(StoredType.Helixien);

                            if (tank == null)
                                break;

                            if (HelixienPendingNets.Count > 0 && HelixienPendingNets.Peek() == net && !alreadySupplied)
                            {
                                TransferFromNetwork(tank, net, type, sourceStorages);

                                alreadySupplied = true;
                                HelixienPendingNets.Dequeue();
                                tank.Counter2 = 0;
                                break;
                            }
                            else if (tank.Counter2 >= 2)
                            {
                                // Network in Queue has become stale.
                                HelixienPendingNets.Dequeue();
                                tank.Counter2 = 0;
                            }
                        }
                    }

                }
                if (validNet != null)
                {
                    TransferToTank(shuttle.TankA, validNet, type);
                    TransferToTank(shuttle.TankB, validNet, type);
                }
            }
        }

        // Method to actually perform the transfer from a Shuttle Tank to deepchem/helixien tanks (storages) and validate the transfer request
        private void TransferToTank(TankState tank, PipeNet net, StoredType type)
        {
            if (tank.IsLocked)
                return;

            if (tank.TankStorage <= 0f)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (!tank.TransferEnabled)
                return;

            if (net == null)
                return;

            if (tank.Content != type)
                return;

            float amount = Mathf.Min(tank.TankStorage, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                net.DistributeAmongStorage(amount, out float transferred);
                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - transferred);
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

        // Method to actually perform the transfer from deepchem/helixien tanks (storages) to a Shuttle Tank
        private void TransferFromNetwork(TankState tank, PipeNet net, StoredType type, List<CompResourceStorage> sourceStorages)
        {
            if (tank.IsLocked)
                return;

            if (tank.IsTransferringFluid)
                return;

            if (tank.Content != StoredType.Empty && tank.Content != type)
                return;

            float freeCapacity = tank.TankCapacity - tank.TankStorage;

            if (freeCapacity <= 0f)
                return;

            float amount = Mathf.Min(freeCapacity, 1f);

            tank.IsTransferringFluid = true;

            try
            {
                net.DrawAmongStorage(amount, out float drawn, sourceStorages, false);

                if (drawn <= 0f)
                    return;

                tank.Content = type;
                tank.TankStorage += drawn;

            }
            finally
            {
                tank.IsTransferringFluid = false;
            }
        }

        // Gizmos for enabling transfer of deepchem and helixien from Shuttle Tanks
        private IEnumerable<Gizmo> AddGizmos()
        {
            if (DeepchemNetworks.Count > 0 || HelixienNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Deepchem && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Deepchem",
                        defaultDesc = "Tank A: Discharge into an adjacent deepchem chemfuel network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadDeepchem"),
                        isActive = () =>
                        {
                            return shuttle.TankA.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankA.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankA);
                        }
                    };
                }
                else if (shuttle.TankA.Content == StoredType.Helixien && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Helixien",
                        defaultDesc = "Tank A: Discharge into an adjacent helixien gas network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadHelixien"),
                        isActive = () =>
                        {
                            return shuttle.TankA.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankA.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankA);
                        }
                    };
                }

                if (shuttle.TankB.Content == StoredType.Deepchem && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Deepchem",
                        defaultDesc = "Tank B: Discharge into an adjacent deepchem chemfuel network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadDeepchem"),
                        isActive = () =>
                        {
                            return shuttle.TankB.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankB.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankB);
                        }
                    };
                }
                else if (shuttle.TankB.Content == StoredType.Helixien && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Helixien",
                        defaultDesc = "Tank B: Discharge into an adjacent helixien gas network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadHelixien"),
                        isActive = () =>
                        {
                            return shuttle.TankB.TransferEnabled;
                        },
                        toggleAction = () =>
                        {
                            if (shuttle.TankB.TankStorage <= 0f)
                                return;

                            shuttle.ToggleTransfer(shuttle.TankB);
                        }
                    };
                }
            }
        }
    }
}
