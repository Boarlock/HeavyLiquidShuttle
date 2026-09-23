/*using HarmonyLib;
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
        }

        public static void Initialize()
        {
            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;

            Log.Message("[HeavyLiquidShuttle] VE shared integration loaded.");
        }

        public static readonly Dictionary<HeavyLiquidShuttle, HashSet<PipeNet>> DeepchemNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PipeNet>>();
        public static readonly Dictionary<HeavyLiquidShuttle, HashSet<PipeNet>> HelixienNetworks = new Dictionary<HeavyLiquidShuttle, HashSet<PipeNet>>();

        private static readonly Dictionary<HeavyLiquidShuttle, HashSetQueue<PipeNet>> DeepchemPendingNets = new Dictionary<HeavyLiquidShuttle, HashSetQueue<PipeNet>>();
        private static readonly Dictionary<HeavyLiquidShuttle, HashSetQueue<PipeNet>> HelixienPendingNets = new Dictionary<HeavyLiquidShuttle, HashSetQueue<PipeNet>>();

        private static readonly FieldInfo MarkedForTransferField = typeof(PipeNet).GetField("markedForTransfer", BindingFlags.Instance | BindingFlags.NonPublic);

        private static List<CompResourceStorage> GetMarkedForTransfer(PipeNet net)
        {
            return (List<CompResourceStorage>)MarkedForTransferField.GetValue(net);
        }

        private static void OnShuttleTick(HeavyLiquidShuttle shuttle)
        {

            ShuttleVESearch.CheckCellsAroundShuttle(shuttle, out HashSet<PipeNet> deepchem, out HashSet<PipeNet> helixien);

            Log.Message(
    $"[HeavyLiquidShuttle] VE scan: Deepchem={deepchem.Count}, Helixien={helixien.Count}");

            // Deepchem networks
            if (deepchem.Count == 0)
            {
                DeepchemNetworks.Remove(shuttle);
            }
            else
            {
                DeepchemNetworks[shuttle] = deepchem;
            }

            // Helixien networks
            if (helixien.Count == 0)
            {
                HelixienNetworks.Remove(shuttle);
            }
            else
            {
                HelixienNetworks[shuttle] = helixien;
            }
            Log.Message(
    $"[HeavyLiquidShuttle] VE networks stored: Deepchem={DeepchemNetworks.ContainsKey(shuttle)}, Helixien={HelixienNetworks.ContainsKey(shuttle)}");
        }

        private static void OnTransferTick(HeavyLiquidShuttle shuttle)
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
            if (DeepchemNetworks.TryGetValue(shuttle, out HashSet<PipeNet> deepchemnNets))
            {
                type = StoredType.Deepchem;

                Log.Message(
        $"[HeavyLiquidShuttle] Deepchem transfer: {deepchemnNets.Count} networks.");

                foreach (PipeNet net in deepchemnNets)
                {
                    List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                    Log.Message(
    $"[HeavyLiquidShuttle] Deepchem net: storages={net.storages.Count}, marked={sourceStorages.Count}");

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
                        Log.Message(
        $"[HeavyLiquidShuttle] Deepchem source: stored={storage.AmountStored}");

                        if (storage.AmountStored > 1f)
                        {
                            if (!DeepchemPendingNets.TryGetValue(shuttle, out HashSetQueue<PipeNet> pendingNets))
                            {
                                pendingNets = new HashSetQueue<PipeNet>();
                                DeepchemPendingNets[shuttle] = pendingNets;
                            }

                            pendingNets.Enqueue(net);
                            
                            TankState? tank = shuttle.GetTankForContent(StoredType.Deepchem);

                            if (tank == null)
                                break;

                            if (pendingNets.Count > 0 && pendingNets.Peek() == net && !alreadySupplied)
                            {
                                TransferFromNetwork(tank, net, type, sourceStorages);

                                alreadySupplied = true;
                                pendingNets.Dequeue();
                                tank.Counter2 = 0;
                                break;
                            }
                            else if (tank.Counter2 >= 2)
                            {
                                // Network in Queue has become stale.
                                pendingNets.Dequeue();
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
            if (HelixienNetworks.TryGetValue(shuttle, out HashSet<PipeNet> helixienNets))
            {
                type = StoredType.Helixien;

                Log.Message(
        $"[HeavyLiquidShuttle] Deepchem transfer: {helixienNets.Count} networks.");

                foreach (PipeNet net in helixienNets)
                {
                    List<CompResourceStorage> sourceStorages = GetMarkedForTransfer(net);

                    Log.Message(
    $"[HeavyLiquidShuttle] Deepchem net: storages={net.storages.Count}, marked={sourceStorages.Count}");

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
                        Log.Message(
        $"[HeavyLiquidShuttle] Deepchem source: stored={storage.AmountStored}");

                        if (storage.AmountStored > 1f)
                        {
                            if (!HelixienPendingNets.TryGetValue(shuttle, out HashSetQueue<PipeNet> pendingNets))
                            {
                                pendingNets = new HashSetQueue<PipeNet>();
                                HelixienPendingNets[shuttle] = pendingNets;
                            }

                            pendingNets.Enqueue(net);

                            TankState? tank = shuttle.GetTankForContent(StoredType.Helixien);

                            if (tank == null)
                                break;

                            if (pendingNets.Count > 0 && pendingNets.Peek() == net && !alreadySupplied)
                            {
                                TransferFromNetwork(tank, net, type, sourceStorages);

                                alreadySupplied = true;
                                pendingNets.Dequeue();
                                tank.Counter2 = 0;
                                break;
                            }
                            else if (tank.Counter2 >= 2)
                            {
                                // Network in Queue has become stale.
                                pendingNets.Dequeue();
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

        private static void TransferToTank(TankState tank, PipeNet net, StoredType type)
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

        private static void TransferFromNetwork(TankState tank, PipeNet net, StoredType type, List<CompResourceStorage> sourceStorages)
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

        private static IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (DeepchemNetworks.ContainsKey(shuttle) || HelixienNetworks.ContainsKey(shuttle))
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
}*/
