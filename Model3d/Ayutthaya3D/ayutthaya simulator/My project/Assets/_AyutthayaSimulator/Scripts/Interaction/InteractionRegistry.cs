using System.Collections.Generic;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class InteractionRegistry : MonoBehaviour
    {
        readonly Dictionary<string, IInteractable> byId = new Dictionary<string, IInteractable>();

        public void Register(IInteractable interactable)
        {
            if (interactable == null || string.IsNullOrEmpty(interactable.InteractId)) return;
            byId[interactable.InteractId] = interactable;
        }

        public bool TryGet(string id, out IInteractable interactable) => byId.TryGetValue(id, out interactable);

        public static InteractionRegistry FindOrCreate()
        {
            var existing = FindFirstObjectByType<InteractionRegistry>();
            if (existing != null) return existing;
            var go = new GameObject("InteractionRegistry");
            return go.AddComponent<InteractionRegistry>();
        }
    }
}
