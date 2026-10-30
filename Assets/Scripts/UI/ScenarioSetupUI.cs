using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EScooterSim
{
    /// <summary>
    /// The setup screen: pick a ready-made scenario or move the sliders, then start a run.
    /// Every slider writes straight into a <see cref="ScenarioConfig"/>.
    /// </summary>
    public class ScenarioSetupUI : MonoBehaviour
    {
        private enum ValueStyle { Number, WholeNumber, Percent }

        /// <summary>Links one slider to one field of the scenario.</summary>
        private class SliderBinding
        {
            public Slider slider;
            public Text valueText;
            public ValueStyle style;
            public string unit;
            public System.Func<ScenarioConfig, float> read;
            public System.Action<ScenarioConfig, float> write;
        }

        private SimulationManager sim;
        private RectTransform panel;
        private Text scenarioLabel;
        private ScenarioConfig working = new ScenarioConfig();
        private string presetName = "Baseline";
        private readonly List<SliderBinding> bindings = new List<SliderBinding>();

        public void Initialize(SimulationManager manager, Transform canvas)
        {
            sim = manager;
            Build(canvas);
            sim.RunStarted += Hide;
            sim.SetupEntered += Show;
            ApplyPreset(new ScenarioConfig());
            Show();
        }

        private void OnDestroy()
        {
            if (sim == null) return;
            sim.RunStarted -= Hide;
            sim.SetupEntered -= Show;
        }

        private void Show()
        {
            panel.gameObject.SetActive(true);
        }

        private void Hide()
        {
            panel.gameObject.SetActive(false);
        }

        private void Build(Transform canvas)
        {
            panel = UIFactory.CreatePanel(canvas, "Setup panel", UIFactory.PanelColor);
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 800f));
            UIFactory.AddVerticalLayout(panel.gameObject, 28, 10f);

            Text title = UIFactory.CreateText(panel, "E-Scooter Near-Miss Simulator", 30, TextAnchor.MiddleLeft, UIFactory.TextColor);
            title.fontStyle = FontStyle.Bold;
            UIFactory.SetSize(title.gameObject, -1f, 40f, 1f);

            Text hint = UIFactory.CreateText(panel,
                "Pick a ready-made scenario or adjust the settings, then start the run.",
                16, TextAnchor.MiddleLeft, UIFactory.MutedText);
            UIFactory.SetSize(hint.gameObject, -1f, 24f, 1f);

            // Preset buttons.
            RectTransform presetRow = UIFactory.CreateRow(panel, "Presets", 40f, 8f);
            List<ScenarioConfig> presets = ScenarioConfig.BuiltInPresets();
            for (int i = 0; i < presets.Count; i++)
            {
                ScenarioConfig preset = presets[i];
                Button button = UIFactory.CreateButton(presetRow, preset.scenarioName, UIFactory.SecondaryButton,
                    delegate { ApplyPreset(preset); });
                UIFactory.SetSize(button.gameObject, -1f, 40f, 1f);
            }

            scenarioLabel = UIFactory.CreateText(panel, "", 17, TextAnchor.MiddleLeft, UIFactory.Accent);
            UIFactory.SetSize(scenarioLabel.gameObject, -1f, 26f, 1f);

            // Two columns of sliders.
            RectTransform columns = UIFactory.CreateRow(panel, "Columns", -1f, 40f);
            columns.GetComponent<LayoutElement>().flexibleHeight = 1f;
            RectTransform left = CreateColumn(columns, "Left column");
            RectTransform right = CreateColumn(columns, "Right column");

            AddHeader(left, "Scooters");
            AddSlider(left, "Number of scooters", 1f, 8f, ValueStyle.WholeNumber, "",
                delegate(ScenarioConfig c) { return c.scooterCount; },
                delegate(ScenarioConfig c, float v) { c.scooterCount = Mathf.RoundToInt(v); });
            AddSlider(left, "Scooter speed", 2f, 10f, ValueStyle.Number, "m/s",
                delegate(ScenarioConfig c) { return c.scooterSpeed; },
                delegate(ScenarioConfig c, float v) { c.scooterSpeed = v; });
            AddSlider(left, "Rider reaction time", 0.2f, 3f, ValueStyle.Number, "s",
                delegate(ScenarioConfig c) { return c.reactionTime; },
                delegate(ScenarioConfig c, float v) { c.reactionTime = v; });
            AddSlider(left, "Hardest braking", 2f, 8f, ValueStyle.Number, "m/s2",
                delegate(ScenarioConfig c) { return c.scooterBrakeDeceleration; },
                delegate(ScenarioConfig c, float v) { c.scooterBrakeDeceleration = v; });

            AddHeader(left, "Vehicles");
            AddSlider(left, "Vehicle speed", 5f, 18f, ValueStyle.Number, "m/s",
                delegate(ScenarioConfig c) { return c.vehicleSpeed; },
                delegate(ScenarioConfig c, float v) { c.vehicleSpeed = v; });
            AddSlider(left, "Time between vehicles", 2f, 15f, ValueStyle.Number, "s",
                delegate(ScenarioConfig c) { return c.vehicleSpawnInterval; },
                delegate(ScenarioConfig c, float v) { c.vehicleSpawnInterval = v; });
            AddSlider(left, "Passing clearance", 0.4f, 1.8f, ValueStyle.Number, "m",
                delegate(ScenarioConfig c) { return c.vehiclePassingClearance; },
                delegate(ScenarioConfig c, float v) { c.vehiclePassingClearance = v; });
            AddSlider(left, "Vehicles turning right", 0f, 0.8f, ValueStyle.Percent, "",
                delegate(ScenarioConfig c) { return c.rightTurnShare; },
                delegate(ScenarioConfig c, float v) { c.rightTurnShare = v; });
            AddSlider(left, "Careful drivers", 0f, 1f, ValueStyle.Percent, "",
                delegate(ScenarioConfig c) { return c.driverYieldRate; },
                delegate(ScenarioConfig c, float v) { c.driverYieldRate = v; });

            AddHeader(right, "Pedestrians");
            AddSlider(right, "Number of pedestrians", 0f, 30f, ValueStyle.WholeNumber, "",
                delegate(ScenarioConfig c) { return c.pedestrianCount; },
                delegate(ScenarioConfig c, float v) { c.pedestrianCount = Mathf.RoundToInt(v); });
            AddSlider(right, "Walking speed", 0.8f, 2f, ValueStyle.Number, "m/s",
                delegate(ScenarioConfig c) { return c.pedestrianSpeed; },
                delegate(ScenarioConfig c, float v) { c.pedestrianSpeed = v; });
            AddSlider(right, "Wait for the walk signal", 0f, 1f, ValueStyle.Percent, "",
                delegate(ScenarioConfig c) { return c.pedestrianComplianceRate; },
                delegate(ScenarioConfig c, float v) { c.pedestrianComplianceRate = v; });

            AddHeader(right, "What counts as a near-miss");
            AddSlider(right, "Passing closer than", 0.3f, 2f, ValueStyle.Number, "m",
                delegate(ScenarioConfig c) { return c.nearMissDistance; },
                delegate(ScenarioConfig c, float v) { c.nearMissDistance = v; });
            AddSlider(right, "Time-to-collision below", 0.5f, 3f, ValueStyle.Number, "s",
                delegate(ScenarioConfig c) { return c.ttcThreshold; },
                delegate(ScenarioConfig c, float v) { c.ttcThreshold = v; });

            AddHeader(right, "Run");
            AddSlider(right, "Run length", 60f, 600f, ValueStyle.WholeNumber, "s",
                delegate(ScenarioConfig c) { return c.runDuration; },
                delegate(ScenarioConfig c, float v) { c.runDuration = Mathf.Round(v); });
            AddSlider(right, "Random seed", 0f, 999f, ValueStyle.WholeNumber, "",
                delegate(ScenarioConfig c) { return c.randomSeed; },
                delegate(ScenarioConfig c, float v) { c.randomSeed = Mathf.RoundToInt(v); });

            // Footer.
            RectTransform footer = UIFactory.CreateRow(panel, "Footer", 52f, 16f);
            Text note = UIFactory.CreateText(footer,
                "The same settings and seed always give the same run, so results can be reproduced.",
                15, TextAnchor.MiddleLeft, UIFactory.MutedText);
            UIFactory.SetSize(note.gameObject, -1f, 52f, 1f);
            Button start = UIFactory.CreateButton(footer, "Start run", UIFactory.PrimaryButton, StartRun);
            UIFactory.SetSize(start.gameObject, 240f, 52f, 0f);
        }

        private static RectTransform CreateColumn(Transform parent, string name)
        {
            RectTransform column = UIFactory.CreateRect(parent, name);
            UIFactory.AddVerticalLayout(column.gameObject, 0, 6f);
            UIFactory.SetSize(column.gameObject, -1f, -1f, 1f);
            return column;
        }

        private static void AddHeader(Transform column, string label)
        {
            Text header = UIFactory.CreateText(column, label, 18, TextAnchor.LowerLeft, UIFactory.TextColor);
            header.fontStyle = FontStyle.Bold;
            UIFactory.SetSize(header.gameObject, -1f, 34f, 1f);
        }

        private void AddSlider(Transform column, string label, float min, float max, ValueStyle style, string unit,
            System.Func<ScenarioConfig, float> read, System.Action<ScenarioConfig, float> write)
        {
            RectTransform row = UIFactory.CreateRow(column, label, 30f, 12f);

            Text labelText = UIFactory.CreateText(row, label, 16, TextAnchor.MiddleLeft, UIFactory.TextColor);
            UIFactory.SetSize(labelText.gameObject, 230f, 30f, 0f);

            Slider slider = UIFactory.CreateSlider(row, min, max, style == ValueStyle.WholeNumber);
            UIFactory.SetSize(slider.gameObject, 120f, 30f, 1f);

            Text valueText = UIFactory.CreateText(row, "", 16, TextAnchor.MiddleRight, UIFactory.Accent);
            UIFactory.SetSize(valueText.gameObject, 92f, 30f, 0f);

            SliderBinding binding = new SliderBinding();
            binding.slider = slider;
            binding.valueText = valueText;
            binding.style = style;
            binding.unit = unit;
            binding.read = read;
            binding.write = write;
            bindings.Add(binding);

            slider.onValueChanged.AddListener(delegate(float value) { OnSliderChanged(binding, value); });
        }

        private void OnSliderChanged(SliderBinding binding, float value)
        {
            // Keep two decimals so the settings are easy to read back and report.
            float rounded = Mathf.Round(value * 100f) / 100f;
            binding.write(working, rounded);
            binding.valueText.text = Describe(binding, rounded);

            working.scenarioName = presetName + " (modified)";
            scenarioLabel.text = "Scenario: " + working.scenarioName;
        }

        private void ApplyPreset(ScenarioConfig preset)
        {
            working = preset.Clone();
            presetName = preset.scenarioName;
            for (int i = 0; i < bindings.Count; i++)
            {
                SliderBinding binding = bindings[i];
                float value = binding.read(working);
                binding.slider.SetValueWithoutNotify(value);
                binding.valueText.text = Describe(binding, value);
            }
            scenarioLabel.text = "Scenario: " + working.scenarioName;
        }

        private static string Describe(SliderBinding binding, float value)
        {
            switch (binding.style)
            {
                case ValueStyle.WholeNumber:
                    return Mathf.RoundToInt(value) + (binding.unit.Length > 0 ? " " + binding.unit : "");
                case ValueStyle.Percent:
                    return Mathf.RoundToInt(value * 100f) + "%";
                default:
                    return value.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)
                        + (binding.unit.Length > 0 ? " " + binding.unit : "");
            }
        }

        private void StartRun()
        {
            sim.BeginRun(working);
        }
    }
}
