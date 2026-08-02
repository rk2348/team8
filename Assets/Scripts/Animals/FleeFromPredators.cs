using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class FleeFromPredators : MonoBehaviour
{
    [Header("逃走の設定")]
    public float detectionRadius = 8f;
    public float fleeDistance = 12f;
    public float normalSpeed = 2f;
    public float fleeSpeed = 6f;
    public float fleeUpdateInterval = 0.5f;
    public float predatorSearchInterval = 1.0f;

    [Header("アニメーションの設定")]
    public Animator animator;
    [Tooltip("逃走開始時に発火するTrigger名")]
    public string runTrigger = "Run";
    [Tooltip("逃走終了時に発火するTrigger名(通常状態へ戻す)")]
    public string idleTrigger = "Idle";

    [Header("状態確認用(読み取り専用)")]
    [SerializeField] private bool isFleeing = false;
    [SerializeField] private Predator currentThreat;

    public bool IsFleeing => isFleeing;

    private NavMeshAgent agent;
    private float fleeTimer = 0f;
    private float searchTimer = 0f;
    private static readonly List<Predator> predatorCache = new List<Predator>();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = normalSpeed;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            currentThreat = FindNearestPredator();
            searchTimer = predatorSearchInterval;
        }

        if (currentThreat == null)
        {
            StopFleeing();
            return;
        }

        float distance = Vector3.Distance(transform.position, currentThreat.transform.position);

        if (distance <= detectionRadius)
        {
            if (!isFleeing)
            {
                isFleeing = true;
                agent.speed = fleeSpeed;

                if (animator != null)
                {
                    animator.SetTrigger(runTrigger); // Boolではなく、開始した瞬間だけTriggerを発火
                }
            }

            fleeTimer -= Time.deltaTime;
            if (fleeTimer <= 0f)
            {
                FleeFrom(currentThreat.transform);
                fleeTimer = fleeUpdateInterval;
            }
        }
        else
        {
            StopFleeing();
        }
    }

    private Predator FindNearestPredator()
    {
        predatorCache.Clear();
        predatorCache.AddRange(FindObjectsOfType<Predator>());

        Predator nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var predator in predatorCache)
        {
            float dist = Vector3.Distance(transform.position, predator.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = predator;
            }
        }

        return nearest;
    }

    private void FleeFrom(Transform threat)
    {
        Vector3 fleeDirection = (transform.position - threat.position).normalized;
        Vector3 fleeTargetPosition = transform.position + fleeDirection * fleeDistance;

        if (NavMesh.SamplePosition(fleeTargetPosition, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else if (NavMesh.SamplePosition(fleeTargetPosition, out NavMeshHit widerHit, fleeDistance * 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(widerHit.position);
        }
    }

    private void StopFleeing()
    {
        if (isFleeing)
        {
            isFleeing = false;
            agent.speed = normalSpeed;
            agent.ResetPath();

            if (animator != null)
            {
                animator.SetTrigger(idleTrigger); // Boolではなく、終了した瞬間だけTriggerを発火
            }
        }
    }

    /// <summary>
    /// 捕食されて死亡するときに、AnimalHealth.Kill()から呼び出す。
    /// 移動のみを止める。AnimatorはTrigger方式になったため、
    /// 死亡時にRun関連のパラメータをリセットする必要はない。
    /// </summary>
    public void StopForDeath()
    {
        isFleeing = false;
        currentThreat = null;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}