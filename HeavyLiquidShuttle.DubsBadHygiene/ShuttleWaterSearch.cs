using DubsBadHygiene;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleWaterSearch
    {
        public static HashSet<PlumbingNet> CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle)
        {
            return ShuttleSearch<PlumbingNet>.CheckCellsAroundShuttle(
                shuttle,
                thing =>
                {
                    CompPipe? pipe = thing.TryGetComp<CompPipe>();

                    return pipe?.pipeNet;
                });

        }
    }
}
