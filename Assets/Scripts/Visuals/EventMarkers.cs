using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Drops a coloured disc on the road wherever an event is logged: yellow for a
    /// near-miss, red for a collision. By the end of a run the discs show where the
    /// conflicts cluster.
    /// </summary>
    public class EventMarkers : MonoBehaviour
    {
        public static readonly Color NearMissColor = new Color(1f, 0.8f, 0.1f);
        public static readonly Color CollisionColor = new Color(0.95f, 0.15f, 0.12f);

        private SimulationManager sim;
        private readonly List<GameObject> markers = new List<GameObject>();

        public void Initialize(SimulationManager manager)
        {
            sim = manager;
            sim.EventRecorded += OnEventRecorded;
            sim.RunStarted += Clear;
            sim.SetupEntered += Clear;
        }

        private void OnDestroy()
        {
            if (sim == null) return;
            sim.EventRecorded -= OnEventRecorded;
            sim.RunStarted -= Clear;
            sim.SetupEntered -= Clear;
        }

        private void OnEventRecorded(NearMissEvent e)
        {
            bool collision = e.eventType == SafetyEventType.Collision;
            // Unity's cylinder is 2 units tall, so this disc is 4 cm thick.
            GameObject marker = Shapes.Create(transform, PrimitiveType.Cylinder, collision ? "Collision" : "Near-miss",
                new Vector3(e.posX, collision ? 0.09f : 0.08f, e.posZ),
                new Vector3(collision ? 1.8f : 1.3f, 0.02f, collision ? 1.8f : 1.3f),
                collision ? CollisionColor : NearMissColor);
            markers.Add(marker);
        }

        private void Clear()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null) Destroy(markers[i]);
            }
            markers.Clear();
        }
    }
}
