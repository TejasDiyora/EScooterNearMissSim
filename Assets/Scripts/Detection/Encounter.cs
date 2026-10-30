using UnityEngine;

namespace EScooterSim
{
    /// <summary>What the situation looked like at one moment of an encounter.</summary>
    public struct EncounterSnapshot
    {
        public bool valid;
        public float time;
        public float scooterSpeed;
        public float otherSpeed;
        public Vector3 position;
        public SignalState signal;
        public Heading scooterHeading;
        /// <summary>True if the other road user was a vehicle turning, or crossing the scooter's direction of travel.</summary>
        public bool otherTurning;
    }

    /// <summary>
    /// One continuous period during which another road user is inside a scooter's
    /// detection radius. The detector keeps the closest approach here and classifies the
    /// encounter once, when it ends. That is what stops one close call being logged on
    /// every frame it lasts.
    /// </summary>
    public class Encounter
    {
        public readonly int ScooterId;
        public readonly int OtherId;
        public readonly AgentType OtherKind;
        public readonly float StartTime;

        /// <summary>Smallest gap at any moment of the encounter.</summary>
        public float ClosestGap = float.PositiveInfinity;
        /// <summary>Smallest gap seen while the other road user was moving past. This is what the distance rule uses.</summary>
        public float MinGap = float.PositiveInfinity;
        /// <summary>Lowest time-to-collision that lasted long enough to count.</summary>
        public float MinTtc = float.PositiveInfinity;
        public bool Collided;
        public EncounterSnapshot AtMinGap;
        public EncounterSnapshot AtMinTtc;

        /// <summary>Last time the other road user was moving. Starts long ago so "never" reads as stale.</summary>
        public float OtherLastMoving = -1000f;

        // Working values for the current stretch of low time-to-collision.
        public float LowTtcDuration;
        public float LowTtcMin = float.PositiveInfinity;
        public EncounterSnapshot AtLowTtcMin;

        public Encounter(AgentBase scooter, AgentBase other, float startTime)
        {
            ScooterId = scooter.AgentId;
            OtherId = other.AgentId;
            OtherKind = other.Kind;
            StartTime = startTime;
        }

        public bool IsConflict(ScenarioConfig config)
        {
            return Collided || MinGap < config.nearMissDistance || MinTtc < config.ttcThreshold;
        }
    }
}
