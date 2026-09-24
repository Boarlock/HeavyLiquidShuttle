using System;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class HeavyLiquidShuttleGameComponent : GameComponent
    {
        public HeavyLiquidShuttleGameComponent(Game game) {  }

        public static event Action? TickIntegration;
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 10 != 0)
                return;

            TickIntegration?.Invoke();
        }
    }
}
