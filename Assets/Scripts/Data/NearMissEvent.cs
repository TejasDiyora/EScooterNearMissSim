namespace EScooterSim
{
    public enum SafetyEventType { NearMiss = 0, Collision = 1 }

    public enum ConflictType { ClosePass = 0, PedestrianCrossing = 1, TurnConflict = 2 }

    /// <summary>One logged near-miss or collision. Becomes one row of the CSV export.</summary>
    public class NearMissEvent
    {
        public string runId;
        public string scenarioName;
        /// <summary>Simulation time of the closest approach, in seconds.</summary>
        public float time;
        public SafetyEventType eventType;
        public ConflictType conflictType;
        /// <summary>Which rule flagged it: Contact, Distance, TTC or Distance+TTC.</summary>
        public string trigger;
        public int scooterId;
        public AgentType otherType;
        public int otherId;
        public float scooterSpeed;
        public float otherSpeed;
        /// <summary>Closest the two bodies came, in metres. Zero for a collision.</summary>
        public float minGap;
        /// <summary>Lowest sustained time-to-collision in seconds. Infinity if it never dropped below the threshold.</summary>
        public float minTtc;
        public SignalState signalState;
        public Heading scooterHeading;
        public float reactionTime;
        public float posX;
        public float posZ;
    }
}
