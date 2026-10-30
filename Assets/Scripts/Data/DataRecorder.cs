using System.Collections.Generic;

namespace EScooterSim
{
    /// <summary>
    /// Keeps the events of the current run in memory and maintains running totals,
    /// so the HUD and dashboard read numbers instead of recounting the list.
    /// </summary>
    public class DataRecorder
    {
        private const int ConflictTypeCount = 3;

        private readonly List<NearMissEvent> events = new List<NearMissEvent>();
        private readonly int[] nearMissesByConflict = new int[ConflictTypeCount];
        private readonly int[] collisionsByConflict = new int[ConflictTypeCount];
        private float scooterSpeedSum;

        public List<NearMissEvent> Events { get { return events; } }
        public int NearMissCount { get; private set; }
        public int CollisionCount { get; private set; }
        /// <summary>Encounters of any kind, including normal passes. Useful as an exposure measure.</summary>
        public int EncounterCount { get; private set; }
        public float MinGap { get; private set; }
        public float MinTtc { get; private set; }

        // Filled in by the simulation manager.
        public float Duration { get; set; }
        public float ScooterDistance { get; set; }
        public int ScooterTrips { get; set; }

        public DataRecorder()
        {
            Clear();
        }

        public void Clear()
        {
            events.Clear();
            for (int i = 0; i < ConflictTypeCount; i++)
            {
                nearMissesByConflict[i] = 0;
                collisionsByConflict[i] = 0;
            }
            scooterSpeedSum = 0f;
            NearMissCount = 0;
            CollisionCount = 0;
            EncounterCount = 0;
            MinGap = float.PositiveInfinity;
            MinTtc = float.PositiveInfinity;
            Duration = 0f;
            ScooterDistance = 0f;
            ScooterTrips = 0;
        }

        public void CountEncounter()
        {
            EncounterCount++;
        }

        public void Record(NearMissEvent e)
        {
            events.Add(e);
            scooterSpeedSum += e.scooterSpeed;

            int index = (int)e.conflictType;
            if (e.eventType == SafetyEventType.Collision)
            {
                CollisionCount++;
                collisionsByConflict[index]++;
            }
            else
            {
                NearMissCount++;
                nearMissesByConflict[index]++;
                if (e.minGap < MinGap) MinGap = e.minGap;
                if (e.minTtc < MinTtc) MinTtc = e.minTtc;
            }
        }

        public int NearMisses(ConflictType type)
        {
            return nearMissesByConflict[(int)type];
        }

        public int Collisions(ConflictType type)
        {
            return collisionsByConflict[(int)type];
        }

        public float AverageScooterSpeedAtEvents
        {
            get { return events.Count > 0 ? scooterSpeedSum / events.Count : 0f; }
        }

        public float ScooterKilometres
        {
            get { return ScooterDistance / 1000f; }
        }

        /// <summary>Near-misses and collisions per kilometre ridden.</summary>
        public float EventsPerKilometre
        {
            get
            {
                float km = ScooterKilometres;
                return km > 0.001f ? events.Count / km : 0f;
            }
        }
    }
}
