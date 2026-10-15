using ProjectLeviathan.Splines;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SurfaceSplineMesh))]
public sealed class SurfaceSplineMeshEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var spline = (SurfaceSplineMesh)target;
        EditorGUILayout.HelpBox("Scene: move os pontos pelas alcas. Shift + clique adiciona um ponto na superficie. Undo disponivel.", MessageType.Info);
        if (GUILayout.Button("Reconstruir malha")) spline.Rebuild();
        if (GUILayout.Button("Adicionar ponto"))
        {
            Undo.RecordObject(spline, "Add spline knot");
            var last = spline.knots.Count > 0 ? spline.knots[spline.knots.Count - 1] : new SurfaceSplineKnot(Vector3.zero, Vector3.up);
            last.position += Vector3.forward * 3; spline.knots.Add(last); Changed(spline);
        }
        if (spline.LastError != null) EditorGUILayout.HelpBox(spline.LastError, MessageType.Error);
        EditorGUILayout.LabelField("Amostras sem superficie", spline.MissedSamples.ToString());
        EditorGUILayout.LabelField("Triangulos rejeitados", spline.RejectedTriangles.ToString());
        using (new EditorGUI.DisabledScope(spline.GeneratedMesh == null))
            if (GUILayout.Button("Exportar copia da malha (.asset)"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Export projected spline", "SurfaceSpline", "asset", "Mesh asset");
                if (!string.IsNullOrEmpty(path))
                {
                    var mesh = Instantiate(spline.GeneratedMesh); mesh.hideFlags = HideFlags.None;
                    AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(path)); AssetDatabase.SaveAssets();
                }
            }
    }
    void OnSceneGUI()
    {
        var spline = (SurfaceSplineMesh)target;
        Handles.color = new Color(.2f, .9f, .75f);
        for (int i = 0; i < spline.knots.Count; i++)
        {
            var knot = spline.knots[i]; Vector3 world = spline.transform.TransformPoint(knot.position);
            Handles.Label(world, "P" + i);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Move spline knot"); knot.position = spline.transform.InverseTransformPoint(moved);
                spline.knots[i] = knot; Changed(spline);
            }
            if (i > 0) Handles.DrawDottedLine(spline.transform.TransformPoint(spline.knots[i-1].position), world, 4);
        }
        Event e = Event.current;
        if (e.shift && !e.alt)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100000, spline.surfaceLayers, QueryTriggerInteraction.Ignore))
                {
                    Undo.RecordObject(spline, "Paint spline knot");
                    spline.knots.Add(new SurfaceSplineKnot(spline.transform.InverseTransformPoint(hit.point),
                        spline.transform.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized));
                    Changed(spline); e.Use();
                }
            }
        }
    }
    static void Changed(SurfaceSplineMesh spline)
    { spline.RequestRebuild(); EditorUtility.SetDirty(spline); PrefabUtility.RecordPrefabInstancePropertyModifications(spline); }
    [MenuItem("GameObject/Leviathan/Spline projetada", false, 10)]
    static void Create(MenuCommand command)
    {
        var go = new GameObject("Spline Projetada"); Undo.RegisterCreatedObjectUndo(go, "Create projected spline");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        var spline = go.AddComponent<SurfaceSplineMesh>();
        spline.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/SplineTrack.mat");
        Selection.activeGameObject = go;
    }
}
