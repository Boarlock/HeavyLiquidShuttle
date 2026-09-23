/*using PipeSystem;
using Verse;
using System.Collections.Generic;

namespace HeavyLiquidShuttleMod
{
    public class ShuttleVESearch
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out HashSet<PipeNet> deepchemNets, out HashSet<PipeNet> helixienNets)
        {
            deepchemNets = new HashSet<PipeNet>();
            helixienNets = new HashSet<PipeNet>();

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
                    CompResource pipe = thing.TryGetComp<CompResource>();

                    if (pipe == null || pipe.PipeNet == null)
                        continue;

                    if (pipe.Resource.name == "Deepchem")
                    {
                        deepchemNets.Add(pipe.PipeNet);
                    }
                    else if (pipe.Resource.name == "Helixien gas")
                    {
                        helixienNets.Add(pipe.PipeNet);
                    }

                    break;
                }
            }
        }
    }
}*/
