using UnityEditor;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class LODSetupTool
    {
        public static void SetupSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("Select a model root with at least one renderer.");
                return;
            }
            var renderers = selected.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("No renderer found. LOD was not created from the object name.");
                return;
            }
            var lod = selected.GetComponent<BuildingLODController>();
            if (lod == null) lod = selected.AddComponent<BuildingLODController>();
            lod.Configure(renderers[0], renderers.Length > 1 ? renderers[1] : null);
            EditorUtility.SetDirty(selected);
        }
    }
}
