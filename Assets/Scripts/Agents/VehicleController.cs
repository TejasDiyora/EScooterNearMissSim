using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// A car. It keeps its lane, stops at red, keeps a gap behind whatever is in its path,
    /// and slows for a right turn. Drivers react instantly to what is in front of them:
    /// the reaction-time variable under study belongs to the scooter riders.
    /// </summary>
    public class VehicleController : AgentBase
    {
        public const float Lookahead = 40f;
        public const float TurnSpeed = 4.5f;
        public const float HeadwaySeconds = 0.8f;
        public const float GridlockPatience = 12f;

        /// <summary>True if this driver checks the bike lane before turning right.</summary>
        public bool YieldsToBikeLane { get; private set; }

        private float cruiseSpeed;
        private float indicatorLeadDistance;
        private GameObject indicatorLamp;

        // Gridlock breaker (see ComputeTargetSpeed).
        private float stuckTime;
        private int ignoredAgentId = -1;
        private float ignoreUntil;

        /// <param name="indicatorLead">How many metres before the turn this driver starts indicating.</param>
        public void Configure(float speed, bool yieldsToBikeLane, float indicatorLead)
        {
            cruiseSpeed = Mathf.Max(1f, speed);
            YieldsToBikeLane = yieldsToBikeLane;
            indicatorLeadDistance = Mathf.Max(0f, indicatorLead);
            acceleration = 2.5f;
            maxBraking = 7f;
            planningDeceleration = 2f;
        }

        public void SetIndicatorLamp(GameObject lamp)
        {
            indicatorLamp = lamp;
            if (indicatorLamp != null) indicatorLamp.SetActive(false);
        }

        /// <summary>True while the right indicator is flashing: from shortly before the turn until it is finished.</summary>
        public bool IndicatorOn
        {
            get
            {
                Route route = Route;
                if (route == null || route.maneuver != Maneuver.RightTurn) return false;
                return DistanceAlong >= route.turnStartDistance - indicatorLeadDistance
                    && DistanceAlong <= route.turnEndDistance;
            }
        }

        /// <summary>True from just before the right turn starts until just after it ends.</summary>
        public bool IsInTurn
        {
            get
            {
                Route route = Route;
                if (route == null || route.maneuver != Maneuver.RightTurn) return false;
                return DistanceAlong >= route.turnStartDistance - 3f
                    && DistanceAlong <= route.turnEndDistance + 2f;
            }
        }

        /// <summary>
        /// A car that is indicating and free to go will be moving at turning pace shortly,
        /// even if it is only just pulling away. A car held at a red light stays put.
        /// </summary>
        public override float PredictionSpeed
        {
            get
            {
                if (!IndicatorOn) return Speed;
                bool heldAtSignal = FrontDistance <= Route.stopLineDistance + 0.5f
                    && sim.Intersection.GetState(Route.signalGroup) != SignalState.Green;
                return heldAtSignal ? Speed : Mathf.Max(Speed, 3f);
            }
        }

        private void LateUpdate()
        {
            if (indicatorLamp == null) return;
            bool lit = IndicatorOn && Mathf.Repeat(Time.time, 0.7f) < 0.35f;
            if (indicatorLamp.activeSelf != lit) indicatorLamp.SetActive(lit);
        }

        protected override float CorridorMarginFor(AgentBase other)
        {
            // Cars pass a scooter in the next lane without slowing, but once the turn
            // swings across the bike lane a moving scooter there is in the way. A scooter
            // that has stopped to let the car through gets only the narrow margin,
            // otherwise each would wait for the other for ever.
            if (other.Kind == AgentType.Scooter) return (IsInTurn && other.Speed > 0.5f) ? 0.6f : 0.15f;
            return 0.3f;
        }

        protected override bool IsBlockedBy(AgentBase other, float lookahead, out float gap)
        {
            if (other.Kind != AgentType.Pedestrian) return base.IsBlockedBy(other, lookahead, out gap);

            // Someone on the sidewalk is not in the way.
            gap = float.MaxValue;
            if (!other.IsOnRoadway) return false;

            // Already in the car's path?
            if (CorridorGap(other, lookahead, 0.3f, out gap)) return true;

            // Not yet. Give way only if the pedestrian would step into the path before
            // the car has gone by.
            float along;
            float lateral;
            if (!Path.ProjectAhead(other.Position, DistanceAlong, lookahead, out along, out lateral)) return false;

            float ahead = along - DistanceAlong;
            if (ahead < 0.05f) return false;                                 // beside or behind

            Vector3 toPath = Path.PointAt(along) - other.Position;
            if (Vector3.Dot(other.Velocity, toPath) <= 0f) return false;     // standing or walking away

            float reach = BodyWidth * 0.5f + other.FootprintRadius + 0.3f;
            float pedestrianTime = (lateral - reach) / Mathf.Max(other.Speed, 0.5f);
            float clearTime = (ahead + HalfLength + other.FootprintRadius + 1f) / Mathf.Max(Speed, 1f);
            if (pedestrianTime > clearTime + 0.5f) return false;

            gap = Mathf.Max(0f, ahead - HalfLength - other.FootprintRadius);
            return true;
        }

        protected override float ComputeTargetSpeed(float dt)
        {
            float target = cruiseSpeed;
            Route route = Route;

            if (route.maneuver == Maneuver.RightTurn)
            {
                if (DistanceAlong < route.turnStartDistance)
                {
                    float room = route.turnStartDistance - DistanceAlong;
                    target = Mathf.Min(target, Mathf.Sqrt(TurnSpeed * TurnSpeed + 2f * planningDeceleration * room));
                }
                else if (DistanceAlong <= route.turnEndDistance)
                {
                    target = Mathf.Min(target, TurnSpeed);
                }
            }

            target = Mathf.Min(target, SignalSpeedLimit());

            float gap;
            AgentBase lead;
            if (FindObstacleAhead(Lookahead, out gap, out lead))
            {
                BlockedBy = lead;

                // Gridlock breaker. Road users can end up stopped in a ring, each waiting
                // for the next: a car nose to side with a scooter after a near-miss, say.
                // After a while the car edges through at walking pace. It should almost
                // never trigger: its job is to make sure a run can never freeze.
                bool ignoring = lead.AgentId == ignoredAgentId && sim.SimTime < ignoreUntil;
                if (Speed < 0.1f && WaitsInARingWith(lead)) stuckTime += dt;
                else if (!ignoring) stuckTime = 0f;

                if (stuckTime > GridlockPatience + (AgentId % 5))
                {
                    ignoredAgentId = lead.AgentId;
                    ignoreUntil = sim.SimTime + 6f;
                    stuckTime = 0f;
                    ignoring = true;
                }

                if (ignoring)
                {
                    target = Mathf.Min(target, 0.8f);
                }
                else
                {
                    float leadSpeed = Mathf.Max(0f, Vector3.Dot(lead.Velocity, Forward));
                    float standoff = (lead.Kind == AgentType.Vehicle ? 2.5f : 3.5f) + HeadwaySeconds * Speed;
                    target = Mathf.Min(target, SafeFollowSpeed(gap, standoff, leadSpeed));
                }
            }
            else
            {
                stuckTime = 0f;
            }

            // A careful driver who has not yet crossed the stop line waits there for a
            // scooter that the turn would cut off. Past the line there is nowhere to wait
            // without blocking the crosswalk, so the driver carries on.
            if (route.maneuver == Maneuver.RightTurn && YieldsToBikeLane
                && FrontDistance <= route.stopLineDistance + 0.3f && ShouldYieldToScooter())
            {
                target = Mathf.Min(target, StopSpeedForRoom(route.stopLineDistance - FrontDistance));
            }

            return target;
        }

        /// <summary>
        /// Follows the chain "who is this one stopped for" starting at <paramref name="lead"/>.
        /// True if everyone in the chain is standing still and it leads back to this car.
        /// </summary>
        private bool WaitsInARingWith(AgentBase lead)
        {
            AgentBase current = lead;
            for (int i = 0; i < 6 && current != null; i++)
            {
                if (current.Speed >= 0.1f) return false;
                current = current.BlockedBy;
                if (current == this) return true;
            }
            return false;
        }

        /// <summary>
        /// A careful driver's mirror check: is a scooter going to reach the point where
        /// this turn crosses the bike lane before the car has cleared it?
        /// </summary>
        private bool ShouldYieldToScooter()
        {
            List<ScooterController> scooters = sim.Scooters;
            for (int i = 0; i < scooters.Count; i++)
            {
                if (WouldCutOff(scooters[i])) return true;
            }
            return false;
        }

        private bool WouldCutOff(ScooterController scooter)
        {
            if (scooter == null || scooter.Route == null) return false;
            if (scooter.Route.heading != Route.heading) return false;

            Vector3 d = RoadLayout.Dir(Route.heading);
            float conflictAlong = Vector3.Dot(Route.conflictPoint, d);
            float scooterAlong = Vector3.Dot(scooter.Position, d);
            float scooterToConflict = conflictAlong - scooterAlong;

            if (scooterToConflict < -1.5f) return false;             // scooter is already past
            float scooterTime = Mathf.Max(0f, scooterToConflict) / Mathf.Max(scooter.Speed, 2f);
            if (scooterTime > 4f) return false;                      // scooter is still far away

            float ownTime = Mathf.Max(0f, Route.conflictDistance - DistanceAlong) / Mathf.Max(Speed, 4f);
            return scooterTime + 1.5f > ownTime;
        }
    }
}
