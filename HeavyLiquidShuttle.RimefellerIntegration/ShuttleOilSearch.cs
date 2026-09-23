/*using Rimefeller;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleOilSearch
    {
        public static HashSet<PipelineNet> CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle)
        {
            HashSet<PipelineNet> nets = new HashSet<PipelineNet>();

            if (shuttle == null)
                return nets;

            // Make sure the shuttle is on a non-null worldspace currently.
            Map map = shuttle.parent.Map;

            if (map == null)
                return nets;

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
                    CompPipe pipe = thing.TryGetComp<CompPipe>();

                    if (pipe == null)
                        continue;

                    if (pipe.pipeNet != null)
                        nets.Add(pipe.pipeNet);

                    shuttle.OilConnectionAt = adjTile;

                    break;
                }
            }
            return nets;
        }
    }
}*/
