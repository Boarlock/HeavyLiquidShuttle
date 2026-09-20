/*using Rimefeller;
using DubsBadHygiene;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleSearch
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out PlumbingNet plumbingNet, out PipelineNet pipelineNet)
        {
            plumbingNet = null;
            pipelineNet = null;

            if (shuttle == null)
                return;

            // Make sure the shuttle is on a non-null worldspace currently.
            Map map = shuttle.parent.Map;

            if (map == null)
                return;

            HashSet<IntVec3> adjacentTilesSet = new HashSet<IntVec3>();

            // Get the cells adjacent to the shuttle.
            foreach (IntVec3 shuttleCell in shuttle.parent.OccupiedRect())
            {
                foreach (IntVec3 adjCell in GenAdjFast.AdjacentCells8Way(shuttleCell))
                {
                    adjacentTilesSet.Add(adjCell);
                }
            }

            // Remove the shuttle cells themselves from adjacent cell list.
            foreach (IntVec3 shuttleCell in shuttle.parent.OccupiedRect())
            {
                adjacentTilesSet.Remove(shuttleCell);
            }

            foreach (IntVec3 adjTile in adjacentTilesSet)
            {
                if (!adjTile.InBounds(map))
                    continue;

                foreach (Thing thing in map.thingGrid.ThingsAt(adjTile))
                {
                    // DBH
                    if (plumbingNet == null)
                    {
                        DubsBadHygiene.CompPipe waterPipe = thing.TryGetComp<DubsBadHygiene.CompPipe>();

                        if (waterPipe != null)
                            plumbingNet = waterPipe.pipeNet;
                    }
                    // Rimefeller
                    if (pipelineNet == null)
                    {
                        Rimefeller.CompPipe oilPipe = thing.TryGetComp<Rimefeller.CompPipe>();

                        if (oilPipe != null)
                            pipelineNet = oilPipe.pipeNet;
                    }

                    // We found everything we're looking for.
                    if (plumbingNet != null && pipelineNet != null)
                        return;
                }
            }
        }

        public static void UnloadWater(Thing shuttle)
        {
            if (shuttle == null)
                return;

            HeavyLiquidShuttle shuttleComp = shuttle.TryGetComp<HeavyLiquidShuttle>();

            if (shuttleComp == null)
                return;

            if (!DubsBadHygieneIntegration.AdjacentNetworks.TryGetValue(shuttleComp, out var net))
                return;

            float amountToTransfer = shuttleComp.WaterStorage;

            if (amountToTransfer <= 0f)
                return;

            float remaining = net.PushWater(amountToTransfer);
            float transferred = amountToTransfer - remaining;

            if (transferred <= 0f)
                return;

            shuttleComp.WaterStorage -= transferred;

            MassPatch.NotifyLiquidMassChanged(shuttleComp);
        }


        public static void UnloadCrude(Thing shuttle)
        {
            if (shuttle == null)
                return;

            HeavyLiquidShuttle shuttleComp =
                shuttle.TryGetComp<HeavyLiquidShuttle>();

            if (shuttleComp == null)
                return;

            if (!RimefellerIntegration.AdjacentNetworks.TryGetValue(shuttleComp, out var net))
                return;

            float amountToTransfer = shuttleComp.CrudeStorage;

            if (amountToTransfer <= 0f)
                return;

            double remaining = net.PushCrude(amountToTransfer);
            double transferred = amountToTransfer - remaining;

            if (transferred <= 0)
                return;

            shuttleComp.CrudeStorage -= (float)transferred;

            MassPatch.NotifyLiquidMassChanged(shuttleComp);
        }
    }
}*/
