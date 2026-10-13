using System;
using System.IO;
using AyutthayaSimulator.Core;
using UnityEditor;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class ModelImportValidator
    {
        public static AssetValidationReport ValidateSelection(bool writeFile)
        {
            var report = new AssetValidationReport { GeneratedAtUtc = DateTime.UtcNow.ToString("o") };
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                report.Errors.Add("No object selected.");
                Finish(report, writeFile);
                return report;
            }
            report.AssetName = selected.name;
            var renderers = selected.GetComponentsInChildren<Renderer>(true);
            report.RendererCount = renderers.Length;
            var filters = selected.GetComponentsInChildren<MeshFilter>(true);
            report.MeshCount = filters.Length;
            if (filters.Length == 0) report.Errors.Add("No MeshFilter found. A name alone is not evidence of a mesh.");
            for (var i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null) report.Errors.Add(filters[i].name + " has a missing mesh.");
            }
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].sharedMaterial == null) report.MissingMaterialCount++;
            }
            if (report.MissingMaterialCount > 0) report.Warnings.Add(report.MissingMaterialCount + " renderer(s) have no material.");
            var scale = selected.transform.lossyScale;
            if (scale.x > 50f || scale.y > 50f || scale.z > 50f)
                report.Warnings.Add("Lossy scale is very large. Confirm 1 unit = 1 metre before binding.");
            report.Passed = report.Errors.Count == 0;
            Finish(report, writeFile);
            return report;
        }

        static void Finish(AssetValidationReport report, bool writeFile)
        {
            Debug.Log("Asset validation " + (report.Passed ? "passed" : "failed") + " for " + report.AssetName);
            if (!writeFile) return;
            var directory = Path.Combine(Application.dataPath, "_AyutthayaSimulator", "Reports");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "asset-validation.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
