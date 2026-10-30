using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace EScooterSim
{
    /// <summary>
    /// The results dashboard shown when a run ends: headline numbers, a breakdown by type
    /// of conflict, the settings that produced them, and the CSV export.
    /// </summary>
    public class ResultsDashboardUI : MonoBehaviour
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private SimulationManager sim;
        private RectTransform panel;
        private Text title;
        private Text summary;
        private Text nearMissValue;
        private Text collisionValue;
        private Text closestValue;
        private Text ttcValue;
        private Text rateValue;
        private readonly Text[] nearMissCells = new Text[3];
        private readonly Text[] collisionCells = new Text[3];
        private Text settings;
        private Text status;

        public void Initialize(SimulationManager manager, Transform canvas)
        {
            sim = manager;
            Build(canvas);
            sim.RunFinished += ShowResults;
            sim.RunStarted += Hide;
            sim.SetupEntered += Hide;
            Hide();
        }

        private void OnDestroy()
        {
            if (sim == null) return;
            sim.RunFinished -= ShowResults;
            sim.RunStarted -= Hide;
            sim.SetupEntered -= Hide;
        }

        private void Hide()
        {
            panel.gameObject.SetActive(false);
        }

        private void Build(Transform canvas)
        {
            panel = UIFactory.CreatePanel(canvas, "Results panel", UIFactory.PanelColor);
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160f, 760f));
            UIFactory.AddVerticalLayout(panel.gameObject, 28, 12f);

            title = UIFactory.CreateText(panel, "Run results", 30, TextAnchor.MiddleLeft, UIFactory.TextColor);
            title.fontStyle = FontStyle.Bold;
            UIFactory.SetSize(title.gameObject, -1f, 40f, 1f);

            summary = UIFactory.CreateText(panel, "", 16, TextAnchor.UpperLeft, UIFactory.MutedText);
            UIFactory.SetSize(summary.gameObject, -1f, 44f, 1f);

            // Headline numbers.
            RectTransform tiles = UIFactory.CreateRow(panel, "Tiles", 120f, 12f);
            nearMissValue = AddTile(tiles, "Near-misses", UIFactory.Accent);
            collisionValue = AddTile(tiles, "Collisions", UIFactory.Danger);
            closestValue = AddTile(tiles, "Closest near-miss", UIFactory.TextColor);
            ttcValue = AddTile(tiles, "Lowest time-to-collision", UIFactory.TextColor);
            rateValue = AddTile(tiles, "Events per km ridden", UIFactory.TextColor);

            // Breakdown table.
            Text tableTitle = UIFactory.CreateText(panel, "By type of conflict", 19, TextAnchor.LowerLeft, UIFactory.TextColor);
            tableTitle.fontStyle = FontStyle.Bold;
            UIFactory.SetSize(tableTitle.gameObject, -1f, 34f, 1f);

            AddTableRow("Type", "Near-misses", "Collisions", "What happened", true, -1);
            AddTableRow("Close pass", "0", "0", "A moving vehicle passed a scooter closer than the distance threshold.", false, 0);
            AddTableRow("Pedestrian crossing", "0", "0", "A pedestrian crossed the bike lane in front of a scooter.", false, 1);
            AddTableRow("Turn conflict", "0", "0", "A vehicle turned right across the bike lane.", false, 2);

            settings = UIFactory.CreateText(panel, "", 14, TextAnchor.UpperLeft, UIFactory.MutedText);
            LayoutElement settingsLayout = UIFactory.SetSize(settings.gameObject, -1f, 64f, 1f);
            settingsLayout.flexibleHeight = 1f;

            RectTransform buttons = UIFactory.CreateRow(panel, "Buttons", 48f, 12f);
            Button export = UIFactory.CreateButton(buttons, "Export CSV", UIFactory.PrimaryButton, Export);
            UIFactory.SetSize(export.gameObject, -1f, 48f, 1f);
            Button again = UIFactory.CreateButton(buttons, "Run again", UIFactory.SecondaryButton, RunAgain);
            UIFactory.SetSize(again.gameObject, -1f, 48f, 1f);
            Button nextSeed = UIFactory.CreateButton(buttons, "Run with next seed", UIFactory.SecondaryButton, RunNextSeed);
            UIFactory.SetSize(nextSeed.gameObject, -1f, 48f, 1f);
            Button newScenario = UIFactory.CreateButton(buttons, "Change settings", UIFactory.SecondaryButton, ChangeSettings);
            UIFactory.SetSize(newScenario.gameObject, -1f, 48f, 1f);

            status = UIFactory.CreateText(panel, "", 14, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.SetSize(status.gameObject, -1f, 40f, 1f);
        }

        /// <summary>One headline number with a caption under it. Returns the number's text.</summary>
        private static Text AddTile(Transform row, string caption, Color valueColor)
        {
            RectTransform tile = UIFactory.CreatePanel(row, "Tile " + caption, UIFactory.TileColor);
            UIFactory.SetSize(tile.gameObject, -1f, 120f, 1f);
            UIFactory.AddVerticalLayout(tile.gameObject, 14, 2f);

            Text value = UIFactory.CreateText(tile, "-", 38, TextAnchor.MiddleLeft, valueColor);
            value.fontStyle = FontStyle.Bold;
            UIFactory.SetSize(value.gameObject, -1f, 56f, 1f);

            Text label = UIFactory.CreateText(tile, caption, 15, TextAnchor.UpperLeft, UIFactory.MutedText);
            UIFactory.SetSize(label.gameObject, -1f, 24f, 1f);
            return value;
        }

        private void AddTableRow(string name, string nearMisses, string collisions, string description, bool header, int index)
        {
            RectTransform row = UIFactory.CreateRow(panel, "Row " + name, 28f, 12f);
            Color color = header ? UIFactory.MutedText : UIFactory.TextColor;
            int size = header ? 14 : 16;

            Text nameText = UIFactory.CreateText(row, name, size, TextAnchor.MiddleLeft, color);
            UIFactory.SetSize(nameText.gameObject, 210f, 28f, 0f);

            Text nearMissText = UIFactory.CreateText(row, nearMisses, size, TextAnchor.MiddleLeft, header ? color : UIFactory.Accent);
            UIFactory.SetSize(nearMissText.gameObject, 120f, 28f, 0f);

            Text collisionText = UIFactory.CreateText(row, collisions, size, TextAnchor.MiddleLeft, header ? color : UIFactory.Danger);
            UIFactory.SetSize(collisionText.gameObject, 110f, 28f, 0f);

            Text descriptionText = UIFactory.CreateText(row, description, header ? 14 : 15, TextAnchor.MiddleLeft, UIFactory.MutedText);
            UIFactory.SetSize(descriptionText.gameObject, -1f, 28f, 1f);

            if (index >= 0)
            {
                nearMissCells[index] = nearMissText;
                collisionCells[index] = collisionText;
            }
        }

        private void ShowResults()
        {
            ScenarioConfig config = sim.Config;
            DataRecorder recorder = sim.Recorder;

            title.text = "Run results: " + config.scenarioName;
            summary.text = sim.Scooters.Count + " scooters rode "
                + recorder.ScooterKilometres.ToString("0.00", Invariant) + " km in "
                + recorder.ScooterTrips + " trips over "
                + recorder.Duration.ToString("0", Invariant) + " s. "
                + recorder.EncounterCount + " encounters with vehicles and pedestrians were tracked.";

            nearMissValue.text = recorder.NearMissCount.ToString(Invariant);
            collisionValue.text = recorder.CollisionCount.ToString(Invariant);
            closestValue.text = Finite(recorder.MinGap) ? recorder.MinGap.ToString("0.00", Invariant) + " m" : "-";
            ttcValue.text = Finite(recorder.MinTtc) ? recorder.MinTtc.ToString("0.00", Invariant) + " s" : "-";
            rateValue.text = recorder.EventsPerKilometre.ToString("0.0", Invariant);

            for (int i = 0; i < 3; i++)
            {
                ConflictType type = (ConflictType)i;
                nearMissCells[i].text = recorder.NearMisses(type).ToString(Invariant);
                collisionCells[i].text = recorder.Collisions(type).ToString(Invariant);
            }

            settings.text =
                "Settings   scooters " + config.scooterCount
                + " at " + config.scooterSpeed.ToString("0.0#", Invariant) + " m/s"
                + ", reaction time " + config.reactionTime.ToString("0.0#", Invariant) + " s"
                + ", braking " + config.scooterBrakeDeceleration.ToString("0.0#", Invariant) + " m/s2"
                + "   |   vehicles " + config.vehicleSpeed.ToString("0.0#", Invariant) + " m/s"
                + ", one every " + config.vehicleSpawnInterval.ToString("0.0#", Invariant) + " s per approach"
                + ", passing clearance " + config.vehiclePassingClearance.ToString("0.0#", Invariant) + " m"
                + ", " + Percent(config.rightTurnShare) + " turn right"
                + ", " + Percent(config.driverYieldRate) + " careful drivers"
                + "   |   " + config.pedestrianCount + " pedestrians"
                + ", " + Percent(config.pedestrianComplianceRate) + " wait for the signal"
                + "   |   near-miss if closer than " + config.nearMissDistance.ToString("0.0#", Invariant) + " m"
                + " or time-to-collision below " + config.ttcThreshold.ToString("0.0#", Invariant) + " s"
                + "   |   seed " + config.randomSeed;

            status.text = "";
            panel.gameObject.SetActive(true);
        }

        private void Export()
        {
            try
            {
                string eventsPath = CsvExporter.ExportEvents(sim.RunId, sim.Config, sim.Recorder);
                string summaryPath = CsvExporter.AppendRunSummary(sim.RunId, sim.Config, sim.Recorder);
                status.text = "Saved " + sim.Recorder.Events.Count + " events to " + eventsPath
                    + "\nAdded this run to " + summaryPath;
                Debug.Log("E-Scooter Sim: exported " + eventsPath + " and updated " + summaryPath);
            }
            catch (System.Exception exception)
            {
                status.text = "Export failed: " + exception.Message;
                Debug.LogError("E-Scooter Sim: export failed. " + exception);
            }
        }

        private void RunAgain()
        {
            sim.BeginRun(sim.Config);
        }

        private void RunNextSeed()
        {
            ScenarioConfig next = sim.Config.Clone();
            next.randomSeed = next.randomSeed + 1;
            sim.BeginRun(next);
        }

        private void ChangeSettings()
        {
            sim.ResetToSetup();
        }

        private static bool Finite(float value)
        {
            return !float.IsInfinity(value) && !float.IsNaN(value);
        }

        private static string Percent(float share)
        {
            return Mathf.RoundToInt(share * 100f) + "%";
        }
    }
}
