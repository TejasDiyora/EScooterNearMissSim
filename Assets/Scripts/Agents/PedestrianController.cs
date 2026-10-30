using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// A pedestrian who walks along the sidewalk and crosses one road.
    /// Compliant pedestrians wait for the walk signal. The others cross whenever there is
    /// a gap in car traffic. They look for cars but not for scooters, which is what
    /// produces pedestrian conflicts in the bike lane.
    /// </summary>
    public class PedestrianController : AgentBase
    {
        /// <summary>True if this pedestrian waits for the walk signal.</summary>
        public bool Compliant { get; private set; }
        public bool CrossingStarted { get; private set; }

        /// <summary>True from stepping off toward the road until reaching the far kerb.</summary>
        public override bool IsOnRoadway
        {
            get { return CrossingStarted && Route != null && DistanceAlong <= Route.crossingEndDistance + 0.5f; }
        }

        private float walkSpeed = 1.4f;

        public void Configure(float speed, bool compliant)
        {
            walkSpeed = Mathf.Max(0.3f, speed);
            Compliant = compliant;
            CrossingStarted = false;
            acceleration = 3f;
            maxBraking = 4f;
            planningDeceleration = 1.5f;
        }

        /// <summary>
        /// Pedestrians watch for cars. Scooters are small and quiet and go unnoticed,
        /// which is the behaviour this simulator studies.
        /// </summary>
        protected override bool IsBlockedBy(AgentBase other, float lookahead, out float gap)
        {
            if (other.Kind != AgentType.Vehicle)
            {
                gap = float.MaxValue;
                return false;
            }
            return CorridorGap(other, lookahead, 0.2f, out gap);
        }

        protected override float ComputeTargetSpeed(float dt)
        {
            if (CrossingStarted)
            {
                // Do not walk into the side of a car that is in the way.
                float gapAhead;
                AgentBase blocker;
                if (IsOnRoadway && FindObstacleAhead(2.5f, out gapAhead, out blocker) && gapAhead < 1f)
                {
                    BlockedBy = blocker;
                    return 0f;
                }
                return walkSpeed;
            }

            float toKerb = Route.stopLineDistance - DistanceAlong;
            bool canCross = CanCross();

            if (toKerb <= 0.3f && canCross)
            {
                CrossingStarted = true;
                return walkSpeed;
            }
            if (canCross) return walkSpeed;
            return Mathf.Min(walkSpeed, StopSpeedForRoom(toKerb));
        }

        private bool CanCross()
        {
            IntersectionManager signals = sim.Intersection;

            // Never step out while a car is sitting on the crossing.
            float gapAhead;
            AgentBase blocker;
            float toFarKerb = Route.crossingEndDistance - DistanceAlong;
            if (FindObstacleAhead(toFarKerb, out gapAhead, out blocker)) return false;

            // Walk signal: traffic on the road being crossed is held on red for long
            // enough to get all the way over.
            float crossingLength = Route.crossingEndDistance - Route.stopLineDistance;
            float timeNeeded = crossingLength / walkSpeed;
            bool walkSignal = signals.GetState(Route.signalGroup) == SignalState.Red
                && signals.TimeUntilGreen(Route.signalGroup) >= timeNeeded * 0.85f;
            if (walkSignal) return true;
            if (Compliant) return false;

            // Crossing against the signal: accept a gap in car traffic.
            List<AgentBase> agents = sim.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentBase other = agents[i];
                if (other.Kind != AgentType.Vehicle) continue;
                if (other.Speed < 0.5f) continue;

                Vector3 toCrossing = Route.crossingCenter - other.Position;
                float distance = toCrossing.magnitude;
                if (distance > 35f) continue;
                if (Vector3.Dot(other.Velocity, toCrossing) <= 0f) continue;   // driving away
                if (distance / other.Speed < 5f) return false;
            }
            return true;
        }
    }
}
