using UnityEditor;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class ColliderGenerator
    {
        public static void AddToSelection()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("Select one or more mesh objects first.");
                return;
            }
            var added = 0;
            for (var i = 0; i < selected.Length; i++)
            {
                var filters = selected[i].GetComponentsInChildren<MeshFilter>(true);
                for (var f = 0; f < filters.Length; f++)
                {
                    if (filters[f].sharedMesh == null) continue;
                    if (filters[f].GetComponent<Collider>() != null) continue;
                    var collider = filters[f].gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filters[f].sharedMesh;
                    collider.convex = false;
                    filters[f].gameObject.isStatic = true;
                    added++;
                }
            }
            Debug.Log("Added " + added + " mesh collider(s). Non-convex colliders were marked static.");
        }
    }
}
