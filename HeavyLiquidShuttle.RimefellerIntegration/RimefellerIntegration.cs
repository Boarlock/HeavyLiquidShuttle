using HarmonyLib;
using Rimefeller;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class PushCrudeState : StateSingle<PipelineNet>
    {
        public RimefellerIntegration? Integration;
    }

    public class RimefellerIntegration : LiquidIntegrationSingle<PipelineNet>
    {
        private static readonly HashSet<RimefellerIntegration> Instances = new HashSet<RimefellerIntegration>();

        public RimefellerIntegration(HeavyLiquidShuttle shuttle) : base(shuttle)
        {
            Instances.Add(this);
            Shuttle.OilSpillIntegration += StartOilSpill;
        }

        protected override void OnCleanup()
        {
            Instances.Remove(this);
            Shuttle.OilSpillIntegration -= StartOilSpill;
        }

        static RimefellerIntegration()
        {
            Harmony harmony = new Harmony("b0arl0ck.heavyliquidshuttle.rimefeller");

            MethodInfo pushOil = AccessTools.Method(typeof(PipelineNet), nameof(PipelineNet.PushCrude));
            MethodInfo prefix = AccessTools.Method(typeof(RimefellerIntegration), nameof(Prefix));
            MethodInfo postfix = AccessTools.Method(typeof(RimefellerIntegration), nameof(Postfix));

            harmony.Patch(pushOil, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

            Log.Message("[HeavyLiquidShuttle] Rimefeller integration loaded.");
        }

        protected override StoredType LiquidType => StoredType.Oil;

        protected override void FindAdjacentNetworks()
        {
            AdjacentXNets = ShuttleOilSearch.CheckCellsAroundShuttle(Shuttle);
        }

        protected override void FindValidNet(out PipelineNet? validOilNet, TankState tank)
        {
            bool alreadySupplied = false;
            validOilNet = null;

            foreach (PipelineNet net in AdjacentXNets)
            {
                foreach (CompStorageTank storage in net.OilStorage)
                {
                    if (storage.space > 0f && !storage.DrainTank)
                    {
                        PendingXNetsSupply.Enqueue(net);

                        if (SupplyNetCounter >= 2)
                        {
                            // Network in Queue has become stale.
                            PendingXNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                        else if (PendingXNetsSupply.Count > 0 && PendingXNetsSupply.Peek() == net && !alreadySupplied)
                        {
                            alreadySupplied = true;
                            PendingXNetsSupply.Dequeue();
                            SupplyNetCounter = 0;
                            break;
                        }
                    }
                }
            }
        }

        protected override float TryPush(PipelineNet net, float amount)
        {
            return (float)net.PushCrude(amount);
        }

        private void StartOilSpill(float spilledAmount)
        {
            if (!Shuttle.OilConnectionAt.IsValid)
                return;

            MapComponent_Rimefeller comp = Shuttle.parent.Map.Rimefeller();

            float current = comp.OilSpillGrid.ValueAt(Shuttle.OilConnectionAt);

            comp.OilSpillGrid.SetAt(Shuttle.OilConnectionAt, current + spilledAmount);
        }

        public static void Prefix(PipelineNet __instance, out PushCrudeState __state)
        {
            __state = new PushCrudeState();

            RimefellerIntegration? integration = null;

            foreach (RimefellerIntegration instance in Instances)
            {
                if (instance.AdjacentXNets.Contains(__instance))
                {
                    integration = instance;
                    break;
                }
            }

            if (integration == null)
                return;

            TankState? tank = integration.Shuttle.GetTankForReceive(StoredType.Oil);

            if (tank == null || tank.IsLocked)
                return;

            __state.Instance = __instance;
            __state.Tank = tank;
            __state.Shuttle = integration.Shuttle;
            __state.Integration = integration;
        }

        public static void Postfix(PushCrudeState __state, ref double __result)
        {
            if (__state.Instance == null || __state.Tank == null || __state.Shuttle == null || __state.Integration == null)
                return;

            if (__result <= 0.0)
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

            double freeCapacity = __state.Tank.TankCapacity - __state.Tank.TankStorage;

            if (freeCapacity <= 0.0)
                return;

            double accepted = Math.Min(__result, Math.Min(freeCapacity, __state.Tank.ReceiveAllowance));

            if (accepted <= 0.0)
                return;

            __state.Tank.Content = StoredType.Oil;
            __state.Tank.TankStorage += (float)accepted;
            __state.Tank.ReceiveAllowance -= accepted;
            __state.Tank.IsContaminated = true;

            MassPatch.NotifyLiquidMassChanged(__state.Shuttle);

            __result -= accepted;
        }
    }
}
