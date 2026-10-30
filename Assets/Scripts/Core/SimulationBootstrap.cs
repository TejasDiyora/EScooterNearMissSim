using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// The one component you add to a scene. On Play it builds the intersection, bakes the
    /// NavMesh, and creates the simulation, the camera and the user interface.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SimulationBootstrap : MonoBehaviour
    {
        [Header("Optional: your own models instead of the built-in shapes")]
        [Tooltip("Should face +Z with its origin on the ground at the centre of the body.")]
        public GameObject scooterPrefab;
        public GameObject vehiclePrefab;
        public GameObject pedestrianPrefab;

        [Header("Optional: skip the setup screen")]
        [Tooltip("Start a run immediately with the preset below, or with the Baseline settings if none is assigned.")]
        public bool startImmediately;
        public ScenarioPreset startPreset;

        public SimulationManager Simulation { get; private set; }

        private void Awake()
        {
            // The simulation works in world coordinates with the intersection at the origin.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            Application.runInBackground = true;
            Time.fixedDeltaTime = SimulationManager.StepSeconds;
            Time.timeScale = 1f;

            AgentFactory.ScooterPrefab = scooterPrefab;
            AgentFactory.VehiclePrefab = vehiclePrefab;
            AgentFactory.PedestrianPrefab = pedestrianPrefab;

            NavMeshRoutePlanner.ResetCounters();
            if (!NavMeshBaker.Build())
            {
                Debug.LogWarning("E-Scooter Sim: the NavMesh could not be built. Agents will follow straight lines between waypoints instead.");
            }

            GameObject simObject = new GameObject("Simulation");
            Simulation = simObject.AddComponent<SimulationManager>();
            Simulation.RunFinished += ReportPathFallbacks;

            IntersectionBuilder.Build(transform, Simulation.Intersection);
            EnsureSun();
            CameraRig rig = CameraRig.Create(Simulation);

            GameObject markerObject = new GameObject("Event markers");
            markerObject.AddComponent<EventMarkers>().Initialize(Simulation);

            Canvas canvas = UIFactory.CreateCanvas("Simulation UI");
            canvas.gameObject.AddComponent<HudUI>().Initialize(Simulation, rig, canvas.transform);
            canvas.gameObject.AddComponent<ResultsDashboardUI>().Initialize(Simulation, canvas.transform);
            canvas.gameObject.AddComponent<ScenarioSetupUI>().Initialize(Simulation, canvas.transform);
        }

        private void Start()
        {
            if (!startImmediately) return;
            ScenarioConfig config = startPreset != null ? startPreset.config : new ScenarioConfig();
            Simulation.BeginRun(config);
        }

        private void OnDestroy()
        {
            if (Simulation != null) Simulation.RunFinished -= ReportPathFallbacks;
            NavMeshBaker.Clear();
            Time.timeScale = 1f;
        }

        /// <summary>Tells you in the Console if any path had to fall back to a straight line.</summary>
        private void ReportPathFallbacks()
        {
            if (NavMeshRoutePlanner.Fallbacks > 0)
            {
                Debug.LogWarning("E-Scooter Sim: " + NavMeshRoutePlanner.Fallbacks + " of "
                    + NavMeshRoutePlanner.LegsPlanned
                    + " path legs could not be planned on the NavMesh and used a straight line instead.");
            }
        }

        private static void EnsureSun()
        {
            if (RenderSettings.sun != null) return;
            if (GameObject.Find("Directional Light") != null) return;

            GameObject sun = new GameObject("Sun");
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.1f;
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }
    }
}
