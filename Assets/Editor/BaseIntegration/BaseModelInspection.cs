using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BaseModelInspection
{
    public const string ModelPath = "Assets/Models/World/BaseGiratoria/BaseCompleta.fbx";
    public static void InspectBatch()
    {
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.bakeAxisConversion = true;
        importer.importAnimation = false;
        importer.preserveHierarchy = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var report = new StringBuilder();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, model.transform);
            report.AppendLine(path + " | pos=" + t.localPosition.ToString("F3") + " rot=" + t.localEulerAngles.ToString("F2") + " scale=" + t.localScale.ToString("F3") + " forward=" + t.forward.ToString("F2") + " up=" + t.up.ToString("F2"));
        }
        foreach (MeshFilter mesh in model.GetComponentsInChildren<MeshFilter>())
            report.AppendLine("MESH " + mesh.name + " vertices=" + mesh.sharedMesh.vertexCount + " bounds=" + mesh.sharedMesh.bounds);
        Directory.CreateDirectory("Artifacts/BaseIntegration");
        File.WriteAllText("Artifacts/BaseIntegration/ModelHierarchy.txt", report.ToString());
        Debug.Log("BASE_MODEL_INSPECTED: " + model.GetComponentsInChildren<Transform>(true).Length + " nodes.");
    }
}
