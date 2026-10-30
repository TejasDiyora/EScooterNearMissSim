using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EScooterSim.EditorTools
{
    /// <summary>
    /// Adds a menu item that creates a ready-to-play scene:
    /// Tools, E-Scooter Sim, Create Simulation Scene.
    /// </summary>
    public static class CreateSimulationScene
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Intersection.unity";

        [MenuItem("Tools/E-Scooter Sim/Create Simulation Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject root = new GameObject("E-Scooter Simulation");
            root.AddComponent<SimulationBootstrap>();

            if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("E-Scooter Sim",
                "The scene was saved to " + ScenePath + ".\n\nPress Play to start the simulator.", "OK");
        }
    }
}
