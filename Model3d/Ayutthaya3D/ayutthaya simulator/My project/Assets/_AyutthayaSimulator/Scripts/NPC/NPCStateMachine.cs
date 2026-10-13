namespace AyutthayaSimulator
{
    public enum NpcMode
    {
        Idle,
        Walk,
        Talk
    }

    public sealed class NPCStateMachine
    {
        public NpcMode Mode = NpcMode.Idle;
        public float StateTime;

        public void Tick(float delta, bool hasPath, bool talking)
        {
            StateTime += delta;
            if (talking)
            {
                Mode = NpcMode.Talk;
                return;
            }
            if (hasPath && StateTime > 1.5f) Mode = NpcMode.Walk;
            else if (!hasPath) Mode = NpcMode.Idle;
        }
    }
}
