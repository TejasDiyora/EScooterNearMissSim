using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Puts road users into the simulation: the scooters once, vehicles on a timer at each
    /// approach, and pedestrians so that the configured number is always walking.
    /// All random choices come from the run's seeded generator, so a run can be repeated.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        public const int MaxVehicles = 60;
        public const int MaxScooters = 12;
        private const float VehicleEntryClearRadius = 14f;
        private const float ScooterEntryClearRadius = 8f;

        private SimulationManager sim;
        private readonly float[] nextVehicleTime = new float[4];
        private float nextPedestrianTime;

        public void Initialize(SimulationManager manager)
        {
            sim = manager;
        }

        public void BeginRun()
        {
            ScenarioConfig config = sim.Config;
            float interval = Mathf.Max(1f, config.vehicleSpawnInterval);
            for (int i = 0; i < nextVehicleTime.Length; i++)
            {
                nextVehicleTime[i] = sim.RandomRange(0.5f, interval);
            }
            nextPedestrianTime = 0f;

            // Scooters start spread over the four approaches.
            int scooterCount = Mathf.Clamp(config.scooterCount, 1, MaxScooters);
            int firstApproach = sim.RandomInt(4);
            for (int i = 0; i < scooterCount; i++)
            {
                SpawnScooter((firstApproach + i) % 4, 20f * (i / 4));
            }

            for (int i = 0; i < config.pedestrianCount; i++)
            {
                SpawnPedestrian();
            }
        }

        public void Tick()
        {
            ScenarioConfig config = sim.Config;
            float interval = Mathf.Max(1f, config.vehicleSpawnInterval);

            for (int i = 0; i < nextVehicleTime.Length; i++)
            {
                if (sim.SimTime < nextVehicleTime[i]) continue;

                Heading heading = (Heading)i;
                Vector3 entry = RoadLayout.Dir(heading) * -RoadLayout.RouteReach
                    + RoadLayout.Right(heading) * RoadLayout.VehicleLaneCenter;

                if (sim.CountAgents(AgentType.Vehicle) < MaxVehicles
                    && EntryClear(entry, AgentType.Vehicle, VehicleEntryClearRadius))
                {
                    SpawnVehicle(heading);
                    nextVehicleTime[i] = sim.SimTime + interval * sim.RandomRange(0.5f, 1.5f);
                }
                else
                {
                    nextVehicleTime[i] = sim.SimTime + 0.5f;
                }
            }

            if (sim.SimTime >= nextPedestrianTime && sim.CountAgents(AgentType.Pedestrian) < config.pedestrianCount)
            {
                SpawnPedestrian();
                nextPedestrianTime = sim.SimTime + sim.RandomRange(0.5f, 2.5f);
            }
        }

        /// <summary>
        /// Moves a scooter that has finished a trip to the start of its next one.
        /// Trips cycle through the four approaches.
        /// </summary>
        public void StartNextScooterTrip(ScooterController scooter)
        {
            // Take the next approach in the cycle whose entry is not occupied by another scooter.
            int index = scooter.RouteIndex;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                index = (index + 1) % 4;
                Heading heading = (Heading)index;
                Vector3 entry = RoadLayout.Dir(heading) * -RoadLayout.RouteReach
                    + RoadLayout.Right(heading) * RoadLayout.BikeLaneCenter;
                if (EntryClear(entry, AgentType.Scooter, ScooterEntryClearRadius)) break;
            }

            Route route = RoadLayout.ScooterStraight((Heading)index);
            RoutePath path = sim.PlanPath(route);
            scooter.RouteIndex = index;
            scooter.TripsCompleted = scooter.TripsCompleted + 1;
            scooter.ForgetHazards();
            scooter.SetRoute(route, path, 0f, scooter.Speed);
        }

        private bool EntryClear(Vector3 entry, AgentType kind, float radius)
        {
            List<AgentBase> agents = sim.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentBase other = agents[i];
                if (other.Kind != kind) continue;
                if ((other.Position - entry).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        private void SpawnScooter(int routeIndex, float startDistance)
        {
            ScenarioConfig config = sim.Config;
            Route route = RoadLayout.ScooterStraight((Heading)routeIndex);
            RoutePath path = sim.PlanPath(route);

            int id = sim.NextAgentId();
            ScooterController scooter = AgentFactory.CreateScooter(sim.AgentRoot, sim.Scooters.Count == 0);
            scooter.Initialize(sim, id, AgentType.Scooter, RoadLayout.ScooterLength, RoadLayout.ScooterWidth);
            scooter.Configure(config);
            scooter.RouteIndex = routeIndex;
            scooter.SetRoute(route, path, startDistance, config.scooterSpeed);
            sim.RegisterAgent(scooter);
            sim.RegisterScooter(scooter);
        }

        private void SpawnVehicle(Heading heading)
        {
            ScenarioConfig config = sim.Config;
            bool turnsRight = sim.RandomValue() < config.rightTurnShare;
            bool yields = sim.RandomValue() < config.driverYieldRate;
            float speed = config.vehicleSpeed * sim.RandomRange(0.9f, 1.1f);
            float clearance = config.vehiclePassingClearance + sim.RandomRange(-0.3f, 0.3f);
            // Careful drivers indicate well before the turn. Of the others, half indicate
            // only in the last few metres and half not until they are already turning.
            float leadRoll = sim.RandomValue();
            float signalRoll = sim.RandomValue();
            float indicatorLead;
            if (yields) indicatorLead = 15f + 20f * leadRoll;
            else indicatorLead = signalRoll < 0.5f ? 0f : 8f * leadRoll;

            Route route;
            if (turnsRight)
            {
                route = RoadLayout.VehicleRightTurn(heading);
            }
            else
            {
                // Shift the car sideways in its lane so it leaves the wanted gap to the bike lane.
                float shift = Mathf.Clamp(RoadLayout.NominalPassingGap - clearance,
                    RoadLayout.MinLaneShift, RoadLayout.MaxLaneShift);
                route = RoadLayout.VehicleStraight(heading, shift);
            }
            RoutePath path = sim.PlanPath(route);

            int id = sim.NextAgentId();
            VehicleController vehicle = AgentFactory.CreateVehicle(sim.AgentRoot, id);
            vehicle.Initialize(sim, id, AgentType.Vehicle, RoadLayout.VehicleLength, RoadLayout.VehicleWidth);
            vehicle.Configure(speed, yields, indicatorLead);
            vehicle.SetRoute(route, path, 0f, speed * 0.7f);
            sim.RegisterAgent(vehicle);
        }

        private void SpawnPedestrian()
        {
            ScenarioConfig config = sim.Config;
            Heading arm = (Heading)sim.RandomInt(4);
            int side = sim.RandomValue() < 0.5f ? 1 : -1;
            float startDistance = sim.RandomRange(14f, 45f);
            float endDistance = sim.RandomRange(20f, 45f);
            bool compliant = sim.RandomValue() < config.pedestrianComplianceRate;
            float speed = config.pedestrianSpeed * sim.RandomRange(0.85f, 1.15f);

            Route route = RoadLayout.PedestrianCrossing(arm, side, startDistance, endDistance);
            RoutePath path = sim.PlanPath(route);

            int id = sim.NextAgentId();
            PedestrianController pedestrian = AgentFactory.CreatePedestrian(sim.AgentRoot, compliant);
            pedestrian.Initialize(sim, id, AgentType.Pedestrian, RoadLayout.PedestrianSize, RoadLayout.PedestrianSize);
            pedestrian.Configure(speed, compliant);
            pedestrian.SetRoute(route, path, 0f, speed);
            sim.RegisterAgent(pedestrian);
        }
    }
}
