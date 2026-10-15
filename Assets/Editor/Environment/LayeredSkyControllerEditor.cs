using ProjectLeviathan.World;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LayeredSkyController))]
public sealed class LayeredSkyControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var sky=(LayeredSkyController)target;
        EditorGUILayout.Space();
        float hour=sky.CurrentHour;
        EditorGUILayout.LabelField("Horario atual",Mathf.FloorToInt(hour).ToString("00")+":"+Mathf.FloorToInt((hour%1)*60).ToString("00"));
        EditorGUILayout.BeginHorizontal();
        Preset(sky,"00:00",0);Preset(sky,"06:00",6);Preset(sky,"12:00",12);Preset(sky,"18:00",18);
        EditorGUILayout.EndHorizontal();
        if(Application.isPlaying)Repaint();
    }
    static void Preset(LayeredSkyController sky,string label,float hour)
    {
        if(!GUILayout.Button(label,EditorStyles.miniButton))return;
        sky.SetTimeOfDay(hour);SceneView.RepaintAll();
    }
}
