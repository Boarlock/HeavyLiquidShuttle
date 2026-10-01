using PipeSystem;
using Verse;
using System.Collections.Generic;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleVESearch
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out HashSet<PipeNet> xNets, out HashSet<PipeNet> yNets, out HashSet<PipeNet> zNets)
        {
            ShuttleSearchDouble<PipeNet, PipeNet, PipeNet>.CheckCellsAroundShuttle(
                shuttle,
                out xNets,
                out yNets,
                out zNets,
                (thing, _) =>
                {
                    CompResource? pipe = thing.TryGetComp<CompResource>();

                    if (pipe?.PipeNet == null)
                        return null;

                    if (pipe.Resource.name != "Deepchem")
                        return null;

                    return pipe?.PipeNet;
                },
                (thing, _) =>
                {
                    CompResource? pipe = thing.TryGetComp<CompResource>();

                    if (pipe?.PipeNet == null)
                        return null;

                    if (pipe.Resource.name != "Helixien gas")
                        return null;

                    return pipe?.PipeNet;
                },
                (thing, _) =>
                {
                    CompResource? pipe = thing.TryGetComp<CompResource>();

                    if (pipe?.PipeNet == null)
                        return null;

                    if (pipe.Resource.name != "Scarlet sludge")
                        return null;

                    return pipe?.PipeNet;
                });


        }
    }
}
