using System;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class ShuttleSearch<TNetwork1>
        where TNetwork1 : class
    {
        public static HashSet<TNetwork1> CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, Func<Thing, IntVec3, TNetwork1?> examineThing)
        {
            HashSet<TNetwork1> nets = new HashSet<TNetwork1>();

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
                adjacentTilesSet.Remove(shuttleCell);

            foreach (IntVec3 adjTile in adjacentTilesSet)
            {
                if (!adjTile.InBounds(map))
                    continue;

                foreach (Thing thing in map.thingGrid.ThingsAt(adjTile))
                {
                    TNetwork1? net = examineThing(thing, adjTile);

                    if (net != null)
                        nets.Add(net);
                }
            }
            return nets;
        }
    }

    public static class ShuttleSearchTripleOut<TNetwork1>
        where TNetwork1 : class
    {
        public static void CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle, out HashSet<TNetwork1> xNets, out HashSet<TNetwork1> yNets, out HashSet<TNetwork1> zNets, Func<Thing, IntVec3, TNetwork1?> examineX, Func<Thing, IntVec3, TNetwork1?> examineY, Func<Thing, IntVec3, TNetwork1?> examineZ)
        {
            xNets = new HashSet<TNetwork1>();
            yNets = new HashSet<TNetwork1>();
            zNets = new HashSet<TNetwork1>();

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

                    TNetwork1? netX = examineX(thing, adjTile);

                    if (netX != null)
                        xNets.Add(netX);

                    TNetwork1? netY = examineY(thing, adjTile);

                    if (netY != null)
                        yNets.Add(netY);

                    TNetwork1? netZ = examineZ(thing, adjTile);

                    if (netZ != null)
                        zNets.Add(netZ);
                }
            }
        }
    }
}
