using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EScooterSim
{
    /// <summary>
    /// Builds the NavMesh when the scene starts, straight from the numbers in
    /// <see cref="RoadLayout"/>, so nothing has to be baked by hand in the Editor.
    ///
    /// The surface is split into three NavMesh areas: road, sidewalk and crosswalk.
    /// Vehicles and the scooter may only use road and crosswalk. Pedestrians may only use
    /// sidewalk and crosswalk. Each agent type therefore stays on the surface it belongs to.
    /// </summary>
    public static class NavMeshBaker
    {
        private const float SlabThickness = 0.2f;

        private static NavMeshData data;
        private static NavMeshDataInstance instance;

        public static bool IsBuilt { get; private set; }

        public static bool Build()
        {
            Clear();

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            float reach = RoadLayout.ArmLength;
            float half = RoadLayout.RoadHalfWidth;
            float walk = RoadLayout.SidewalkWidth;

            // The two roads. They overlap in the middle, which is fine: same area.
            AddSlab(sources, Vector3.zero, 2f * reach, 2f * half, RoadLayout.AreaRoad);
            AddSlab(sources, Vector3.zero, 2f * half, 2f * reach, RoadLayout.AreaRoad);

            // Sidewalks: one strip on each side of each of the four arms.
            float armLength = reach - half;
            float armMiddle = half + armLength * 0.5f;
            float sideMiddle = half + walk * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddSlab(sources, new Vector3(sx * armMiddle, 0f, sz * sideMiddle), armLength, walk, RoadLayout.AreaSidewalk);
                    AddSlab(sources, new Vector3(sx * sideMiddle, 0f, sz * armMiddle), walk, armLength, RoadLayout.AreaSidewalk);
                }
            }

            // Crosswalks: volumes that re-label the stretch of road they cover.
            float c = RoadLayout.CrosswalkCenter;
            float width = RoadLayout.CrosswalkWidth;
            float span = 2f * half + 0.6f;
            AddAreaVolume(sources, new Vector3(c, 0f, 0f), width, span, RoadLayout.AreaCrosswalk);
            AddAreaVolume(sources, new Vector3(-c, 0f, 0f), width, span, RoadLayout.AreaCrosswalk);
            AddAreaVolume(sources, new Vector3(0f, 0f, c), span, width, RoadLayout.AreaCrosswalk);
            AddAreaVolume(sources, new Vector3(0f, 0f, -c), span, width, RoadLayout.AreaCrosswalk);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(2f * reach + 20f, 10f, 2f * reach + 20f));
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                IsBuilt = false;
                return false;
            }

            instance = NavMesh.AddNavMeshData(data);
            IsBuilt = instance.valid;
            return IsBuilt;
        }

        public static void Clear()
        {
            if (instance.valid) NavMesh.RemoveNavMeshData(instance);
            instance = new NavMeshDataInstance();
            data = null;
            IsBuilt = false;
        }

        /// <summary>A flat walkable slab whose top face is at ground level.</summary>
        private static void AddSlab(List<NavMeshBuildSource> sources, Vector3 center, float sizeX, float sizeZ, int area)
        {
            NavMeshBuildSource source = new NavMeshBuildSource();
            source.shape = NavMeshBuildSourceShape.Box;
            source.size = new Vector3(sizeX, SlabThickness, sizeZ);
            source.transform = Matrix4x4.TRS(
                new Vector3(center.x, -SlabThickness * 0.5f, center.z), Quaternion.identity, Vector3.one);
            source.area = area;
            sources.Add(source);
        }

        /// <summary>A volume that changes the area type of whatever walkable surface is inside it.</summary>
        private static void AddAreaVolume(List<NavMeshBuildSource> sources, Vector3 center, float sizeX, float sizeZ, int area)
        {
            NavMeshBuildSource source = new NavMeshBuildSource();
            source.shape = NavMeshBuildSourceShape.ModifierBox;
            source.size = new Vector3(sizeX, 2f, sizeZ);
            source.transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
            source.area = area;
            sources.Add(source);
        }
    }

    /// <summary>
    /// Turns a list of waypoints into the path an agent follows, by asking the NavMesh
    /// for the way from each waypoint to the next within the areas the agent may use.
    /// If the NavMesh cannot give a complete path for a leg, that leg falls back to a
    /// straight line and the fallback is counted, so a NavMesh problem shows up as a
    /// number rather than as agents refusing to move.
    /// </summary>
    public static class NavMeshRoutePlanner
    {
        private const float SnapDistance = 1.5f;

        private static NavMeshPath scratch;

        public static int LegsPlanned { get; private set; }
        public static int Fallbacks { get; private set; }

        public static void ResetCounters()
        {
            LegsPlanned = 0;
            Fallbacks = 0;
        }

        public static RoutePath BuildPath(IList<Vector3> waypoints, int areaMask)
        {
            List<Vector3> points = new List<Vector3>();
            if (waypoints.Count > 0) points.Add(waypoints[0]);
            for (int i = 1; i < waypoints.Count; i++)
            {
                AppendLeg(points, waypoints[i - 1], waypoints[i], areaMask);
            }
            return new RoutePath(points);
        }

        private static void AppendLeg(List<Vector3> points, Vector3 from, Vector3 to, int areaMask)
        {
            LegsPlanned++;

            if (NavMeshBaker.IsBuilt)
            {
                if (scratch == null) scratch = new NavMeshPath();

                NavMeshHit startHit;
                NavMeshHit endHit;
                if (NavMesh.SamplePosition(from, out startHit, SnapDistance, areaMask)
                    && NavMesh.SamplePosition(to, out endHit, SnapDistance, areaMask)
                    && NavMesh.CalculatePath(startHit.position, endHit.position, areaMask, scratch)
                    && scratch.status == NavMeshPathStatus.PathComplete)
                {
                    Vector3[] corners = scratch.corners;
                    if (corners.Length >= 2)
                    {
                        // Keep the exact waypoint as the end of the leg and take any corners
                        // the NavMesh added in between.
                        for (int i = 1; i < corners.Length - 1; i++) points.Add(corners[i]);
                        points.Add(to);
                        return;
                    }
                }
            }

            Fallbacks++;
            points.Add(to);
        }
    }
}
