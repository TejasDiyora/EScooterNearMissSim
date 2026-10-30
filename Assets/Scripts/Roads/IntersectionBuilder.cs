using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Builds the visible intersection from the numbers in <see cref="RoadLayout"/>:
    /// roads, bike lanes, sidewalks, markings, signals and a few buildings.
    /// Because the scene is generated from the same numbers the agents use, the picture
    /// and the simulation cannot drift apart.
    /// </summary>
    public static class IntersectionBuilder
    {
        private static readonly Color Grass = new Color(0.33f, 0.47f, 0.3f);
        private static readonly Color Asphalt = new Color(0.2f, 0.2f, 0.22f);
        private static readonly Color BikeLane = new Color(0.16f, 0.4f, 0.3f);
        private static readonly Color Sidewalk = new Color(0.68f, 0.68f, 0.66f);
        private static readonly Color PaintWhite = new Color(0.92f, 0.92f, 0.9f);
        private static readonly Color PaintYellow = new Color(0.95f, 0.78f, 0.15f);
        private static readonly Color Metal = new Color(0.28f, 0.29f, 0.31f);
        private static readonly Color SignalHousing = new Color(0.08f, 0.08f, 0.08f);

        private static readonly Color[] BuildingColors =
        {
            new Color(0.7f, 0.62f, 0.52f),
            new Color(0.56f, 0.6f, 0.66f),
            new Color(0.74f, 0.7f, 0.64f),
            new Color(0.6f, 0.5f, 0.45f)
        };

        public static Transform Build(Transform parent, IntersectionManager signals)
        {
            Transform root = new GameObject("Intersection").transform;
            root.SetParent(parent, false);

            float reach = RoadLayout.ArmLength;
            float half = RoadLayout.RoadHalfWidth;

            Shapes.Box(root, "Ground", new Vector3(0f, -0.15f, 0f), new Vector3(2f * reach + 120f, 0.2f, 2f * reach + 120f), Grass);
            Shapes.Box(root, "Road east-west", new Vector3(0f, -0.05f, 0f), new Vector3(2f * reach, 0.1f, 2f * half), Asphalt);
            Shapes.Box(root, "Road north-south", new Vector3(0f, -0.05f, 0f), new Vector3(2f * half, 0.1f, 2f * reach), Asphalt);

            BuildSidewalks(root);
            for (int i = 0; i < 4; i++)
            {
                Heading heading = (Heading)i;
                BuildLaneMarkings(root, heading);
                BuildCrosswalk(root, heading);
                BuildSignal(root, heading, signals);
            }
            BuildBuildings(root);
            return root;
        }

        private static void BuildSidewalks(Transform root)
        {
            Transform group = new GameObject("Sidewalks").transform;
            group.SetParent(root, false);

            float reach = RoadLayout.ArmLength;
            float half = RoadLayout.RoadHalfWidth;
            float walk = RoadLayout.SidewalkWidth;
            float armLength = reach - half;
            float armMiddle = half + armLength * 0.5f;
            float sideMiddle = half + walk * 0.5f;

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Shapes.Box(group, "Sidewalk", new Vector3(sx * armMiddle, 0.005f, sz * sideMiddle),
                        new Vector3(armLength, 0.11f, walk), Sidewalk);
                    Shapes.Box(group, "Sidewalk", new Vector3(sx * sideMiddle, 0.005f, sz * armMiddle),
                        new Vector3(walk, 0.11f, armLength), Sidewalk);
                }
            }
        }

        /// <summary>Paint for the lanes used by traffic travelling along <paramref name="heading"/>.</summary>
        private static void BuildLaneMarkings(Transform root, Heading heading)
        {
            Transform group = new GameObject("Markings " + heading).transform;
            group.SetParent(root, false);

            Vector3 d = RoadLayout.Dir(heading);
            Vector3 r = RoadLayout.Right(heading);
            float reach = RoadLayout.ArmLength;
            float half = RoadLayout.RoadHalfWidth;
            float stop = RoadLayout.StopLineFromCenter;
            float armLength = reach - half;
            float armMiddle = (reach + half) * 0.5f;

            // Bike lane surface and its separating line, before and after the intersection.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 along = d * (sign * armMiddle);
                Shapes.Strip(group, "Bike lane", along + r * RoadLayout.BikeLaneCenter, heading,
                    armLength, RoadLayout.BikeLaneWidth, 0.02f, BikeLane);
                Shapes.Strip(group, "Bike lane line", along + r * RoadLayout.LaneWidth, heading,
                    armLength, 0.12f, 0.04f, PaintWhite);
            }

            // Centre line of the approach.
            float centreLength = reach - stop;
            Shapes.Strip(group, "Centre line", d * -((reach + stop) * 0.5f), heading,
                centreLength, 0.2f, 0.04f, PaintYellow);

            // Stop line across this direction's half of the road.
            Shapes.Strip(group, "Stop line", d * -(stop + 0.2f) + r * (half * 0.5f), heading,
                0.4f, half, 0.04f, PaintWhite);
        }

        /// <summary>Zebra stripes across the arm that points away from the centre along <paramref name="arm"/>.</summary>
        private static void BuildCrosswalk(Transform root, Heading arm)
        {
            Transform group = new GameObject("Crosswalk " + arm).transform;
            group.SetParent(root, false);

            Vector3 u = RoadLayout.Dir(arm);
            Vector3 w = RoadLayout.Right(arm);
            for (int i = 0; i < 10; i++)
            {
                float across = -4.5f + i;
                Shapes.Strip(group, "Stripe", u * RoadLayout.CrosswalkCenter + w * across, arm,
                    RoadLayout.CrosswalkWidth, 0.5f, 0.04f, PaintWhite);
            }
        }

        /// <summary>
        /// A signal for traffic travelling along <paramref name="heading"/>: a mast at the kerb
        /// with the lamps hung over the lane, plus a bar on the road that shows the same
        /// colour so the state is easy to read from the overview camera.
        /// </summary>
        private static void BuildSignal(Transform root, Heading heading, IntersectionManager signals)
        {
            Transform group = new GameObject("Signal " + heading).transform;
            group.SetParent(root, false);

            Vector3 d = RoadLayout.Dir(heading);
            Vector3 r = RoadLayout.Right(heading);
            float half = RoadLayout.RoadHalfWidth;
            float stop = RoadLayout.StopLineFromCenter;

            Vector3 poleBase = d * -stop + r * (half + 0.9f);
            Vector3 overLane = d * -stop + r * RoadLayout.VehicleLaneCenter;
            float armLength = (half + 0.9f) - RoadLayout.VehicleLaneCenter + 1f;
            Vector3 armCentre = (poleBase + overLane) * 0.5f - r * 0.5f;

            // Unity's cylinder is 2 units tall, so a Y scale of 3 gives a 6 m pole.
            Shapes.Create(group, PrimitiveType.Cylinder, "Pole", poleBase + Vector3.up * 3f, new Vector3(0.25f, 3f, 0.25f), Metal);
            Shapes.Box(group, "Mast arm", armCentre + Vector3.up * 5.9f, AcrossSize(heading, armLength, 0.15f, 0.15f), Metal);
            Shapes.Box(group, "Housing", overLane + Vector3.up * 5.4f, AcrossSize(heading, 2f, 0.6f, 0.5f), SignalHousing);

            // Lamps face oncoming traffic. Red is on the driver's left.
            Vector3 lampCentre = overLane + Vector3.up * 5.5f - d * 0.2f;
            Renderer red = Lamp(group, "Red lamp", lampCentre - r * 0.62f);
            Renderer yellow = Lamp(group, "Yellow lamp", lampCentre);
            Renderer green = Lamp(group, "Green lamp", lampCentre + r * 0.62f);

            GameObject bar = Shapes.Strip(group, "Signal bar", d * -(stop + 0.85f) + r * (half * 0.5f), heading,
                0.5f, half, 0.05f, Color.red);

            TrafficLightController head = group.gameObject.AddComponent<TrafficLightController>();
            head.Setup(RoadLayout.GroupOf(heading), red, yellow, green, bar.GetComponent<Renderer>());
            signals.RegisterHead(head);
        }

        private static Renderer Lamp(Transform parent, string name, Vector3 position)
        {
            GameObject lamp = Shapes.Create(parent, PrimitiveType.Sphere, name, position, new Vector3(0.55f, 0.55f, 0.55f), Color.black);
            return lamp.GetComponent<Renderer>();
        }

        /// <summary>Size of a box whose long side runs across the road for the given heading.</summary>
        private static Vector3 AcrossSize(Heading heading, float across, float height, float along)
        {
            bool alongX = heading == Heading.East || heading == Heading.West;
            return alongX ? new Vector3(along, height, across) : new Vector3(across, height, along);
        }

        private static void BuildBuildings(Transform root)
        {
            Transform group = new GameObject("Buildings").transform;
            group.SetParent(root, false);

            float inner = RoadLayout.RoadHalfWidth + RoadLayout.SidewalkWidth + 2f;
            int index = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Low buildings nearest the camera so they do not hide the road.
                    float tall = sz < 0 ? 5f : 9f;
                    AddBuilding(group, sx * (inner + 8f), sz * (inner + 8f), 16f, 16f, tall + 2f, index++);
                    AddBuilding(group, sx * (inner + 30f), sz * (inner + 6f), 20f, 12f, tall, index++);
                    AddBuilding(group, sx * (inner + 6f), sz * (inner + 30f), 12f, 20f, tall + 1f, index++);
                }
            }
        }

        private static void AddBuilding(Transform group, float x, float z, float sizeX, float sizeZ, float height, int index)
        {
            Color color = BuildingColors[index % BuildingColors.Length];
            Shapes.Box(group, "Building", new Vector3(x, height * 0.5f - 0.05f, z), new Vector3(sizeX, height, sizeZ), color);
        }
    }
}
