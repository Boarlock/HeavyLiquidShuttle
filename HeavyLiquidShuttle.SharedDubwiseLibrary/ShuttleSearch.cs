using Rimefeller;
using DubsBadHygiene;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleSearch
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out HashSet<PlumbingNet> xNets, out HashSet<PipelineNet> yNets)
        {
            ShuttleSearchDouble<PlumbingNet, PipelineNet>.CheckCellsAroundShuttle(
                shuttle,
                out xNets,
                out yNets,
                (thing, _) =>
                {
                    DubsBadHygiene.CompPipe? pipe = thing.TryGetComp<DubsBadHygiene.CompPipe>();

                    return pipe?.pipeNet;
                },
                (thing, cell) =>
                {
                    Rimefeller.CompPipe? pipe = thing.TryGetComp<Rimefeller.CompPipe>();

                    return pipe?.pipeNet;
                });

        }
    }
}
