using AyutthayaSimulator.Core;
using UnityEditor;
using UnityEngine;

namespace AyutthayaSimulator.EditorTools
{
    public static class ComponentAutoBinder
    {
        public static void BindSelection(bool forceKind, ModelKind forced = ModelKind.Unknown)
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("Select an imported model root first.");
                return;
            }
            var filters = selected.GetComponentsInChildren<MeshFilter>(true);
            var facts = new ModelNodeFacts
            {
                Name = selected.name,
                HasMesh = filters.Length > 0 && filters[0].sharedMesh != null,
                HasRendererBounds = selected.GetComponentInChildren<Renderer>() != null,
                ManualOverride = forceKind,
                OverrideKind = forced,
                ChildCount = selected.transform.childCount,
                ScaleIsRoughlyMetric = selected.transform.lossyScale.magnitude < 20f
            };
            var decision = ModelBindingRules.Classify(facts);
            if (decision.Decision == BindingDecision.Reject)
            {
                Debug.LogWarning("Not bound: " + decision.Reason);
                return;
            }
            if (decision.Decision == BindingDecision.CandidateNeedsConfirmation && !forceKind)
            {
                Debug.LogWarning("Candidate only: " + decision.Reason);
                return;
            }
            switch (decision.Kind)
            {
                case ModelKind.Building:
                    if (selected.GetComponent<BuildingController>() == null) selected.AddComponent<BuildingController>();
                    if (selected.GetComponent<BuildingMetadata>() == null) selected.AddComponent<BuildingMetadata>();
                    if (selected.GetComponent<BuildingInteriorLoader>() == null) selected.AddComponent<BuildingInteriorLoader>();
                    break;
                case ModelKind.Door:
                    if (selected.GetComponent<DoorController>() == null) selected.AddComponent<DoorController>();
                    break;
                case ModelKind.Vehicle:
                    if (selected.GetComponent<VehicleController>() == null) selected.AddComponent<VehicleController>();
                    break;
                case ModelKind.Npc:
                    if (selected.GetComponent<NPCController>() == null) selected.AddComponent<NPCController>();
                    if (selected.GetComponent<NPCDialogue>() == null) selected.AddComponent<NPCDialogue>();
                    break;
            }
            EditorUtility.SetDirty(selected);
            Debug.Log("Bound " + decision.Kind + " to " + selected.name + ". " + decision.Reason);
        }
    }
}
