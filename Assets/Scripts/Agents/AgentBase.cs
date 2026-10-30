using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    public enum AgentType { Scooter = 0, Vehicle = 1, Pedestrian = 2 }

    /// <summary>
    /// Shared behaviour for every road user: following a path, speeding up and slowing
    /// down, obeying a signal, and spotting whatever is in the way.
    ///
    /// Each simulation step runs in two passes so the result does not depend on the order
    /// agents are stored in: first every agent decides its target speed from the current
    /// state of the world (Decide), then every agent moves (Move).
    /// </summary>
    public abstract class AgentBase : MonoBehaviour
    {
        public int AgentId { get; private set; }
        public AgentType Kind { get; private set; }
        public Route Route { get; private set; }
        public RoutePath Path { get; private set; }

        /// <summary>Distance travelled along the current path, measured at the body centre.</summary>
        public float DistanceAlong { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Forward { get; private set; }
        public float BodyLength { get; private set; }
        public float BodyWidth { get; private set; }
        public float FootprintRadius { get; private set; }
        public float DistanceTravelled { get; private set; }
        public bool ReachedEnd { get; private set; }

        /// <summary>The agent this one is currently stopped for, if any. Used to spot gridlock.</summary>
        public AgentBase BlockedBy { get; protected set; }

        public Vector3 Velocity { get { return Forward * Speed; } }
        public float HalfLength { get { return BodyLength * 0.5f; } }
        public float FrontDistance { get { return DistanceAlong + HalfLength; } }
        public int FootprintCount { get { return footprintOffsets.Length; } }

        protected SimulationManager sim;
        protected float acceleration = 2f;
        protected float maxBraking = 4f;
        protected float planningDeceleration = 1.5f;

        private float[] footprintOffsets = new float[] { 0f };
        private float targetSpeed;
        private bool transformPlaced;

        public void Initialize(SimulationManager manager, int id, AgentType kind, float length, float width)
        {
            sim = manager;
            AgentId = id;
            Kind = kind;
            BodyLength = length;
            BodyWidth = width;
            Forward = Vector3.forward;

            // The body is approximated on the ground by a row of circles. That keeps the
            // gap and time-to-collision maths simple and exact for the closest circle.
            FootprintRadius = width * 0.5f;
            float span = Mathf.Max(0f, length - width);
            int count = span < 0.05f ? 1 : 1 + Mathf.CeilToInt(span / (1.5f * FootprintRadius));
            footprintOffsets = new float[count];
            for (int i = 0; i < count; i++)
            {
                footprintOffsets[i] = count == 1 ? 0f : -span * 0.5f + span * i / (count - 1);
            }
        }

        public void SetRoute(Route route, RoutePath path, float startDistance, float startSpeed)
        {
            Route = route;
            Path = path;
            DistanceAlong = Mathf.Clamp(startDistance, 0f, path.Length);
            Speed = Mathf.Max(0f, startSpeed);
            targetSpeed = Speed;
            Position = path.PointAt(DistanceAlong);
            Forward = path.DirectionAt(DistanceAlong);
            ReachedEnd = false;
            transformPlaced = false;
            SyncTransform(0f);
        }

        /// <summary>Centre of one of the circles that make up the body footprint.</summary>
        public Vector3 FootprintCenter(int index)
        {
            return Position + Forward * footprintOffsets[index];
        }

        /// <summary>How far along the body, from its centre, footprint circle <paramref name="index"/> sits.</summary>
        public float FootprintOffset(int index)
        {
            return footprintOffsets[index];
        }

        /// <summary>False for a pedestrian who is on the sidewalk or waiting at the kerb.</summary>
        public virtual bool IsOnRoadway
        {
            get { return true; }
        }

        /// <summary>
        /// The speed others should assume when predicting where this agent will be.
        /// Normally its current speed.
        /// </summary>
        public virtual float PredictionSpeed
        {
            get { return Speed; }
        }

        /// <summary>Pass 1 of a step: choose a target speed from the current state of the world.</summary>
        public void Decide(float dt)
        {
            BlockedBy = null;
            targetSpeed = Mathf.Max(0f, ComputeTargetSpeed(dt));
        }

        /// <summary>Pass 2 of a step: accelerate or brake toward the target speed and advance along the path.</summary>
        public void Move(float dt)
        {
            if (targetSpeed < Speed)
            {
                Speed = Mathf.Max(targetSpeed, Speed - maxBraking * dt);
            }
            else
            {
                Speed = Mathf.Min(targetSpeed, Speed + acceleration * dt);
            }
            if (Speed < 0.001f) Speed = 0f;

            float step = Speed * dt;
            DistanceAlong = Mathf.Min(Path.Length, DistanceAlong + step);
            DistanceTravelled += step;
            Position = Path.PointAt(DistanceAlong);
            Forward = Path.DirectionAt(DistanceAlong);
            ReachedEnd = DistanceAlong >= Path.Length - 0.01f;
        }

        /// <summary>Copies the simulated position onto the visible GameObject.</summary>
        public void SyncTransform(float dt)
        {
            Quaternion facing = Quaternion.LookRotation(Forward, Vector3.up);
            if (!transformPlaced || dt <= 0f)
            {
                transform.SetPositionAndRotation(Position, facing);
                transformPlaced = true;
            }
            else
            {
                // Turn smoothly so corners in the path do not snap.
                Quaternion smoothed = Quaternion.RotateTowards(transform.rotation, facing, 240f * dt);
                transform.SetPositionAndRotation(Position, smoothed);
            }
        }

        /// <summary>The speed this agent wants this step. Implemented per agent type.</summary>
        protected abstract float ComputeTargetSpeed(float dt);

        /// <summary>Extra sideways clearance this agent keeps from another agent in its path.</summary>
        protected virtual float CorridorMarginFor(AgentBase other)
        {
            return 0.3f;
        }

        /// <summary>
        /// Highest speed from which the agent can still stop within <paramref name="room"/> metres
        /// at its planning deceleration.
        /// </summary>
        protected float StopSpeedForRoom(float room)
        {
            if (room <= 0.05f) return 0f;
            return Mathf.Sqrt(2f * planningDeceleration * room);
        }

        /// <summary>
        /// Highest safe speed behind something <paramref name="gap"/> metres ahead that is itself
        /// moving away at <paramref name="leadSpeed"/>.
        /// </summary>
        protected float SafeFollowSpeed(float gap, float standoff, float leadSpeed)
        {
            float room = gap - standoff;
            if (room <= 0f) return 0f;
            return Mathf.Sqrt(2f * planningDeceleration * room + leadSpeed * leadSpeed);
        }

        /// <summary>
        /// Speed limit imposed by the traffic signal. Only applies before the stop line:
        /// an agent that has already crossed it keeps going so it never freezes inside
        /// the intersection.
        /// </summary>
        protected float SignalSpeedLimit()
        {
            if (FrontDistance > Route.stopLineDistance + 0.5f) return float.PositiveInfinity;

            SignalState state = sim.Intersection.GetState(Route.signalGroup);
            float room = Route.stopLineDistance - FrontDistance;
            if (state == SignalState.Green)
            {
                // Do not block the box: wait at the line while a car from the cross street
                // is still stuck inside the intersection.
                if (CrossTrafficStuckInBox()) return StopSpeedForRoom(room);
                return float.PositiveInfinity;
            }

            if (state == SignalState.Yellow)
            {
                // Too close to stop comfortably: carry on through the yellow.
                float needed = Speed * Speed / (2f * Mathf.Max(room, 0.1f));
                if (needed > planningDeceleration * 1.5f) return float.PositiveInfinity;
            }
            return StopSpeedForRoom(room);
        }

        /// <summary>True if a slow or stopped car from the other street is inside the intersection.</summary>
        protected bool CrossTrafficStuckInBox()
        {
            float limit = RoadLayout.RoadHalfWidth + 0.5f;
            List<AgentBase> agents = sim.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentBase other = agents[i];
                if (other.Kind != AgentType.Vehicle || other == this) continue;
                if (other.Route.signalGroup == Route.signalGroup) continue;
                if (other.Speed >= 2f) continue;

                Vector3 p = other.Position;
                if (Mathf.Abs(p.x) < limit && Mathf.Abs(p.z) < limit) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether <paramref name="other"/> lies in the strip of road this agent is about
        /// to drive over. If it does, <paramref name="gap"/> is the free distance from this
        /// agent's front to it.
        /// </summary>
        protected bool CorridorGap(AgentBase other, float lookahead, float margin, out float gap)
        {
            gap = float.MaxValue;
            bool blocking = false;
            float halfWidth = BodyWidth * 0.5f;

            // Extra clearance is only kept from things that are moving. Something standing
            // still blocks the way only if it is physically in it. Without this, two agents
            // stopped next to each other's paths could each wait for the other for ever.
            if (other.Speed <= 0.2f) margin = Mathf.Min(margin, 0.05f);

            // Cheap test on the body centre first: every footprint circle lies within half a
            // body length of it, so if the centre is far from the path no circle can be in it.
            float centreAlong;
            float centreLateral;
            if (!Path.ProjectAhead(other.Position, DistanceAlong, lookahead, out centreAlong, out centreLateral)) return false;
            if (centreLateral > halfWidth + other.FootprintRadius + margin + other.HalfLength) return false;

            for (int i = 0; i < other.FootprintCount; i++)
            {
                Vector3 c = other.FootprintCenter(i);
                float along;
                float lateral;
                if (!Path.ProjectAhead(c, DistanceAlong, lookahead, out along, out lateral)) continue;

                float ahead = along - DistanceAlong;
                // Beside or behind this agent, not in front of it.
                if (ahead < 0.05f && Vector3.Dot(c - Position, Forward) <= 0f) continue;
                if (lateral > halfWidth + other.FootprintRadius + margin) continue;

                float g = ahead - HalfLength - other.FootprintRadius;
                if (g < gap)
                {
                    gap = g;
                    blocking = true;
                }
            }
            if (blocking && gap < 0f) gap = 0f;
            return blocking;
        }

        /// <summary>
        /// Whether <paramref name="other"/> is something this agent has to slow down or stop for.
        /// By default that means it is physically in the path ahead.
        /// </summary>
        protected virtual bool IsBlockedBy(AgentBase other, float lookahead, out float gap)
        {
            return CorridorGap(other, lookahead, CorridorMarginFor(other), out gap);
        }

        /// <summary>Finds the nearest agent in this agent's path within <paramref name="lookahead"/> metres.</summary>
        protected bool FindObstacleAhead(float lookahead, out float gap, out AgentBase lead)
        {
            gap = float.MaxValue;
            lead = null;
            List<AgentBase> agents = sim.Agents;

            for (int i = 0; i < agents.Count; i++)
            {
                AgentBase other = agents[i];
                if (other == this) continue;

                Vector3 offset = other.Position - Position;
                float reach = lookahead + HalfLength + other.HalfLength + 3f;
                if (offset.sqrMagnitude > reach * reach) continue;

                // No route doubles back, so anything well behind is never in the path.
                if (Vector3.Dot(offset, Forward) < -(HalfLength + other.HalfLength + 1f)) continue;

                float g;
                if (IsBlockedBy(other, lookahead, out g) && g < gap)
                {
                    gap = g;
                    lead = other;
                }
            }
            return lead != null;
        }
    }
}
