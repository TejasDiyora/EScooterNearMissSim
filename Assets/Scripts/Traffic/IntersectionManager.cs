using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    public enum SignalState { Green = 0, Yellow = 1, Red = 2 }

    /// <summary>
    /// Runs the signal cycle for the whole intersection so that conflicting directions
    /// are never green together:
    ///
    ///   0 North-South green   1 North-South yellow   2 all red
    ///   3 East-West green     4 East-West yellow     5 all red
    ///
    /// The all-red phases give traffic already inside the intersection time to clear.
    /// </summary>
    public class IntersectionManager : MonoBehaviour
    {
        private const int PhaseCount = 6;

        private float greenDuration = 20f;
        private float yellowDuration = 3f;
        private float allRedDuration = 2f;
        private int phase;
        private float timeInPhase;
        private readonly List<TrafficLightController> heads = new List<TrafficLightController>();

        public int Phase { get { return phase; } }

        public void Configure(ScenarioConfig config)
        {
            greenDuration = Mathf.Max(1f, config.greenDuration);
            yellowDuration = Mathf.Max(0.5f, config.yellowDuration);
            allRedDuration = Mathf.Max(0f, config.allRedDuration);
        }

        public void RegisterHead(TrafficLightController head)
        {
            heads.Add(head);
            head.Show(GetState(head.Group));
        }

        public void ResetCycle()
        {
            phase = 0;
            timeInPhase = 0f;
            RefreshHeads();
        }

        public void Tick(float dt)
        {
            timeInPhase += dt;
            bool changed = false;
            int guard = 0;
            while (timeInPhase >= PhaseDuration(phase) && guard < PhaseCount * 2)
            {
                timeInPhase -= PhaseDuration(phase);
                phase = (phase + 1) % PhaseCount;
                changed = true;
                guard++;
            }
            if (changed) RefreshHeads();
        }

        public SignalState GetState(SignalGroup group)
        {
            int greenPhase = GreenPhaseOf(group);
            if (phase == greenPhase) return SignalState.Green;
            if (phase == greenPhase + 1) return SignalState.Yellow;
            return SignalState.Red;
        }

        /// <summary>Seconds until the group next gets a green. Zero if it is green now.</summary>
        public float TimeUntilGreen(SignalGroup group)
        {
            int greenPhase = GreenPhaseOf(group);
            if (phase == greenPhase) return 0f;

            float time = PhaseDuration(phase) - timeInPhase;
            int p = (phase + 1) % PhaseCount;
            while (p != greenPhase)
            {
                time += PhaseDuration(p);
                p = (p + 1) % PhaseCount;
            }
            return time;
        }

        private static int GreenPhaseOf(SignalGroup group)
        {
            return group == SignalGroup.NorthSouth ? 0 : 3;
        }

        private float PhaseDuration(int p)
        {
            switch (p)
            {
                case 0:
                case 3:
                    return greenDuration;
                case 1:
                case 4:
                    return yellowDuration;
                default:
                    return allRedDuration;
            }
        }

        private void RefreshHeads()
        {
            for (int i = 0; i < heads.Count; i++)
            {
                heads[i].Show(GetState(heads[i].Group));
            }
        }
    }
}
