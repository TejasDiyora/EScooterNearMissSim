using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Every tunable parameter for one simulation run.
    /// Edited through the setup screen, so a new scenario never needs a code change.
    /// </summary>
    [System.Serializable]
    public class ScenarioConfig
    {
        public string scenarioName = "Baseline";

        [Header("Scooter")]
        [Tooltip("How many e-scooters ride at once. Each one is tracked for near-misses.")]
        public int scooterCount = 4;
        [Tooltip("Cruising speed in m/s. 5.5 m/s is about 20 km/h.")]
        public float scooterSpeed = 5.5f;
        [Tooltip("Seconds between a hazard appearing and the rider starting to respond.")]
        public float reactionTime = 1.0f;
        public float scooterAcceleration = 2.0f;
        [Tooltip("Hardest braking the scooter can do, in m/s^2.")]
        public float scooterBrakeDeceleration = 4.5f;

        [Header("Vehicles")]
        [Tooltip("Cruising speed in m/s. 11 m/s is about 40 km/h.")]
        public float vehicleSpeed = 11f;
        [Tooltip("Average seconds between vehicles on each approach. Lower means heavier traffic.")]
        public float vehicleSpawnInterval = 5f;
        [Tooltip("Average side gap, in metres, that a passing vehicle leaves next to the scooter.")]
        public float vehiclePassingClearance = 1.2f;
        [Tooltip("Share of vehicles that turn right across the bike lane (0 to 1).")]
        public float rightTurnShare = 0.25f;
        [Tooltip("Share of right-turning drivers who check the bike lane, wait for scooters and indicate early (0 to 1).")]
        public float driverYieldRate = 0.6f;

        [Header("Pedestrians")]
        public int pedestrianCount = 12;
        public float pedestrianSpeed = 1.4f;
        [Tooltip("Share of pedestrians who wait for the walk signal (0 to 1). The rest cross on gaps in car traffic.")]
        public float pedestrianComplianceRate = 0.75f;

        [Header("Near-miss definition")]
        [Tooltip("A pass closer than this many metres counts as a near-miss.")]
        public float nearMissDistance = 1.0f;
        [Tooltip("A time-to-collision below this many seconds counts as a near-miss.")]
        public float ttcThreshold = 1.5f;
        [Tooltip("Other road users inside this radius of the scooter are tracked as an encounter.")]
        public float detectionRadius = 12f;

        [Header("Signals")]
        public float greenDuration = 20f;
        public float yellowDuration = 3f;
        public float allRedDuration = 2f;

        [Header("Run")]
        [Tooltip("Length of one run in simulated seconds.")]
        public float runDuration = 180f;
        [Tooltip("Same seed and same settings give the same run.")]
        public int randomSeed = 42;

        public ScenarioConfig Clone()
        {
            return (ScenarioConfig)MemberwiseClone();
        }

        /// <summary>Ready-made scenarios. Each changes one aspect of Baseline.</summary>
        public static List<ScenarioConfig> BuiltInPresets()
        {
            List<ScenarioConfig> presets = new List<ScenarioConfig>();

            ScenarioConfig baseline = new ScenarioConfig();
            presets.Add(baseline);

            ScenarioConfig fast = new ScenarioConfig();
            fast.scenarioName = "Fast scooter";
            fast.scooterSpeed = 8.5f;
            presets.Add(fast);

            ScenarioConfig slowReaction = new ScenarioConfig();
            slowReaction.scenarioName = "Slow reaction";
            slowReaction.reactionTime = 2.0f;
            presets.Add(slowReaction);

            ScenarioConfig riskyPedestrians = new ScenarioConfig();
            riskyPedestrians.scenarioName = "Risky pedestrians";
            riskyPedestrians.pedestrianCount = 18;
            riskyPedestrians.pedestrianComplianceRate = 0.3f;
            presets.Add(riskyPedestrians);

            ScenarioConfig closePassing = new ScenarioConfig();
            closePassing.scenarioName = "Close passing";
            closePassing.vehiclePassingClearance = 0.8f;
            closePassing.vehicleSpawnInterval = 3.5f;
            presets.Add(closePassing);

            ScenarioConfig aggressiveTurns = new ScenarioConfig();
            aggressiveTurns.scenarioName = "Aggressive turns";
            aggressiveTurns.rightTurnShare = 0.5f;
            aggressiveTurns.driverYieldRate = 0.1f;
            // Pedestrians on the crosswalk hold turning cars back at the start of each
            // green, which hides this conflict. Fewer pedestrians lets it show.
            aggressiveTurns.pedestrianCount = 4;
            presets.Add(aggressiveTurns);

            return presets;
        }
    }
}
