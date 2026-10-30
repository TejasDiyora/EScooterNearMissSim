using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// An e-scooter. It rides in the bike lane, obeys the signal, and responds to hazards
    /// only after the rider's reaction time has passed.
    ///
    /// A hazard is another road user that is either in the strip of bike lane ahead or on
    /// a collision course. The first time one qualifies, the time is noted. The scooter
    /// may respond to it once it has been a hazard for <see cref="ScenarioConfig.reactionTime"/>
    /// seconds. Something seen from far away has long been "noticed" by the time braking is
    /// needed, so the delay only bites for hazards that appear suddenly.
    /// </summary>
    public class ScooterController : AgentBase
    {
        /// <summary>How far the rider looks for hazards, in metres.</summary>
        public const float AwarenessRange = 25f;
        /// <summary>Collision courses further off than this many seconds are not treated as hazards.</summary>
        public const float HazardHorizon = 3.5f;
        /// <summary>Below this time-to-collision the rider brakes as hard as possible.</summary>
        public const float EmergencyTtc = 2.0f;
        /// <summary>Distance the rider aims to stop short of something blocking the lane.</summary>
        public const float FollowStandoff = 2.0f;
        /// <summary>Extra clearance the rider wants when judging a collision course.</summary>
        public const float HazardMargin = 0.2f;

        private readonly Dictionary<int, float> firstNoticed = new Dictionary<int, float>();

        /// <summary>True while the rider is emergency braking.</summary>
        public bool IsBraking { get; private set; }
        public int TripsCompleted { get; set; }
        /// <summary>Which of the four approaches the current trip uses.</summary>
        public int RouteIndex { get; set; }

        public void Configure(ScenarioConfig config)
        {
            acceleration = Mathf.Max(0.1f, config.scooterAcceleration);
            maxBraking = Mathf.Max(0.5f, config.scooterBrakeDeceleration);
            planningDeceleration = 1.5f;
        }

        /// <summary>Called when the scooter starts a new trip somewhere else.</summary>
        public void ForgetHazards()
        {
            firstNoticed.Clear();
        }

        protected override float CorridorMarginFor(AgentBase other)
        {
            return other.Kind == AgentType.Pedestrian ? 0.6f : 0.2f;
        }

        protected override float ComputeTargetSpeed(float dt)
        {
            ScenarioConfig config = sim.Config;
            float target = Mathf.Min(config.scooterSpeed, SignalSpeedLimit());
            IsBraking = false;

            List<AgentBase> agents = sim.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentBase other = agents[i];
                if (other == this) continue;

                Vector3 offset = other.Position - Position;
                if (offset.sqrMagnitude > AwarenessRange * AwarenessRange)
                {
                    firstNoticed.Remove(other.AgentId);
                    continue;
                }

                float gap;
                bool inLane = CorridorGap(other, AwarenessRange, CorridorMarginFor(other), out gap);

                // The rider cannot see what is behind, so only things ahead or alongside
                // can be judged for a collision course.
                float ttc = float.PositiveInfinity;
                if (Vector3.Dot(offset, Forward) > -1f)
                {
                    // Extra clearance is only wanted from things that are moving. Riding
                    // past something standing still is fine as long as it is not touched.
                    bool otherMoving = other.Speed > 0.2f;
                    ttc = ConflictMath.TimeToCollision(this, other, otherMoving ? HazardMargin : 0.05f);

                    // A car that is indicating shows where it is going, so the rider can
                    // judge the turn before the car starts it.
                    VehicleController vehicle = other as VehicleController;
                    if (otherMoving && vehicle != null && vehicle.IndicatorOn)
                    {
                        float predicted = ConflictMath.PredictedTimeToCollision(this, other, HazardHorizon, HazardMargin);
                        ttc = Mathf.Min(ttc, predicted);
                    }
                }

                if (!inLane && ttc >= HazardHorizon) continue;

                float noticedAt;
                if (!firstNoticed.TryGetValue(other.AgentId, out noticedAt))
                {
                    noticedAt = sim.SimTime;
                    firstNoticed[other.AgentId] = noticedAt;
                }
                if (sim.SimTime - noticedAt < config.reactionTime) continue;

                if (inLane)
                {
                    float leadSpeed = Mathf.Max(0f, Vector3.Dot(other.Velocity, Forward));
                    float followSpeed = SafeFollowSpeed(gap, FollowStandoff, leadSpeed);
                    if (followSpeed < target)
                    {
                        target = followSpeed;
                        BlockedBy = other;
                    }
                }

                if (ttc < EmergencyTtc)
                {
                    target = 0f;
                    IsBraking = true;
                    BlockedBy = other;
                }
                else if (ttc < HazardHorizon)
                {
                    // A conflict is developing but is not urgent yet: ease off.
                    target = Mathf.Min(target, Mathf.Max(0f, Speed - planningDeceleration * dt));
                }
            }
            return target;
        }
    }
}
