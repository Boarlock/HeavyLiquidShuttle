using Rimefeller;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleOilSearch
    {
        public static HashSet<PipelineNet> CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle)
        {
            return ShuttleSearch<PipelineNet>.CheckCellsAroundShuttle(
                shuttle, 
                (thing, cell) =>
            {
                CompPipe? pipe = thing.TryGetComp<CompPipe>();

                return pipe?.pipeNet;
            });
        }
    }
}
