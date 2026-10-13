using UnityEngine;
using UnityEngine.AI;

namespace AyutthayaSimulator
{
    public sealed class NPCController : MonoBehaviour
    {
        public NPCSchedule Schedule;
        public NPCDialogue Dialogue;
        public NPCAnimation AnimationDriver;
        public Transform Visual;
        public Vector3 PointA;
        public Vector3 PointB;
        public float WalkSpeed = 1.3f;
        public string NavigationStatus = "not_tested";

        NPCStateMachine brain = new NPCStateMachine();
        NavMeshAgent agent;
        Vector3 target;
        bool useAgent;
        DayNightCycle cycle;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            cycle = FindFirstObjectByType<DayNightCycle>();
            target = PointB;
            useAgent = agent != null && NavMesh.SamplePosition(transform.position, out _, 2f, NavMesh.AllAreas);
            NavigationStatus = useAgent ? "navmesh" : "fallback_straight_walk_no_navmesh";
            if (agent != null) agent.enabled = useAgent;
        }

        void Update()
        {
            var talking = Dialogue != null && Dialogue.Talking;
            var resting = Schedule != null && cycle != null && !Schedule.ShouldBeOutside(cycle.Hour);
            if (useAgent && agent != null) agent.isStopped = talking || resting;
            brain.Tick(Time.deltaTime, !resting, talking);
            if (resting && !talking) brain.Mode = NpcMode.Idle;
            if (AnimationDriver != null) AnimationDriver.Tick(brain.Mode);
            if (talking || resting) return;
            if (useAgent && agent != null)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.4f) target = target == PointA ? PointB : PointA;
                agent.SetDestination(target);
                return;
            }
            var step = WalkSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, step);
            if (Vector3.Distance(transform.position, target) < 0.3f) target = target == PointA ? PointB : PointA;
            var look = target - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 4f);
        }
    }
}
