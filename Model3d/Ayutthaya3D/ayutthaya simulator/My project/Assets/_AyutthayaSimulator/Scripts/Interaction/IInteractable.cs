namespace AyutthayaSimulator
{
    public interface IInteractable
    {
        string InteractId { get; }
        string GetPrompt(string language);
        void Interact(PlayerController player);
    }
}
