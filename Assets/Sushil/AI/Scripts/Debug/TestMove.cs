using UnityEngine;
using UnityEngine.AI;

public class TestMove : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform target;

    void Start()
    {
        if (agent == null || target == null) return;
        if (!agent.enabled || !agent.gameObject.activeInHierarchy || !agent.isOnNavMesh) return;
        agent.SetDestination(target.position);
    }
}
