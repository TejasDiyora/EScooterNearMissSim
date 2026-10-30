using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// A scenario saved as an asset, so settings can be kept in the project and edited in
    /// the Inspector. Create one from the Project window:
    /// right-click, Create, E-Scooter Sim, Scenario Preset.
    /// </summary>
    [CreateAssetMenu(fileName = "Scenario", menuName = "E-Scooter Sim/Scenario Preset")]
    public class ScenarioPreset : ScriptableObject
    {
        public ScenarioConfig config = new ScenarioConfig();
    }
}
