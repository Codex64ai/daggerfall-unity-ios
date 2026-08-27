// Project:         Daggerfall Unity iOS touch port
// License:         MIT License (LICENSE file)
//
// Road network queries and routing, over the path data ported from Basic Roads.
//
// Basic Roads is Copyright (C) 2020 Hazelnut, MIT licensed - see BasicRoadsTexturing.cs. The
// direction bitmask and the map-pixel offsets for each direction are its format, taken from
// BasicRoads and Travel Options so the data is read exactly as it was authored.
//
// The ROUTING here is this port's own. Travel Options follows a road the player is standing on,
// one map pixel at a time, and offers a junction map so the player chooses at forks. That is a
// different feature from "walk to Daggerfall and use the roads on the way", which needs a route
// worked out in advance - so that part is written rather than ported.

using System.Collections.Generic;
using DaggerfallConnect.Arena2;
using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop.Game.Mobile
{
    /// <summary>
    /// The Iliac Bay's road and track network as a graph, and A* over it.
    /// </summary>
    public static class MobileRoadNetwork
    {
        // Direction bits, Basic Roads' format. Note NORTH IS Y-1: Daggerfall map pixel Y grows
        // southward, so getting this backwards routes every journey the wrong way.
        public const byte N = 128;
        public const byte NE = 64;
        public const byte E = 32;
        public const byte SE = 16;
        public const byte S = 8;
        public const byte SW = 4;
        public const byte W = 2;
        public const byte NW = 1;

        static readonly byte[] dirBits = { N, NE, E, SE, S, SW, W, NW };
        static readonly int[] dirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] dirY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        public const int Width = MapsFile.MaxMapPixelX;   // 1000
        public const int Height = MapsFile.MaxMapPixelY;  // 500

        // A road is preferred over a track of the same length. Roads are the maintained routes
        // and read as the sensible way to travel; without a preference a route will happily
        // take a muddy track that happens to be one pixel shorter.
        const float roadCost = 1.0f;
        const float trackCost = 1.6f;
        const float diagonalScale = 1.41421356f;

        // Routing through a settlement is expensive, because a journey steers a straight bearing
        // and a town is full of buildings to walk into - the reported failure was the player
        // stuck behind one. This does not forbid it: a road genuinely runs through many towns,
        // and the destination is usually a town itself. It just means a route will take a way
        // around when one exists at anything like a comparable length.
        const float settlementPenalty = 8.0f;

        // A route across the whole map is a few hundred pixels; anything far beyond that means
        // the search is wandering and should be abandoned rather than stalling the game.
        const int maxNodesExplored = 250000;

        static byte[] roadData;
        static byte[] trackData;
        static bool loaded;

        /// <summary>True when path data is present and routing can be attempted.</summary>
        public static bool Available
        {
            get
            {
                EnsureLoaded();
                return roadData != null && trackData != null;
            }
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;

            // Read straight from the texturing instance if roads were installed, so there is
            // one copy of the data rather than two. Falls back to loading it directly, which
            // keeps routing usable for a caller that wants routes without road rendering.
            BasicRoads.BasicRoadsTexturing texturing =
                DaggerfallUnity.Instance.TerrainTexturing as BasicRoads.BasicRoadsTexturing;

            if (texturing != null)
            {
                roadData = texturing.GetPathData(BasicRoads.BasicRoadsTexturing.roads);
                trackData = texturing.GetPathData(BasicRoads.BasicRoadsTexturing.tracks);
                return;
            }

            roadData = LoadResource("roadData");
            trackData = LoadResource("trackData");
        }

        static byte[] LoadResource(string name)
        {
            UnityEngine.TextAsset asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(
                BasicRoads.BasicRoadsTexturing.ResourceFolder + name);

            if (asset == null || asset.bytes == null || asset.bytes.Length != Width * Height)
                return null;

            return asset.bytes;
        }

        public static bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>Direction bits for roads at this map pixel.</summary>
        public static byte RoadsAt(int x, int y)
        {
            EnsureLoaded();
            return (roadData != null && InBounds(x, y)) ? roadData[y * Width + x] : (byte)0;
        }

        /// <summary>Direction bits for tracks at this map pixel.</summary>
        public static byte TracksAt(int x, int y)
        {
            EnsureLoaded();
            return (trackData != null && InBounds(x, y)) ? trackData[y * Width + x] : (byte)0;
        }

        /// <summary>Roads and tracks combined - anything walkable as a path.</summary>
        public static byte PathsAt(int x, int y)
        {
            return (byte)(RoadsAt(x, y) | TracksAt(x, y));
        }

        /// <summary>
        /// Does this map pixel hold a town, village, hamlet or tavern?
        ///
        /// Asked of the map data rather than tracked here, and deliberately tolerant: a failure
        /// to read location data must not break routing, so anything unexpected is treated as
        /// open country and simply routed through as before.
        /// </summary>
        public static bool IsSettlement(int x, int y)
        {
            // Memoised. A* asks this for every neighbour of every node it opens, so a route can
            // want tens of thousands of answers - and each uncached one is a map data lookup.
            // Without this the settlement penalty would trade a stuck player for a visible
            // pause whenever a journey starts.
            int key = y * Width + x;
            bool known;
            if (settlementCache.TryGetValue(key, out known))
                return known;

            bool result = LookupSettlement(x, y);
            settlementCache[key] = result;
            return result;
        }

        static readonly Dictionary<int, bool> settlementCache = new Dictionary<int, bool>();

        static bool LookupSettlement(int x, int y)
        {
            try
            {
                ContentReader.MapSummary summary;
                if (!DaggerfallUnity.Instance.ContentReader.HasLocation(x, y, out summary))
                    return false;

                return summary.LocationType == DFRegion.LocationTypes.TownCity ||
                       summary.LocationType == DFRegion.LocationTypes.TownHamlet ||
                       summary.LocationType == DFRegion.LocationTypes.TownVillage ||
                       summary.LocationType == DFRegion.LocationTypes.Tavern;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasAnyPath(int x, int y)
        {
            return PathsAt(x, y) != 0;
        }

        /// <summary>
        /// Route from one map pixel to another along roads and tracks, or null if there is no
        /// connected route. The returned list starts at the pixel after <paramref name="fromX"/>
        /// and ends at the destination.
        ///
        /// A* over a 1000x500 grid: half a million nodes at worst, which sounds large and is
        /// not - the search is bounded by the heuristic and typically touches a few thousand.
        /// It runs once when a journey starts, not per frame.
        /// </summary>
        public static List<DFPosition> FindRoute(int fromX, int fromY, int toX, int toY)
        {
            EnsureLoaded();

            if (roadData == null || !InBounds(fromX, fromY) || !InBounds(toX, toY))
                return null;

            if (fromX == toX && fromY == toY)
                return new List<DFPosition>();

            int start = fromY * Width + fromX;
            int goal = toY * Width + toX;

            // Dictionaries rather than half-million-entry arrays: a route touches a small
            // fraction of the map, and allocating 500k floats per journey on a phone to hold
            // mostly infinity is the wrong trade.
            Dictionary<int, float> best = new Dictionary<int, float>();
            Dictionary<int, int> cameFrom = new Dictionary<int, int>();
            SimpleQueue open = new SimpleQueue();

            best[start] = 0f;
            open.Push(start, Heuristic(fromX, fromY, toX, toY));

            int explored = 0;

            while (open.Count > 0)
            {
                int current = open.Pop();

                if (current == goal)
                    return Rebuild(cameFrom, start, goal);

                if (++explored > maxNodesExplored)
                    return null;

                int cx = current % Width;
                int cy = current / Width;

                byte roads = RoadsAt(cx, cy);
                byte tracks = TracksAt(cx, cy);
                byte paths = (byte)(roads | tracks);
                if (paths == 0)
                    continue;

                float currentCost = best[current];

                for (int d = 0; d < 8; d++)
                {
                    if ((paths & dirBits[d]) == 0)
                        continue;

                    int nx = cx + dirX[d];
                    int ny = cy + dirY[d];
                    if (!InBounds(nx, ny))
                        continue;

                    // Prefer roads, and charge diagonals their true length so a route does not
                    // zig-zag to save a nominal step.
                    bool onRoad = (roads & dirBits[d]) != 0;
                    float step = onRoad ? roadCost : trackCost;
                    if (dirX[d] != 0 && dirY[d] != 0)
                        step *= diagonalScale;

                    // Prefer to pass settlements rather than through them - except the one we
                    // are heading for, which must stay reachable at normal cost.
                    if ((ny * Width + nx) != goal && IsSettlement(nx, ny))
                        step += settlementPenalty;

                    int neighbour = ny * Width + nx;
                    float tentative = currentCost + step;

                    float known;
                    if (best.TryGetValue(neighbour, out known) && known <= tentative)
                        continue;

                    best[neighbour] = tentative;
                    cameFrom[neighbour] = current;
                    open.Push(neighbour, tentative + Heuristic(nx, ny, toX, toY));
                }
            }

            return null;
        }

        /// <summary>
        /// Octile distance, scaled by the cheapest possible step so it never overestimates -
        /// an inadmissible heuristic here would quietly return routes that are not shortest.
        /// </summary>
        static float Heuristic(int x, int y, int toX, int toY)
        {
            int dx = x > toX ? x - toX : toX - x;
            int dy = y > toY ? y - toY : toY - y;
            int lo = dx < dy ? dx : dy;
            int hi = dx < dy ? dy : dx;
            return roadCost * ((hi - lo) + diagonalScale * lo);
        }

        static List<DFPosition> Rebuild(Dictionary<int, int> cameFrom, int start, int goal)
        {
            List<DFPosition> route = new List<DFPosition>();
            int node = goal;

            while (node != start)
            {
                route.Add(new DFPosition(node % Width, node / Width));

                int prev;
                if (!cameFrom.TryGetValue(node, out prev))
                    return null;      // broken chain; better no route than a wrong one

                node = prev;
            }

            route.Reverse();
            return route;
        }

        /// <summary>
        /// Nearest map pixel carrying any path, within a small radius. Journeys rarely start or
        /// end exactly on a road, so both ends have to be snapped to the network before a route
        /// can be found at all.
        /// </summary>
        public static DFPosition NearestPathPixel(int x, int y, int maxRadius)
        {
            EnsureLoaded();

            if (roadData == null)
                return null;

            if (HasAnyPath(x, y))
                return new DFPosition(x, y);

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        // Ring only - the interior was covered by a smaller radius, so this
                        // finds the genuinely nearest rather than merely a near one.
                        if (dx > -r && dx < r && dy > -r && dy < r)
                            continue;

                        int nx = x + dx;
                        int ny = y + dy;
                        if (InBounds(nx, ny) && HasAnyPath(nx, ny))
                            return new DFPosition(nx, ny);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Binary heap. Sorting a list on every push, at a few thousand pushes per route, is
        /// the difference between a routing pause the player notices and one they do not.
        /// </summary>
        class SimpleQueue
        {
            readonly List<int> nodes = new List<int>();
            readonly List<float> costs = new List<float>();

            public int Count { get { return nodes.Count; } }

            public void Push(int node, float cost)
            {
                nodes.Add(node);
                costs.Add(cost);

                int i = nodes.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (costs[parent] <= costs[i])
                        break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public int Pop()
            {
                int result = nodes[0];
                int last = nodes.Count - 1;

                nodes[0] = nodes[last];
                costs[0] = costs[last];
                nodes.RemoveAt(last);
                costs.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, smallest = i;
                    if (l < nodes.Count && costs[l] < costs[smallest]) smallest = l;
                    if (r < nodes.Count && costs[r] < costs[smallest]) smallest = r;
                    if (smallest == i)
                        break;
                    Swap(smallest, i);
                    i = smallest;
                }

                return result;
            }

            void Swap(int a, int b)
            {
                int n = nodes[a]; nodes[a] = nodes[b]; nodes[b] = n;
                float c = costs[a]; costs[a] = costs[b]; costs[b] = c;
            }
        }
    }
}
