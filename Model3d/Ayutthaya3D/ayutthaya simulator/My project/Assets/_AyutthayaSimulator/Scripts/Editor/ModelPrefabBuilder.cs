using System.IO;
using UnityEditor;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class ModelPrefabBuilder
    {
        public static void CreateFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("Select a configured model before generating a prefab.");
                return;
            }
            var validation = ModelImportValidator.ValidateSelection(false);
            if (!validation.Passed)
            {
                Debug.LogWarning("Prefab was not created because validation failed.");
                return;
            }
            var directory = "Assets/_AyutthayaSimulator/Prefabs";
            Directory.CreateDirectory(directory);
            var path = directory + "/" + selected.name + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(selected, path, InteractionMode.UserAction);
            Debug.Log("Prefab written to " + path);
        }
    }

    public static class BuildingPrefabBuilder
    {
        [MenuItem("Tools/Ayutthaya Simulator/Create Procedural Building Prefab")]
        public static void CreateProcedural()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ProceduralBuildingPlaceholder";
            go.AddComponent<BuildingMetadata>().Confidence = "placeholder_not_a_real_building";
            go.AddComponent<BuildingController>();
            var directory = "Assets/_AyutthayaSimulator/Prefabs/Buildings";
            Directory.CreateDirectory(directory);
            var path = directory + "/ProceduralBuildingPlaceholder.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("Placeholder building prefab written to " + path + ". It is not a surveyed Ayutthaya building.");
        }
    }
}
