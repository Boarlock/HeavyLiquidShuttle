using System;
using System.Diagnostics;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class HeavyLiquidShuttleManager
    {
        internal static bool IsInitialized = false;

        internal static void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!IsInitialized || Current.ProgramState != ProgramState.Playing)
                return;

            if (hasFocus)
                return;

            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    HeavyLiquidShuttle? shuttle = thing.TryGetComp<HeavyLiquidShuttle>();

                    if (shuttle == null)
                        continue;

                    shuttle.TankA.transferEnabled = false;
                    shuttle.TankB.transferEnabled = false;
                }
            }
        }
    }

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
}
