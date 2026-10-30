using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace EScooterSim
{
    public enum SimState { Setup = 0, Running = 1, Finished = 2 }

    /// <summary>
    /// Owns the run: the clock, the list of agents, the random generator and the order in
    /// which everything is updated.
    ///
    /// The simulation advances in fixed steps from FixedUpdate, and all random choices
    /// come from one seeded generator. The same settings and seed therefore give the same
    /// events regardless of frame rate, which matters when the output is used as data.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        /// <summary>Step length used when a run is fast-forwarded to its end.</summary>
        public const float StepSeconds = 0.02f;

        public ScenarioConfig Config { get; private set; }
        public SimState State { get; private set; }
        public float SimTime { get; private set; }
        public string RunId { get; private set; }

        public IntersectionManager Intersection { get; private set; }
        public Spawner Spawner { get; private set; }
        public NearMissDetector Detector { get; private set; }
        public DataRecorder Recorder { get; private set; }
        public Transform AgentRoot { get; private set; }
        public List<AgentBase> Agents { get { return agents; } }
        public List<ScooterController> Scooters { get { return scooters; } }

        /// <summary>The first scooter. The camera follows it and the HUD shows its speed.</summary>
        public ScooterController PrimaryScooter
        {
            get { return scooters.Count > 0 ? scooters[0] : null; }
        }

        public event System.Action RunStarted;
        public event System.Action RunFinished;
        public event System.Action SetupEntered;
        public event System.Action<NearMissEvent> EventRecorded;

        private readonly List<AgentBase> agents = new List<AgentBase>();
        private readonly List<ScooterController> scooters = new List<ScooterController>();
        private readonly List<AgentBase> finished = new List<AgentBase>();
        private System.Random rng = new System.Random(1);
        private int nextAgentId = 1;
        private int runCounter;

        private void Awake()
        {
            Config = new ScenarioConfig();
            Recorder = new DataRecorder();
            State = SimState.Setup;
            RunId = "";

            AgentRoot = new GameObject("Agents").transform;
            AgentRoot.SetParent(transform, false);

            Intersection = gameObject.AddComponent<IntersectionManager>();
            Spawner = gameObject.AddComponent<Spawner>();
            Detector = gameObject.AddComponent<NearMissDetector>();
            Intersection.Configure(Config);
            Spawner.Initialize(this);
            Detector.Initialize(this);
        }

        private void FixedUpdate()
        {
            if (State == SimState.Running) Step(Time.fixedDeltaTime);
        }

        /// <summary>Starts a new run with a copy of the given settings.</summary>
        public void BeginRun(ScenarioConfig config)
        {
            ClearAgents();

            Config = config.Clone();
            rng = new System.Random(Config.randomSeed);
            nextAgentId = 1;
            SimTime = 0f;
            runCounter++;
            RunId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + "_" + runCounter.ToString("00", CultureInfo.InvariantCulture);

            Recorder.Clear();
            Intersection.Configure(Config);
            Intersection.ResetCycle();
            Detector.ResetRun();
            State = SimState.Running;
            Spawner.BeginRun();

            Time.timeScale = 1f;
            if (RunStarted != null) RunStarted();
        }

        /// <summary>
        /// Advances the simulation by one step. The order is fixed:
        /// signals, spawning, decisions, movement, detection, clean-up.
        /// </summary>
        public void Step(float dt)
        {
            if (State != SimState.Running) return;

            SimTime += dt;
            Intersection.Tick(dt);
            Spawner.Tick();

            for (int i = 0; i < agents.Count; i++) agents[i].Decide(dt);
            for (int i = 0; i < agents.Count; i++)
            {
                agents[i].Move(dt);
                agents[i].SyncTransform(dt);
            }

            for (int i = 0; i < scooters.Count; i++)
            {
                if (!scooters[i].ReachedEnd) continue;
                // The scooter jumps to the start of its next trip, so every encounter it
                // was in ends here.
                Detector.CloseAllFor(scooters[i]);
                Spawner.StartNextScooterTrip(scooters[i]);
            }

            Detector.Tick(dt);

            finished.Clear();
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i].ReachedEnd && agents[i].Kind != AgentType.Scooter) finished.Add(agents[i]);
            }
            for (int i = 0; i < finished.Count; i++) RemoveAgent(finished[i]);
            finished.Clear();

            UpdateExposure();
            if (SimTime >= Config.runDuration) EndRun();
        }

        /// <summary>Runs the rest of the current run immediately instead of in real time.</summary>
        public void RunToEnd()
        {
            int guard = 0;
            while (State == SimState.Running && guard < 500000)
            {
                Step(StepSeconds);
                guard++;
            }
        }

        public void EndRun()
        {
            if (State != SimState.Running) return;

            Detector.CloseAll();
            UpdateExposure();
            State = SimState.Finished;
            Time.timeScale = 1f;
            if (RunFinished != null) RunFinished();
        }

        /// <summary>Removes every agent and returns to the setup state.</summary>
        public void ResetToSetup()
        {
            ClearAgents();
            Detector.ResetRun();
            State = SimState.Setup;
            Time.timeScale = 1f;
            if (SetupEntered != null) SetupEntered();
        }

        public void RegisterAgent(AgentBase agent)
        {
            agents.Add(agent);
        }

        public void RegisterScooter(ScooterController scooter)
        {
            scooters.Add(scooter);
        }

        public int NextAgentId()
        {
            int id = nextAgentId;
            nextAgentId++;
            return id;
        }

        public int CountAgents(AgentType kind)
        {
            int count = 0;
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i].Kind == kind) count++;
            }
            return count;
        }

        /// <summary>Plans the path for a route on the NavMesh and fills in the route's key distances.</summary>
        public RoutePath PlanPath(Route route)
        {
            RoutePath path = NavMeshRoutePlanner.BuildPath(route.waypoints, route.areaMask);
            route.ComputeDistances(path);
            return path;
        }

        /// <summary>Random number in [0, 1) from the run's seeded generator.</summary>
        public float RandomValue()
        {
            return (float)rng.NextDouble();
        }

        public float RandomRange(float min, float max)
        {
            return min + (max - min) * (float)rng.NextDouble();
        }

        /// <summary>Random whole number from 0 up to, but not including, <paramref name="count"/>.</summary>
        public int RandomInt(int count)
        {
            return rng.Next(count);
        }

        public void RaiseEventRecorded(NearMissEvent e)
        {
            if (EventRecorded != null) EventRecorded(e);
        }

        private void RemoveAgent(AgentBase agent)
        {
            Detector.OnAgentRemoved(agent);
            agents.Remove(agent);
            Destroy(agent.gameObject);
        }

        private void ClearAgents()
        {
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i] != null) Destroy(agents[i].gameObject);
            }
            agents.Clear();
            scooters.Clear();
        }

        private void UpdateExposure()
        {
            float distance = 0f;
            int trips = 0;
            for (int i = 0; i < scooters.Count; i++)
            {
                distance += scooters[i].DistanceTravelled;
                trips += scooters[i].TripsCompleted;
            }
            Recorder.Duration = SimTime;
            Recorder.ScooterDistance = distance;
            Recorder.ScooterTrips = trips;
        }
    }
}
