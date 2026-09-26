using DubsBadHygiene;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class PushWaterState : StateSingle<PlumbingNet>
    {
        public DubsBadHygieneIntegration? Integration;
        public Dictionary<CompWaterStorage, float> WaterStorages = new Dictionary<CompWaterStorage, float>();
    }

    public class DubsBadHygieneIntegration : LiquidIntegrationSingle<PlumbingNet>
    {
        private static readonly HashSet<DubsBadHygieneIntegration> Instances = new HashSet<DubsBadHygieneIntegration>();
        protected override StoredType LiquidType => StoredType.Water;
        protected override HashSet<PlumbingNet> FindAdjacentNetworks()
        {
            return ShuttleWaterSearch.CheckCellsAroundShuttle(Shuttle);
        }

        protected override void FindValidNet(out PlumbingNet? validWaterNet)
        {
            validWaterNet = null;

            foreach (PlumbingNet net in AdjacentXNets)
            {
                foreach (CompWaterStorage storage in net.WaterTowers)
                {
                    if (storage.space >= 1f && !storage.DrainTank)
                    {
                        validWaterNet = net;
                        break;
                    }
                }
                if (validWaterNet != null)
                    break;
            }
        }

        protected override float TryPush(PlumbingNet net, float amount)
        {
            return net.PushWater(amount);
        }

        public DubsBadHygieneIntegration(HeavyLiquidShuttle shuttle) : base(shuttle)
        {
            Instances.Add(this);
        }

        protected override void OnCleanup()
        {
            Instances.Remove(this);
        }

        static DubsBadHygieneIntegration()
        {
            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.dbh");

            MethodInfo pushOil = AccessTools.Method(typeof(PlumbingNet), nameof(PlumbingNet.PushWater));
            MethodInfo prefix = AccessTools.Method(typeof(DubsBadHygieneIntegration), nameof(Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(DubsBadHygieneIntegration), nameof(Postfix));

            harmony.Patch(pushOil, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            Log.Message("[HeavyLiquidShuttle] Dubs Bad Hygiene integration loaded.");
        }

        public static void Prefix(PlumbingNet __instance, out PushWaterState __state)
        {
            __state = new PushWaterState();

            DubsBadHygieneIntegration? integration = null;

            foreach (DubsBadHygieneIntegration instance in Instances)
            {
                if (instance.AdjacentXNets.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.Shuttle.GetTankForContent(StoredType.Water);

            if (tank == null || tank.IsLocked)
                return;

            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.Shuttle;
            __state.Integration = integration;

            foreach (CompWaterStorage waterTower in __instance.WaterTowers)
            {
                __state.WaterStorages[waterTower] = waterTower.WaterStorage;
            }
        }

        public static void Postfix(PushWaterState __state, ref float __result)
        {
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
                return;

            foreach (KeyValuePair<CompWaterStorage, float> entry in __state.WaterStorages)
            {

                CompWaterStorage waterTower = entry.Key;
                float before = entry.Value;

                if (waterTower.WaterStorage > before && __state.Tank.IsContaminated && __state.Tank.IsTransferringFluid)
                {
                    waterTower.WaterQuality = ContaminationLevel.Contaminated;
                }
            }

            if (__result <= 0f)
                return;

            if (__state.Tank.IsTransferringFluid)
                return;

            if (__state.Tank.ReceiveAllowance <= 0f)
            {
                __state.Integration.PendingXNetsReceive.Enqueue(__state.Instance);
                return;
            }

            if (__state.Integration.PendingXNetsReceive.Count > 0)
            {

                if (__state.Tank.Counter >= 2)
                {
                    __state.Integration.PendingXNetsReceive.Dequeue();
                    __state.Tank.Counter = 0;

                    return;
                }

                if (__state.Integration.PendingXNetsReceive.Peek() != __state.Instance)
                    return;

                __state.Integration.PendingXNetsReceive.Dequeue();
            }

            __state.Tank.Counter = 0;

            float freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0f)
                return;

            float accepted = Mathf.Min(__result, (float)__state.Tank.ReceiveAllowance, freeCapacity);

            if (accepted <= 0f)
                return;

            __state.Tank.Content = StoredType.Water;
            __state.Tank.TankStorage += accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = __state.Instance.IsNetContaminated();

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }
    }
}
