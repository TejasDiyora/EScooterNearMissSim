using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>Direction of travel. Right-hand traffic.</summary>
    public enum Heading { East = 0, North = 1, West = 2, South = 3 }

    /// <summary>Which pair of approaches shares a signal phase.</summary>
    public enum SignalGroup { NorthSouth = 0, EastWest = 1 }

    public enum Maneuver { Straight = 0, RightTurn = 1, Crossing = 2 }

    /// <summary>
    /// A planned trip through the intersection: the waypoints to follow plus the
    /// places along it that matter (stop line, turn, conflict point, kerbs).
    /// </summary>
    public class Route
    {
        public string name;
        public Heading heading;
        public SignalGroup signalGroup;
        public Maneuver maneuver;
        public int areaMask;
        public List<Vector3> waypoints = new List<Vector3>();

        // Key places as world points.
        public Vector3 stopPoint;
        public Vector3 turnStartPoint;
        public Vector3 turnEndPoint;
        public Vector3 conflictPoint;
        public Vector3 crossingEndPoint;
        public Vector3 crossingCenter;

        // The same places as distances along the planned path. Filled in by ComputeDistances.
        public float stopLineDistance;
        public float turnStartDistance = -1f;
        public float turnEndDistance = -1f;
        public float conflictDistance = -1f;
        public float crossingEndDistance = -1f;

        public void ComputeDistances(RoutePath path)
        {
            stopLineDistance = path.DistanceAlongOf(stopPoint);
            if (maneuver == Maneuver.RightTurn)
            {
                turnStartDistance = path.DistanceAlongOf(turnStartPoint);
                turnEndDistance = path.DistanceAlongOf(turnEndPoint);
                conflictDistance = path.DistanceAlongOf(conflictPoint);
            }
            if (maneuver == Maneuver.Crossing)
            {
                crossingEndDistance = path.DistanceAlongOf(crossingEndPoint);
            }
        }
    }

    /// <summary>
    /// Geometry of the four-way intersection. The centre is the world origin.
    /// Each road has, per direction, one vehicle lane next to the centre line and one
    /// bike lane next to the kerb. Sidewalks run outside the kerbs.
    /// </summary>
    public static class RoadLayout
    {
        public const float LaneWidth = 3.5f;
        public const float BikeLaneWidth = 1.5f;
        public const float SidewalkWidth = 3f;
        public const float RoadHalfWidth = LaneWidth + BikeLaneWidth;
        public const float ArmLength = 60f;
        public const float RouteMargin = 2f;
        public const float RouteReach = ArmLength - RouteMargin;

        // Sideways positions, measured from the road's centre line.
        public const float VehicleLaneCenter = LaneWidth * 0.5f;
        public const float BikeLaneCenter = LaneWidth + BikeLaneWidth * 0.5f;
        public const float SidewalkCenter = RoadHalfWidth + SidewalkWidth * 0.5f;

        // Positions along an arm, measured from the intersection centre.
        public const float CrosswalkCenter = SidewalkCenter;
        public const float CrosswalkWidth = SidewalkWidth;
        public const float StopLineFromCenter = RoadHalfWidth + SidewalkWidth + 1f;
        public const float KerbWaitFromCenter = RoadHalfWidth + 1.25f;
        public const float TurnRadius = 4.5f;

        // Body sizes.
        public const float VehicleLength = 4.4f;
        public const float VehicleWidth = 1.8f;
        public const float ScooterLength = 1.2f;
        public const float ScooterWidth = 0.6f;
        public const float PedestrianSize = 0.5f;

        /// <summary>Side gap between a lane-centred vehicle and the scooter in the bike lane.</summary>
        public const float NominalPassingGap =
            BikeLaneCenter - VehicleLaneCenter - VehicleWidth * 0.5f - ScooterWidth * 0.5f;
        public const float MinLaneShift = -0.6f;
        public const float MaxLaneShift = 1.05f;

        // NavMesh areas. Indices 3 and up are free for user-defined areas.
        public const int AreaRoad = 3;
        public const int AreaSidewalk = 4;
        public const int AreaCrosswalk = 5;
        public const int RoadMask = (1 << AreaRoad) | (1 << AreaCrosswalk);
        public const int PedestrianMask = (1 << AreaSidewalk) | (1 << AreaCrosswalk);

        public static Vector3 Dir(Heading heading)
        {
            switch (heading)
            {
                case Heading.East: return new Vector3(1f, 0f, 0f);
                case Heading.North: return new Vector3(0f, 0f, 1f);
                case Heading.West: return new Vector3(-1f, 0f, 0f);
                default: return new Vector3(0f, 0f, -1f);
            }
        }

        /// <summary>Unit vector pointing to the right of someone travelling along the heading.</summary>
        public static Vector3 Right(Heading heading)
        {
            Vector3 d = Dir(heading);
            return new Vector3(d.z, 0f, -d.x);
        }

        public static SignalGroup GroupOf(Heading heading)
        {
            return (heading == Heading.North || heading == Heading.South)
                ? SignalGroup.NorthSouth
                : SignalGroup.EastWest;
        }

        public static Route VehicleStraight(Heading heading, float lateralShift)
        {
            Vector3 d = Dir(heading);
            Vector3 r = Right(heading);
            float lateral = VehicleLaneCenter + lateralShift;

            Route route = new Route();
            route.name = "Vehicle " + heading + " straight";
            route.heading = heading;
            route.signalGroup = GroupOf(heading);
            route.maneuver = Maneuver.Straight;
            route.areaMask = RoadMask;
            route.waypoints.Add(d * -RouteReach + r * lateral);
            route.waypoints.Add(d * RouteReach + r * lateral);
            route.stopPoint = d * -StopLineFromCenter + r * lateral;
            return route;
        }

        /// <summary>
        /// A vehicle that turns right at the intersection. The turn is a quarter circle
        /// that crosses the bike lane, which is where the right-hook conflict happens.
        /// </summary>
        public static Route VehicleRightTurn(Heading heading)
        {
            Vector3 d = Dir(heading);
            Vector3 r = Right(heading);
            float lane = VehicleLaneCenter;
            float radius = TurnRadius;
            Vector3 center = d * (-lane - radius) + r * (lane + radius);

            Route route = new Route();
            route.name = "Vehicle " + heading + " right turn";
            route.heading = heading;
            route.signalGroup = GroupOf(heading);
            route.maneuver = Maneuver.RightTurn;
            route.areaMask = RoadMask;

            route.waypoints.Add(d * -RouteReach + r * lane);
            int steps = 9;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.PI * 0.5f * i / steps;
                route.waypoints.Add(center - r * (radius * Mathf.Cos(angle)) + d * (radius * Mathf.Sin(angle)));
            }
            route.waypoints.Add(d * -lane + r * RouteReach);

            route.stopPoint = d * -StopLineFromCenter + r * lane;
            route.turnStartPoint = center - r * radius;
            route.turnEndPoint = center + d * radius;

            float conflictAngle = Mathf.Acos((radius - (BikeLaneCenter - lane)) / radius);
            route.conflictPoint = center
                - r * (radius * Mathf.Cos(conflictAngle))
                + d * (radius * Mathf.Sin(conflictAngle));
            return route;
        }

        public static Route ScooterStraight(Heading heading)
        {
            Vector3 d = Dir(heading);
            Vector3 r = Right(heading);

            Route route = new Route();
            route.name = "Scooter " + heading;
            route.heading = heading;
            route.signalGroup = GroupOf(heading);
            route.maneuver = Maneuver.Straight;
            route.areaMask = RoadMask;
            route.waypoints.Add(d * -RouteReach + r * BikeLaneCenter);
            route.waypoints.Add(d * RouteReach + r * BikeLaneCenter);
            route.stopPoint = d * -StopLineFromCenter + r * BikeLaneCenter;
            return route;
        }

        /// <summary>
        /// A pedestrian who walks along a sidewalk and crosses one arm of the intersection.
        /// </summary>
        /// <param name="arm">The arm whose road is crossed, named by the direction it points away from the centre.</param>
        /// <param name="side">+1 or -1: which side of that road the pedestrian starts on.</param>
        public static Route PedestrianCrossing(Heading arm, int side, float startDistance, float endDistance)
        {
            Vector3 u = Dir(arm);
            Vector3 w = Right(arm) * side;
            Vector3 line = u * CrosswalkCenter;

            Route route = new Route();
            route.name = "Pedestrian across " + arm + " arm";
            route.heading = arm;
            route.signalGroup = GroupOf(arm);
            route.maneuver = Maneuver.Crossing;
            route.areaMask = PedestrianMask;
            route.waypoints.Add(line + w * startDistance);
            route.waypoints.Add(line + w * KerbWaitFromCenter);
            route.waypoints.Add(line - w * KerbWaitFromCenter);
            route.waypoints.Add(line - w * endDistance);
            route.stopPoint = line + w * KerbWaitFromCenter;
            route.crossingEndPoint = line - w * KerbWaitFromCenter;
            route.crossingCenter = line;
            return route;
        }
    }
}
