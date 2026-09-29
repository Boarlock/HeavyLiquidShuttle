using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class HeavyLiquidShuttleGameComp : GameComponent
    {
        public HeavyLiquidShuttleGameComp(Game game) { }

        private Stopwatch stopwatch = new Stopwatch();
        public static event Action? TickIntegration;
        public override void GameComponentTick()
        {
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
                stopwatch.Stop();
            else
                stopwatch.Start();

            if (stopwatch.ElapsedMilliseconds >= 166.67)
            {
                stopwatch.Restart();
                TickIntegration?.Invoke();
            }
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

        public bool IsLocked;
        public bool TransferEnabled;
        public bool IsContaminated = false;

        public float SupplyAllowance = 1f;
        public float ReceiveAllowance = 1f;

        public StoredType Content = StoredType.Empty;
        public WaterState WaterQuality = WaterState.Untreated;
        public HelixienState TankExplosiveness = HelixienState.None;

        public enum WaterState
        {
            Treated,
            Untreated,
            Contaminated
        }

        public enum HelixienState
        {
            None,
            Low,
            Moderate,
            High
        }

        public HelixienState GetHelixienState(out bool stateChanged)
        {
            HelixienState oldState = TankExplosiveness;
            HelixienState newState;
            stateChanged = false;

            if (TankStorage < 100f)
                newState = HelixienState.None;

            else if (TankStorage < 250f)
                newState = HelixienState.Low;

            else if (TankStorage < 500f)
                newState = HelixienState.Moderate;

            else
                newState = HelixienState.High;

            if (oldState != newState)
                stateChanged = true;

            return newState;
        }       
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
            if (!IsInitialized || Current.ProgramState != ProgramState.Playing)
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
                    shuttle.TankB.TransferEnabled = false;
                }
            }
        }
    }
}
