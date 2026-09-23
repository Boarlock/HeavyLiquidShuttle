using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class PushWaterState
    {
        // Vars to track transfer state across Prefix to Postfix
        public PlumbingNet? Instance;
        public TankState? Tank;
        public HeavyLiquidShuttle? Shuttle;
        public DubsBadHygieneIntegration? Integration;

        // To track what water storages recieved water from our shuttle if we pushed.
        public Dictionary<CompWaterStorage, float> WaterStorages = new Dictionary<CompWaterStorage, float>();
    }

    public class DubsBadHygieneIntegration
    {
        private static readonly HashSet<DubsBadHygieneIntegration> Instances = new HashSet<DubsBadHygieneIntegration>();
        private readonly HeavyLiquidShuttle shuttle;
        public DubsBadHygieneIntegration(HeavyLiquidShuttle shuttle)
        {
            Log.Message($"[HLS DBH] Constructor created for shuttle: {shuttle.parent}");
            this.shuttle = shuttle;

            Instances.Add(this);

            HeavyLiquidShuttle.TickIntegration += OnShuttleTick;
            HeavyLiquidShuttle.TickIntegration += OnTransferTick;
            HeavyLiquidShuttle.GizmoIntegration += AddGizmos;
        }

        static DubsBadHygieneIntegration()
        {
            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dbh");

            MethodInfo pushWater = AccessTools.Method(typeof(PlumbingNet), nameof(PlumbingNet.PushWater));
            MethodInfo prefix = AccessTools.Method(typeof(DubsBadHygieneIntegration), nameof(Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(DubsBadHygieneIntegration), nameof(Postfix));

            harmony.Patch(pushWater, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene integration loaded.");
        }

        public HashSet<PlumbingNet> AdjacentNetworks = new HashSet<PlumbingNet>();
        public HashSetQueue<PlumbingNet> PendingNetworks = new HashSetQueue<PlumbingNet>();

        public static void Prefix(PlumbingNet __instance, out PushWaterState __state)
        {
            Log.Message($"[HLS DBH] PushWater PREFIX - Net: {__instance}");

            __state = new PushWaterState();

            DubsBadHygieneIntegration? integration = null;

            foreach (DubsBadHygieneIntegration instance in Instances)
            {
                Log.Message($"[HLS DBH] Checking integration - Adjacent count: {instance.AdjacentNetworks.Count}");

                if (instance.AdjacentNetworks.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
            {
                Log.Message("[HLS DBH] PushWater PREFIX EXIT - No matching integration.");
                return;
            }

            Log.Message("[HLS DBH] PushWater PREFIX - Found matching integration.");

            TankState? tank = integration.shuttle.GetTankForContent(StoredType.Water);

            Log.Message($"[HLS DBH] Water tank found: {tank != null}");

            if (tank == null)
                return;

            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.shuttle;
            __state.Integration = integration;

            Log.Message($"[HLS DBH] State populated - Tank storage: {tank.TankStorage}");

            foreach (CompWaterStorage waterTower in __instance.WaterTowers)
            {
                __state.WaterStorages[waterTower] = waterTower.WaterStorage;
            }

            Log.Message($"[HLS DBH] Stored {__state.WaterStorages.Count} water tower states.");
        }

        public static void Postfix(PushWaterState __state, ref float __result)
        {
            Log.Message(
        $"[HLS DBH] PushWater POSTFIX - " +
        $"Instance: {__state.Instance != null}, " +
        $"Tank: {__state.Tank != null}, " +
        $"Shuttle: {__state.Shuttle != null}, " +
        $"Integration: {__state.Integration != null}, " +
        $"Result: {__result}"
    );

            // This PushWater call was not associated with one of our shuttles.
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
            {
                Log.Message("[HLS DBH] POSTFIX EXIT - Missing state.");
                return;
            }
            // See which DBH towers actually received water from this shuttle.
            foreach (KeyValuePair<CompWaterStorage, float> entry in __state.WaterStorages)
            {

                CompWaterStorage waterTower = entry.Key;
                float before = entry.Value;

                if (waterTower.WaterStorage > before && __state.Tank.IsContaminated && __state.Tank.IsTransferringFluid)
                {
                    waterTower.WaterQuality = ContaminationLevel.Contaminated;
                }
            }

            // If DBH completely satisfied the request, nothing remains for us.
            if (__result <= 0f)
            {
                Log.Message("[HLS DBH] POSTFIX EXIT - PushWater result <= 0.");
                return;
            }

            if (__state.Tank.IsTransferringFluid)
            {
                Log.Message("[HLS DBH] POSTFIX EXIT - Tank currently transferring.");
                return;
            }

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                Log.Message("[HLS DBH] Tank allowance exhausted - queueing network.");
                __state.Integration.PendingNetworks.Enqueue(__state.Instance);
                return;
            }

            if (__state.Integration.PendingNetworks.Count > 0)
            {
                
                // Network in Queue has become stale.
                if (__state.Tank.Counter >= 2)
                {
                    Log.Message($"[HLS DBH] Network has become stale {__state.Tank.Counter >= 2}");
                    __state.Integration.PendingNetworks.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                // Check current call against next item in the Queue
                if (__state.Integration.PendingNetworks.Peek() != __state.Instance)
                {
                    Log.Message($"[HLS DBH] Checking queue against current net, match: {__state.Integration.PendingNetworks.Peek() != __state.Instance}");
                    return;
                }

                // This network is now being served.
                __state.Integration.PendingNetworks.Dequeue();
            }

            //Reset the Queue counter
            __state.Tank.Counter = 0;

            // Safer way to update storage so this method only gives what was taken.
            float freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            Log.Message(
    $"[HLS DBH] ACCEPTING WATER - " +
    $"Result: {__result}, " +
    $"Allowance: {__state.Tank.ReceiveAllowance}, " +
    $"Free capacity: {freeCapacity}"
);

            if (freeCapacity <= 0f)
                return;

            float accepted = Mathf.Min(__result, (float)__state.Tank.ReceiveAllowance, freeCapacity);

            if (accepted <= 0f)
                return;

            // Update shuttle's mass and water storage.
            __state.Tank.Content = StoredType.Water;
            __state.Tank.TankStorage += accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = __state.Instance.IsNetContaminated();

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            Log.Message(
    $"[HLS DBH] WATER ACCEPTED - " +
    $"Accepted: {accepted}, " +
    $"New storage: {__state.Tank.TankStorage}, " +
    $"Remaining PushWater: {__result}"
);

            __result -= accepted;
        }

        private void OnShuttleTick()
        {
            Log.Message($"[HLS DBH] OnShuttleTick START - Shuttle: {shuttle.parent}");
            AdjacentNetworks = ShuttleWaterSearch.CheckCellsAroundShuttle(shuttle);
            Log.Message($"[HLS DBH] AdjacentNetworks found: {AdjacentNetworks.Count}");
            if (AdjacentNetworks.Count <= 0)
            {
                Log.Message("[HLS DBH] OnShuttleTick EXIT - No adjacent networks.");
                return;
            }

            if (shuttle.TankA.Content == StoredType.Water)
            {
                Log.Message($"[HLS DBH] Tank A water - Storage: {shuttle.TankA.TankStorage}, Allowance: {shuttle.TankA.ReceiveAllowance}");
                if (shuttle.TankA.Counter < 2)
                    shuttle.TankA.Counter++;

                shuttle.TankA.ReceiveAllowance = 1.0;
            }
            if (shuttle.TankB.Content == StoredType.Water)
            {
                Log.Message($"[HLS DBH] Tank B water - Storage: {shuttle.TankB.TankStorage}, Allowance: {shuttle.TankB.ReceiveAllowance}");
                if (shuttle.TankB.Counter < 2)
                    shuttle.TankB.Counter++;

                shuttle.TankB.ReceiveAllowance = 1.0;
            }
            Log.Message("[HLS DBH] OnShuttleTick END");
        }

        private void OnTransferTick()
        {
            if (AdjacentNetworks.Count <= 0)
                return;

            PlumbingNet? validNet = null;

            foreach (PlumbingNet net in AdjacentNetworks)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
                {
                    if (storage.space >= 1f && !storage.DrainTank)
                    {
                        validNet = net;
                        break;
                    }
                }

                if (validNet != null)
                    break;
            }

            if (validNet == null)
                return;

            TransferTank(shuttle.TankA, validNet);
            TransferTank(shuttle.TankB, validNet);
        }

        private void TransferTank(TankState tank, PlumbingNet net)
        {
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
                float remaining = net.PushWater(amount);
                float transferred = amount - remaining;

                tank.TankStorage = Mathf.Max(0f, tank.TankStorage - transferred);
                MassPatch.NotifyLiquidMassChanged(shuttle);
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

        private IEnumerable<Gizmo> AddGizmos(HeavyLiquidShuttle shuttle)
        {
            if (AdjacentNetworks.Count > 0)
            {
                if (shuttle.TankA.Content == StoredType.Water && shuttle.TankA.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank A: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
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
                if (shuttle.TankB.Content == StoredType.Water && shuttle.TankB.TankStorage > 0f)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "Discharge Water",
                        defaultDesc = "Tank B: Discharge into an adjacent water network.",
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmo/UnloadWater"),
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
