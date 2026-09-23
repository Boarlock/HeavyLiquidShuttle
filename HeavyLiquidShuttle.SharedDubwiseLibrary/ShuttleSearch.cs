using Rimefeller;
using DubsBadHygiene;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleSearch
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out HashSet<PlumbingNet> waterNets, out HashSet<PipelineNet> oilNets)
        {
            waterNets = new HashSet<PlumbingNet>();
            oilNets = new HashSet<PipelineNet>();

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
                    DubsBadHygiene.CompPipe waterPipe = thing.TryGetComp<DubsBadHygiene.CompPipe>();
                    Rimefeller.CompPipe oilPipe = thing.TryGetComp<Rimefeller.CompPipe>();

                    if (waterPipe?.pipeNet != null)
                    {
                        waterNets.Add(waterPipe.pipeNet);
                    }

                    if (oilPipe?.pipeNet != null)
                    {
                        oilNets.Add(oilPipe.pipeNet);
                        shuttle.OilConnectionAt = adjTile;

                    }
                    
                }
            }
        }
    }
}
