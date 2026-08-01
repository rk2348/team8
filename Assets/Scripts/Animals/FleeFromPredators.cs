using System.Collections.Generic;
using UnityEditorInternal.VR;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// シーン内の"Predator"コンポーネントを持つ全ての捕食者を警戒し、
/// 一定距離以内に近づくと最も近い捕食者から逃げる汎用スクリプト。
/// シカ、ウサギ、シマウマなど、被食者となる動物すべてに使い回せる。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class FleeFromPredators : MonoBehaviour
{
    [Header("逃走の設定")]
    [Tooltip("この距離より近づくと逃げ始める(メートル)")]
    public float detectionRadius = 8f;
    [Tooltip("逃げる際の移動距離(捕食者と反対方向にどれだけ走るか)")]
    public float fleeDistance = 12f;
    [Tooltip("通常時の移動速度")]
    public float normalSpeed = 2f;
    [Tooltip("逃走時の移動速度")]
    public float fleeSpeed = 6f;
    [Tooltip("逃走中に再度目的地を再計算する間隔(秒)")]
    public float fleeUpdateInterval = 0.5f;
    [Tooltip("捕食者の再探索を行う間隔(秒)。重い処理なので毎フレームは避ける")]
    public float predatorSearchInterval = 1.0f;

    [Header("アニメーションの設定")]
    [Tooltip("この動物のAnimatorコンポーネント")]
    public Animator animator;
    [Tooltip("Animator Controller内の、逃走中を示すBoolパラメータ名")]
    public string isRunningParam = "IsRunning";

    [Header("状態確認用(読み取り専用)")]
    [SerializeField] private bool isFleeing = false;
    [SerializeField] private Predator currentThreat;

    // 外部(AnimalIdleBehaviorやPredatorAIなど)から現在逃走中かを確認するためのプロパティ
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
                    animator.SetBool(isRunningParam, true);
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
                animator.SetBool(isRunningParam, false);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}