using ProjectLeviathan.Grass;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassManager))]
public sealed class GrassManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GrassManager manager = (GrassManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("GPU diagnostics (asynchronous, sampled)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Tiles / resident patches", manager.ActiveTiles + " / " + manager.ResidentPatches);
        EditorGUILayout.LabelField("Generated / visible blades", manager.GeneratedBlades + " / " + manager.VisibleBlades);
        EditorGUILayout.LabelField("Pooled buffers", (manager.BufferBytes / 1048576f).ToString("F2") + " MiB");
        EditorGUILayout.LabelField("Tile textures (GPU)", (manager.TextureBytes / 1048576f).ToString("F2") + " MiB");
        EditorGUILayout.LabelField("Grass LateUpdate GC", manager.LastUpdateAllocatedBytes + " bytes");
        EditorGUILayout.LabelField("Indirect API", manager.UsingGraphicsJobsFallback ? "DX11 Graphics Jobs compatibility" : "RenderMeshIndirect");
        EditorGUILayout.LabelField("Terrain tiles LOD 0 / 1 / 2 / 3", string.Join(" / ", manager.tilesByLod));
        if (Application.isPlaying && GUILayout.Button("Rebuild masks and distribution")) manager.Rebuild();
        if (Application.isPlaying) Repaint();
    }
}
