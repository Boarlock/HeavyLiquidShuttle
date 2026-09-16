using DubsBadHygiene;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CargoShuttle
{
    public class ShuttleWater
    {
        public static PlumbingNet? CheckCellsAroundShuttle(Thing shuttle)
        {
            if (shuttle == null)
                return null;

            CompCargoShuttle shuttleComp = shuttle.TryGetComp<CompCargoShuttle>();

            if (shuttleComp == null)
                return null;

            // Make sure the shuttle is on a non-null worldspace currently.
            Map map = shuttle.Map;

            if (map == null)
                return null;

            HashSet<IntVec3> adjacentTilesSet = new HashSet<IntVec3>();

            // Get the cells adjacent to the shuttle.
            foreach (IntVec3 shuttleCell in shuttle.OccupiedRect())
            {
                foreach (IntVec3 adjCell in GenAdjFast.AdjacentCells8Way(shuttleCell))
                {
                    adjacentTilesSet.Add(adjCell);
                }
            }

            // Remove the shuttle cells themselves from adjacent cell list.
            foreach (IntVec3 shuttleCell in shuttle.OccupiedRect())
            {
                adjacentTilesSet.Remove(shuttleCell);
            }

            PlumbingNet net = null!;

            foreach (IntVec3 adjTile in adjacentTilesSet)
            {
                if (!adjTile.InBounds(map))
                    continue;

                foreach (Thing thing in map.thingGrid.ThingsAt(adjTile))
                {
                    CompPipe pipe = thing.TryGetComp<CompPipe>();

                    if (pipe == null)
                        continue;

                    net = pipe.pipeNet;
                    break;
                }

                if (net != null)
                    return net;
            }
            return null;
        }

        public static void UnloadWater(Thing shuttle)
        {
            if (shuttle == null)
                return;

            CompCargoShuttle shuttleComp = shuttle.TryGetComp<CompCargoShuttle>();

            if (shuttleComp == null || shuttleComp.AdjacentPlumbingNet == null)
                return;

            float amountToTransfer = shuttleComp.WaterStorage;

            if (amountToTransfer <= 0f)
                return;

            float remaining = shuttleComp.AdjacentPlumbingNet.PushWater(amountToTransfer);
            float transferred = amountToTransfer - remaining;

            if (transferred <= 0f)
                return;

            shuttleComp.WaterStorage -= transferred;
        }
    }
}
