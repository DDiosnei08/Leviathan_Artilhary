using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class ProceduralTerrainBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (UnityEngine.Object.FindAnyObjectByType<TerrainBootstrapOptOut>() != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<TerrainWorldController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            Shader terrainShader = Shader.Find("Universal Render Pipeline/Lit");
            if (terrainShader == null)
            {
                terrainShader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (terrainShader == null)
            {
                Debug.LogError("Could not find a compatible URP terrain shader. Assign a terrain material in TerrainSettings.");
                return;
            }

            GameObject terrainRoot = new GameObject("Project Leviathan Terrain");
            TerrainWorldController controller = terrainRoot.AddComponent<TerrainWorldController>();
            controller.terrainSettings = ProceduralBattlefieldSetup.LoadTerrainSettings();
            controller.terrainMaterial = controller.terrainSettings.terrainMaterial != null
                ? controller.terrainSettings.terrainMaterial
                : new Material(terrainShader);
            controller.terrainSettings.terrainMaterial = controller.terrainMaterial;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                controller.viewer = mainCamera.transform;
            }

            PlacePrototypeRoot("ArtilleryTestGenerated", controller.terrainSettings, new Vector3(600f, 0f, 600f), 0.35f);
            PlacePrototypeRoot("CommandRoomGenerated", controller.terrainSettings, new Vector3(620f, 0f, 590f), 0.18f);
        }

        static void PlacePrototypeRoot(string rootName, TerrainSettings settings, Vector3 worldXZ, float yOffset)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                return;
            }

            ProceduralBattlefieldSetup.PlaceOnTerrain(root.transform, settings, worldXZ, yOffset);
        }
    }
}
