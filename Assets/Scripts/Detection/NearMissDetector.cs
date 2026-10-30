using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Watches every vehicle and pedestrian that comes near a scooter and decides, once
    /// per encounter, whether it was a collision, a near-miss, or a normal pass.
    ///
    ///   Collision  the two bodies overlapped while closing at 1 m/s or more.
    ///   Near-miss  no contact, but either
    ///              - a moving road user passed closer than the distance threshold, or
    ///              - the pair stayed on a collision course with a time-to-collision
    ///                below the TTC threshold for at least <see cref="MinTtcDuration"/> seconds.
    ///
    /// Measurements only count while the two are moving relative to each other. A car
    /// queued next to a scooter at a red light is close, but it is not a near-miss.
    /// </summary>
    public class NearMissDetector : MonoBehaviour
    {
        /// <summary>Relative speed, in m/s, below which a pair is treated as stationary.</summary>
        public const float MinConflictSpeed = 1.0f;
        /// <summary>Speed, in m/s, above which the other road user counts as moving for the distance rule.</summary>
        public const float MinOtherSpeed = 0.5f;
        /// <summary>The distance rule still applies for this many seconds after the other road user stops.</summary>
        public const float RecentlyMovingWindow = 1.0f;
        /// <summary>
        /// How long a low time-to-collision has to last before it counts. A car swinging
        /// through a turn can point at a scooter for an instant without being a threat.
        /// </summary>
        public const float MinTtcDuration = 0.3f;
        private const float ExitHysteresis = 2f;
        private const long KeyStride = 1000000L;

        private SimulationManager sim;
        private readonly Dictionary<long, Encounter> open = new Dictionary<long, Encounter>();
        private readonly List<long> scratchKeys = new List<long>();

        /// <summary>Conflicts in encounters that are still open and not yet written to the log.</summary>
        public int PendingNearMisses { get; private set; }
        public int PendingCollisions { get; private set; }

        public void Initialize(SimulationManager manager)
        {
            sim = manager;
        }

        public void ResetRun()
        {
            open.Clear();
            PendingNearMisses = 0;
            PendingCollisions = 0;
        }

        public void Tick(float dt)
        {
            PendingNearMisses = 0;
            PendingCollisions = 0;

            ScenarioConfig config = sim.Config;
            List<ScooterController> scooters = sim.Scooters;
            List<AgentBase> agents = sim.Agents;
            float radius = config.detectionRadius;

            for (int s = 0; s < scooters.Count; s++)
            {
                ScooterController scooter = scooters[s];

                for (int i = 0; i < agents.Count; i++)
                {
                    AgentBase other = agents[i];
                    if (other.Kind == AgentType.Scooter) continue;

                    float distance = Vector3.Distance(other.Position, scooter.Position);
                    long key = KeyOf(scooter.AgentId, other.AgentId);

                    Encounter encounter;
                    if (!open.TryGetValue(key, out encounter))
                    {
                        if (distance > radius) continue;
                        encounter = new Encounter(scooter, other, sim.SimTime);
                        open.Add(key, encounter);
                    }
                    else if (distance > radius + ExitHysteresis)
                    {
                        open.Remove(key);
                        Close(encounter);
                        continue;
                    }

                    Measure(encounter, scooter, other, dt);

                    if (encounter.Collided) PendingCollisions++;
                    else if (encounter.IsConflict(config)) PendingNearMisses++;
                }
            }
        }

        /// <summary>Ends every encounter with an agent that is about to leave the simulation.</summary>
        public void OnAgentRemoved(AgentBase agent)
        {
            scratchKeys.Clear();
            foreach (KeyValuePair<long, Encounter> pair in open)
            {
                if (pair.Value.OtherId == agent.AgentId) scratchKeys.Add(pair.Key);
            }
            CloseScratchKeys();
        }

        /// <summary>Ends every encounter of one scooter. Used when it jumps to the start of its next trip.</summary>
        public void CloseAllFor(ScooterController scooter)
        {
            scratchKeys.Clear();
            foreach (KeyValuePair<long, Encounter> pair in open)
            {
                if (pair.Value.ScooterId == scooter.AgentId) scratchKeys.Add(pair.Key);
            }
            CloseScratchKeys();
        }

        /// <summary>Ends every open encounter. Used when the run ends.</summary>
        public void CloseAll()
        {
            scratchKeys.Clear();
            foreach (KeyValuePair<long, Encounter> pair in open)
            {
                scratchKeys.Add(pair.Key);
            }
            CloseScratchKeys();
            PendingNearMisses = 0;
            PendingCollisions = 0;
        }

        private static long KeyOf(int scooterId, int otherId)
        {
            return scooterId * KeyStride + otherId;
        }

        private void CloseScratchKeys()
        {
            // Close in a fixed order so the event log is the same on every run.
            scratchKeys.Sort();
            for (int i = 0; i < scratchKeys.Count; i++)
            {
                Encounter encounter = open[scratchKeys[i]];
                open.Remove(scratchKeys[i]);
                Close(encounter);
            }
            scratchKeys.Clear();
        }

        private void Measure(Encounter encounter, ScooterController scooter, AgentBase other, float dt)
        {
            float gap = ConflictMath.Gap(scooter, other);
            if (gap < encounter.ClosestGap) encounter.ClosestGap = Mathf.Max(0f, gap);
            if (other.Speed >= MinOtherSpeed) encounter.OtherLastMoving = sim.SimTime;

            float relativeSpeed = (other.Velocity - scooter.Velocity).magnitude;

            // Contact only counts as a collision at a real closing speed. Two road users
            // edging past each other at walking pace after a near-miss are not crashing.
            if (gap <= 0f && relativeSpeed >= MinConflictSpeed)
            {
                if (!encounter.Collided)
                {
                    encounter.Collided = true;
                    encounter.MinGap = 0f;
                    encounter.MinTtc = 0f;
                    encounter.AtMinGap = Capture(scooter, other);
                }
                return;
            }
            if (encounter.Collided) return;

            if (relativeSpeed < MinConflictSpeed)
            {
                encounter.LowTtcDuration = 0f;
                encounter.LowTtcMin = float.PositiveInfinity;
                return;
            }

            // Distance rule: a moving road user passing close to the scooter. The scooter
            // riding past a stopped queue of cars is not a near-miss, but a car that has
            // just braked to a stop to avoid the scooter still counts.
            bool otherActive = sim.SimTime - encounter.OtherLastMoving <= RecentlyMovingWindow;
            if (otherActive && gap < encounter.MinGap)
            {
                encounter.MinGap = Mathf.Max(0f, gap);
                encounter.AtMinGap = Capture(scooter, other);
            }

            // TTC rule: the low value has to persist before it is accepted.
            float ttc = ConflictMath.TimeToCollision(scooter, other, 0f);
            if (ttc < sim.Config.ttcThreshold)
            {
                encounter.LowTtcDuration += dt;
                if (ttc < encounter.LowTtcMin)
                {
                    encounter.LowTtcMin = ttc;
                    encounter.AtLowTtcMin = Capture(scooter, other);
                }
                if (encounter.LowTtcDuration >= MinTtcDuration && encounter.LowTtcMin < encounter.MinTtc)
                {
                    encounter.MinTtc = encounter.LowTtcMin;
                    encounter.AtMinTtc = encounter.AtLowTtcMin;
                }
            }
            else
            {
                encounter.LowTtcDuration = 0f;
                encounter.LowTtcMin = float.PositiveInfinity;
            }
        }

        private EncounterSnapshot Capture(ScooterController scooter, AgentBase other)
        {
            EncounterSnapshot snapshot = new EncounterSnapshot();
            snapshot.valid = true;
            snapshot.time = sim.SimTime;
            snapshot.scooterSpeed = scooter.Speed;
            snapshot.otherSpeed = other.Speed;
            snapshot.position = scooter.Position;
            snapshot.signal = sim.Intersection.GetState(scooter.Route.signalGroup);
            snapshot.scooterHeading = scooter.Route.heading;
            VehicleController vehicle = other as VehicleController;
            bool sameDirection = Vector3.Dot(scooter.Forward, other.Forward) > 0.9f;
            snapshot.otherTurning = !sameDirection || (vehicle != null && vehicle.IsInTurn);
            return snapshot;
        }

        private void Close(Encounter encounter)
        {
            ScenarioConfig config = sim.Config;
            sim.Recorder.CountEncounter();

            bool byDistance = encounter.MinGap < config.nearMissDistance;
            bool byTtc = encounter.MinTtc < config.ttcThreshold;
            if (!encounter.Collided && !byDistance && !byTtc) return;

            EncounterSnapshot at = (encounter.Collided || byDistance) ? encounter.AtMinGap : encounter.AtMinTtc;
            if (!at.valid) return;

            NearMissEvent e = new NearMissEvent();
            e.runId = sim.RunId;
            e.scenarioName = config.scenarioName;
            e.time = at.time;
            e.eventType = encounter.Collided ? SafetyEventType.Collision : SafetyEventType.NearMiss;
            if (encounter.OtherKind == AgentType.Pedestrian) e.conflictType = ConflictType.PedestrianCrossing;
            else e.conflictType = at.otherTurning ? ConflictType.TurnConflict : ConflictType.ClosePass;

            if (encounter.Collided) e.trigger = "Contact";
            else if (byDistance && byTtc) e.trigger = "Distance+TTC";
            else if (byDistance) e.trigger = "Distance";
            else e.trigger = "TTC";

            e.scooterId = encounter.ScooterId;
            e.otherType = encounter.OtherKind;
            e.otherId = encounter.OtherId;
            e.scooterSpeed = at.scooterSpeed;
            e.otherSpeed = at.otherSpeed;
            e.minGap = (encounter.Collided || byDistance) ? encounter.MinGap : encounter.ClosestGap;
            e.minTtc = encounter.MinTtc;
            e.signalState = at.signal;
            e.scooterHeading = at.scooterHeading;
            e.reactionTime = config.reactionTime;
            e.posX = at.position.x;
            e.posZ = at.position.z;

            sim.Recorder.Record(e);
            sim.RaiseEventRecorded(e);
        }
    }
}
