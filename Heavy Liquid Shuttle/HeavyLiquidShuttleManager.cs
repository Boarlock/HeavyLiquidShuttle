using System;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class HeavyLiquidShuttleGameComp : GameComponent
    {
        public HeavyLiquidShuttleGameComp(Game game) { }

        public static event Action? TickIntegration;
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 10 != 0)
                return;

            TickIntegration?.Invoke();
        }
    }

    public enum StoredType
    {
        Empty,
        Water,
        Oil,
        Deepchem,
        Helixien
    }

    public class TankState
    {
        public float TankCapacity = 1250f;
        public float TankStorage = 0f;

        public int Counter = 0;
        public int Counter2 = 0;
        public bool IsLocked;
        public bool TransferEnabled;
        public bool IsContaminated = false;
        public bool IsTransferringFluid;
        public double ReceiveAllowance = 1.0;
        public StoredType Content = StoredType.Empty;
    }

    public static class HeavyLiquidShuttleManager 
    {
        internal static bool IsInitialized = false;

        // Mass conversions for liquids and gas.
        public const float CrudeMassPerLiter = 0.85f;
        public const float DeepchemMassPerLiter = 1.2f;
        public const float HelixienMassPerLiter = 0.2f;

        internal static void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!IsInitialized)
                return;

            if (hasFocus)
                return;

            Log.Message("[HeavyLiquidShuttle] Application lost focus. Halting transfers.");

            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    HeavyLiquidShuttle? shuttle = thing.TryGetComp<HeavyLiquidShuttle>();

                    if (shuttle == null)
                        continue;

                    shuttle.TankA.TransferEnabled = false;
                    shuttle.TankA.IsTransferringFluid = false;
                    shuttle.TankA.ReceiveAllowance = 1.0;
                    shuttle.TankA.Counter = 0;
                    shuttle.TankA.Counter2 = 0;

                    shuttle.TankB.TransferEnabled = false;
                    shuttle.TankB.IsTransferringFluid = false;
                    shuttle.TankB.ReceiveAllowance = 1.0;
                    shuttle.TankB.Counter = 0;
                    shuttle.TankA.Counter2 = 0;
                }
            }
        }
    }
}
