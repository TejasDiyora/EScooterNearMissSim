using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EScooterSim
{
    /// <summary>
    /// The panel shown during a run: live counters, playback speed, and camera controls.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        private static readonly float[] SpeedOptions = { 1f, 2f, 5f, 10f };

        private SimulationManager sim;
        private CameraRig rig;
        private RectTransform panel;
        private Text stats;
        private Text cameraButtonLabel;
        private readonly StringBuilder builder = new StringBuilder();

        public void Initialize(SimulationManager manager, CameraRig cameraRig, Transform canvas)
        {
            sim = manager;
            rig = cameraRig;
            Build(canvas);
            sim.RunStarted += Show;
            sim.RunFinished += Hide;
            sim.SetupEntered += Hide;
            Hide();
        }

        private void OnDestroy()
        {
            if (sim == null) return;
            sim.RunStarted -= Show;
            sim.RunFinished -= Hide;
            sim.SetupEntered -= Hide;
        }

        private void Show()
        {
            panel.gameObject.SetActive(true);
            RefreshCameraLabel();
            Refresh();
        }

        private void Hide()
        {
            panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (sim != null && sim.State == SimState.Running) Refresh();
        }

        private void Build(Transform canvas)
        {
            panel = UIFactory.CreatePanel(canvas, "HUD panel", UIFactory.PanelColor);
            UIFactory.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(390f, 440f));
            UIFactory.AddVerticalLayout(panel.gameObject, 16, 8f);

            stats = UIFactory.CreateText(panel, "", 17, TextAnchor.UpperLeft, UIFactory.TextColor);
            UIFactory.SetSize(stats.gameObject, -1f, 170f, 1f);

            RectTransform speedRow = UIFactory.CreateRow(panel, "Speed", 34f, 6f);
            Text speedLabel = UIFactory.CreateText(speedRow, "Speed", 15, TextAnchor.MiddleLeft, UIFactory.MutedText);
            UIFactory.SetSize(speedLabel.gameObject, 60f, 34f, 0f);
            for (int i = 0; i < SpeedOptions.Length; i++)
            {
                float speed = SpeedOptions[i];
                Button button = UIFactory.CreateButton(speedRow, speed.ToString("0", CultureInfo.InvariantCulture) + "x",
                    UIFactory.SecondaryButton, delegate { Time.timeScale = speed; });
                UIFactory.SetSize(button.gameObject, -1f, 34f, 1f);
            }

            RectTransform runRow = UIFactory.CreateRow(panel, "Run controls", 34f, 6f);
            Button skip = UIFactory.CreateButton(runRow, "Skip to end", UIFactory.SecondaryButton, SkipToEnd);
            UIFactory.SetSize(skip.gameObject, -1f, 34f, 1f);
            Button stop = UIFactory.CreateButton(runRow, "End run now", UIFactory.SecondaryButton, EndRun);
            UIFactory.SetSize(stop.gameObject, -1f, 34f, 1f);

            RectTransform cameraRow = UIFactory.CreateRow(panel, "Camera", 34f, 6f);
            Button cameraButton = UIFactory.CreateButton(cameraRow, "Camera", UIFactory.SecondaryButton, ToggleCamera);
            UIFactory.SetSize(cameraButton.gameObject, -1f, 34f, 1f);
            cameraButtonLabel = cameraButton.GetComponentInChildren<Text>();

            Text legend = UIFactory.CreateText(panel,
                "Orange rider: scooter 1. Yellow riders: other scooters.\n"
                + "Blue walkers wait for the signal. Pink walkers cross on gaps.\n"
                + "Road discs: yellow near-miss, red collision.",
                13, TextAnchor.UpperLeft, UIFactory.MutedText);
            UIFactory.SetSize(legend.gameObject, -1f, 60f, 1f);
        }

        private void SkipToEnd()
        {
            sim.RunToEnd();
        }

        private void EndRun()
        {
            sim.EndRun();
        }

        private void ToggleCamera()
        {
            if (rig == null) return;
            rig.ToggleFollow();
            RefreshCameraLabel();
        }

        private void RefreshCameraLabel()
        {
            if (cameraButtonLabel == null || rig == null) return;
            cameraButtonLabel.text = rig.FollowScooter ? "Camera: following scooter 1" : "Camera: overview";
        }

        private void Refresh()
        {
            ScenarioConfig config = sim.Config;
            DataRecorder recorder = sim.Recorder;
            NearMissDetector detector = sim.Detector;

            // Count conflicts in encounters that are still open, so the numbers move the
            // moment something happens instead of when the encounter is closed.
            int nearMisses = recorder.NearMissCount + detector.PendingNearMisses;
            int collisions = recorder.CollisionCount + detector.PendingCollisions;

            builder.Length = 0;
            builder.Append("<b>").Append(config.scenarioName).Append("</b>\n");
            builder.Append("Time  ").Append(Clock(sim.SimTime)).Append(" / ").Append(Clock(config.runDuration)).Append('\n');
            builder.Append("Near-misses  <color=#FFCC40><b>").Append(nearMisses).Append("</b></color>\n");
            builder.Append("Collisions  <color=#FF6B5C><b>").Append(collisions).Append("</b></color>\n");

            ScooterController scooter = sim.PrimaryScooter;
            if (scooter != null && scooter.Route != null)
            {
                SignalState signal = sim.Intersection.GetState(scooter.Route.signalGroup);
                builder.Append("Scooter 1  ")
                    .Append(scooter.Speed.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(" m/s, signal ").Append(signal.ToString().ToLowerInvariant());
                if (scooter.IsBraking) builder.Append(", braking hard");
                builder.Append('\n');
            }

            builder.Append("On the road  ")
                .Append(sim.Scooters.Count).Append(" scooters, ")
                .Append(sim.CountAgents(AgentType.Vehicle)).Append(" vehicles, ")
                .Append(sim.CountAgents(AgentType.Pedestrian)).Append(" pedestrians\n");
            builder.Append("Playback  ").Append(Time.timeScale.ToString("0", CultureInfo.InvariantCulture)).Append("x");

            stats.text = builder.ToString();
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
