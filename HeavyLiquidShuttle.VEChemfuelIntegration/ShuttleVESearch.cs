using PipeSystem;
using System.Collections.Generic;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public static class ShuttleVESearch
    {
        public static Dictionary<StoredTypeDef, HashSet<PipeNet>> CheckCellsAroundShuttle(HeavyLiquidShuttle shuttle)
        {
            Dictionary<StoredTypeDef, HashSet<PipeNet>> nets = new Dictionary<StoredTypeDef, HashSet<PipeNet>>();

            if (shuttle == null)
                return nets;

            Map map = shuttle.parent.Map;

            if (map == null)
                return nets;

            HashSet<IntVec3> adjacentTilesSet = new HashSet<IntVec3>();

            foreach (IntVec3 shuttleCell in shuttle.parent.OccupiedRect())
            {
                foreach (IntVec3 adjacentCell in GenAdjFast.AdjacentCells8Way(shuttleCell))
                    adjacentTilesSet.Add(adjacentCell);
            }

            foreach (IntVec3 shuttleCell in shuttle.parent.OccupiedRect())
                adjacentTilesSet.Remove(shuttleCell);

            foreach (IntVec3 adjacentTile in adjacentTilesSet)
            {
                if (!adjacentTile.InBounds(map))
                    continue;

                foreach (Thing thing in map.thingGrid.ThingsAt(adjacentTile))
                {
                    CompResource? pipe = thing.TryGetComp<CompResource>();

                    if (pipe?.PipeNet == null)
                        continue;

                    StoredTypeDef? def = GetStoredTypeDef(pipe.PipeNet.def.defName);

                    if (def == null)
                        continue;

                    if (!nets.TryGetValue(def, out HashSet<PipeNet>? resourceNets))
                    {
                        resourceNets = new HashSet<PipeNet>();
                        nets.Add(def, resourceNets);
                    }

                    resourceNets.Add(pipe.PipeNet);
                }
            }

            return nets;
        }

        private static StoredTypeDef? GetStoredTypeDef(string defName)
        {
            switch (defName)
            {
                case "VCHE_DeepchemNet":
                    return CachedDefs.Deepchem;
                case "VCHE_ChemfuelNet":
                    return CachedDefs.Chemfuel;
                case "VHGE_HelixienNet":
                    return CachedDefs.Helixien;
                case "ScarletNet":
                    return CachedDefs.Scarlet;
                case "VGE_OxygenNet":
                    return CachedDefs.Oxygen;
                case "VGE_AstrofuelNet":
                    return CachedDefs.Astrofuel;
                default:
                    return null;
            }
        }
    }
}