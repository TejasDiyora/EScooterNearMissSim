using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Creates the GameObjects for road users. By default each one is assembled from
    /// primitive shapes so the project needs no art assets. Assign a prefab on the
    /// SimulationBootstrap component to use your own model instead: it should face +Z and
    /// have its origin on the ground at the centre of the body.
    /// </summary>
    public static class AgentFactory
    {
        public static GameObject ScooterPrefab;
        public static GameObject VehiclePrefab;
        public static GameObject PedestrianPrefab;

        public static readonly Color PrimaryScooterColor = new Color(1f, 0.45f, 0.05f);
        public static readonly Color ScooterColor = new Color(0.98f, 0.82f, 0.12f);
        public static readonly Color CompliantPedestrianColor = new Color(0.2f, 0.55f, 0.9f);
        public static readonly Color RiskyPedestrianColor = new Color(0.92f, 0.3f, 0.6f);

        private static readonly Color DarkMetal = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Glass = new Color(0.16f, 0.2f, 0.26f);
        private static readonly Color Skin = new Color(0.93f, 0.8f, 0.68f);
        private static readonly Color IndicatorAmber = new Color(1f, 0.6f, 0f);

        private static readonly Color[] VehiclePalette =
        {
            new Color(0.75f, 0.76f, 0.78f),
            new Color(0.2f, 0.32f, 0.55f),
            new Color(0.62f, 0.14f, 0.14f),
            new Color(0.9f, 0.9f, 0.88f),
            new Color(0.25f, 0.27f, 0.3f),
            new Color(0.2f, 0.45f, 0.35f)
        };

        /// <param name="primary">The first scooter gets its own colour because the camera follows it.</param>
        public static ScooterController CreateScooter(Transform parent, bool primary)
        {
            GameObject root = NewRoot("Scooter", parent, ScooterPrefab);
            if (ScooterPrefab == null)
            {
                Transform t = root.transform;
                Color riderColor = primary ? PrimaryScooterColor : ScooterColor;
                Shapes.Create(t, PrimitiveType.Cube, "Deck", new Vector3(0f, 0.12f, 0f), new Vector3(0.28f, 0.06f, 1.15f), DarkMetal);
                Shapes.Create(t, PrimitiveType.Cube, "Stem", new Vector3(0f, 0.62f, 0.48f), new Vector3(0.06f, 1f, 0.06f), DarkMetal);
                Shapes.Create(t, PrimitiveType.Cube, "Handlebar", new Vector3(0f, 1.12f, 0.48f), new Vector3(0.5f, 0.05f, 0.05f), DarkMetal);
                Shapes.Create(t, PrimitiveType.Capsule, "Rider", new Vector3(0f, 1.02f, -0.05f), new Vector3(0.42f, 0.85f, 0.42f), riderColor);
                Shapes.Create(t, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 1.98f, -0.05f), new Vector3(0.32f, 0.32f, 0.32f), Color.white);
                // A coloured disc on the ground so the scooter is easy to find from the overview camera.
                Shapes.Create(t, PrimitiveType.Cylinder, "Ground marker", new Vector3(0f, 0.06f, 0f), new Vector3(1.6f, 0.01f, 1.6f), riderColor);
            }
            return GetOrAdd<ScooterController>(root);
        }

        /// <param name="colorSeed">Any number. Picks the body colour so cars differ without using the run's random generator.</param>
        public static VehicleController CreateVehicle(Transform parent, int colorSeed)
        {
            GameObject root = NewRoot("Vehicle", parent, VehiclePrefab);
            VehicleController controller = GetOrAdd<VehicleController>(root);
            if (VehiclePrefab == null)
            {
                Transform t = root.transform;
                Color body = VehiclePalette[Mathf.Abs(colorSeed) % VehiclePalette.Length];
                Shapes.Create(t, PrimitiveType.Cube, "Body", new Vector3(0f, 0.55f, 0f),
                    new Vector3(RoadLayout.VehicleWidth, 0.7f, RoadLayout.VehicleLength), body);
                Shapes.Create(t, PrimitiveType.Cube, "Cabin", new Vector3(0f, 1.15f, -0.25f),
                    new Vector3(RoadLayout.VehicleWidth - 0.2f, 0.55f, 2.2f), Glass);

                // Right-hand indicator lamps, front and rear. Shown only while indicating.
                GameObject lamps = new GameObject("Right indicator");
                lamps.transform.SetParent(t, false);
                float side = RoadLayout.VehicleWidth * 0.5f - 0.15f;
                float end = RoadLayout.VehicleLength * 0.5f - 0.2f;
                Shapes.Create(lamps.transform, PrimitiveType.Cube, "Front", new Vector3(side, 0.95f, end), new Vector3(0.4f, 0.12f, 0.4f), IndicatorAmber);
                Shapes.Create(lamps.transform, PrimitiveType.Cube, "Rear", new Vector3(side, 0.95f, -end), new Vector3(0.4f, 0.12f, 0.4f), IndicatorAmber);
                controller.SetIndicatorLamp(lamps);
            }
            return controller;
        }

        /// <param name="compliant">Pedestrians who wait for the walk signal are blue, the others pink.</param>
        public static PedestrianController CreatePedestrian(Transform parent, bool compliant)
        {
            GameObject root = NewRoot("Pedestrian", parent, PedestrianPrefab);
            if (PedestrianPrefab == null)
            {
                Transform t = root.transform;
                Color clothes = compliant ? CompliantPedestrianColor : RiskyPedestrianColor;
                Shapes.Create(t, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f), clothes);
                Shapes.Create(t, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Skin);
                Shapes.Create(t, PrimitiveType.Cylinder, "Ground marker", new Vector3(0f, 0.07f, 0f), new Vector3(0.9f, 0.01f, 0.9f), clothes);
            }
            return GetOrAdd<PedestrianController>(root);
        }

        private static GameObject NewRoot(string name, Transform parent, GameObject prefab)
        {
            GameObject root = prefab != null ? Object.Instantiate(prefab) : new GameObject(name);
            root.name = name;
            root.transform.SetParent(parent, false);
            return root;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null) component = root.AddComponent<T>();
            return component;
        }
    }
}
