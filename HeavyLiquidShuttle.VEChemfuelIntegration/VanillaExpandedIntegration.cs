using PipeSystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class VanillaExpandedIntegration
    {
        private readonly HeavyLiquidShuttle shuttle;
        public VanillaExpandedIntegration(HeavyLiquidShuttle shuttle)
        {
            this.shuttle = shuttle;

            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Log.Message("[HeavyLiquidShuttle] VE shared integration loaded.");
        }

        public HashSet<PipeNet> DeepchemNetworks = new HashSet<PipeNet>();
        public HashSet<PipeNet> HelixienNetworks = new HashSet<PipeNet>();

        private HashSetQueue<PipeNet> DeepchemPendingNets = new HashSetQueue<PipeNet>();
        private HashSetQueue<PipeNet> HelixienPendingNets = new HashSetQueue<PipeNet>();

        private static readonly FieldInfo MarkedForTransferField = typeof(PipeNet).GetField("markedForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<CompResourceStorage> GetMarkedForTransfer(PipeNet net)
        {
            return (List<CompResourceStorage>)MarkedForTransferField.GetValue(net);
        }

        private void OnShuttleTick() => ShuttleVESearch.CheckCellsAroundShuttle(shuttle, out DeepchemNetworks, out HelixienNetworks);
        
        private void OnTransferTick()
        {
            StoredType type;
            PipeNet? validNet = null;
            bool alreadySupplied = false;
            bool foundValidNet = false;

            if (shuttle.TankA.Counter2 < 2)
                shuttle.TankA.Counter2++;

            if (shuttle.TankB.Counter2 < 2)
                shuttle.TankB.Counter2++;

            Log.Message("[HeavyLiquidShuttle] VEF OnTransferTick running.");

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

        private void TransferToTank(TankState tank, PipeNet net, StoredType type)
        {
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

        private void TransferFromNetwork(TankState tank, PipeNet net, StoredType type, List<CompResourceStorage> sourceStorages)
        {
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
