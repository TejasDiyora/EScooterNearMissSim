using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// One signal head: three lamps that show the state of its signal group, plus a bar
    /// painted on the road in the same colour so the state can be read from above.
    /// The timing lives in <see cref="IntersectionManager"/>. This component only displays it.
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        private static readonly Color RedOn = new Color(1f, 0.12f, 0.1f);
        private static readonly Color YellowOn = new Color(1f, 0.82f, 0.1f);
        private static readonly Color GreenOn = new Color(0.15f, 0.9f, 0.3f);
        private static readonly Color LampOff = new Color(0.12f, 0.12f, 0.12f);

        private Renderer redLamp;
        private Renderer yellowLamp;
        private Renderer greenLamp;
        private Renderer roadBar;

        public SignalGroup Group { get; private set; }
        public SignalState State { get; private set; }

        public void Setup(SignalGroup group, Renderer red, Renderer yellow, Renderer green, Renderer bar)
        {
            Group = group;
            redLamp = red;
            yellowLamp = yellow;
            greenLamp = green;
            roadBar = bar;
            Show(SignalState.Red);
        }

        public void Show(SignalState state)
        {
            State = state;
            SetColor(redLamp, state == SignalState.Red ? RedOn : LampOff);
            SetColor(yellowLamp, state == SignalState.Yellow ? YellowOn : LampOff);
            SetColor(greenLamp, state == SignalState.Green ? GreenOn : LampOff);

            Color barColor = RedOn;
            if (state == SignalState.Green) barColor = GreenOn;
            else if (state == SignalState.Yellow) barColor = YellowOn;
            SetColor(roadBar, barColor);
        }

        private static void SetColor(Renderer target, Color color)
        {
            if (target != null) target.sharedMaterial = MaterialLibrary.Get(color);
        }
    }
}
