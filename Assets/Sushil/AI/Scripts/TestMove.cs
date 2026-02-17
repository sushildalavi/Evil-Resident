using UnityEngine;
using UnityEngine.AI;

public class TestMove : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform target;

    void Start()
    {
        agent.SetDestination(target.position);
    }
}