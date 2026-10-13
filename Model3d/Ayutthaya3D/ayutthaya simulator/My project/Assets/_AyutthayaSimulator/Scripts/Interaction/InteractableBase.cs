using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        public string Id = "";
        public string PromptKey = "interact";

        public string InteractId => string.IsNullOrEmpty(Id) ? name : Id;

        public virtual string GetPrompt(string language) => LocalizationCatalog.Get(language, PromptKey);

        public abstract void Interact(PlayerController player);
    }
}
