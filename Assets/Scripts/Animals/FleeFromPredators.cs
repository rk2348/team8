using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// シーン内の"Predator"コンポーネントを持つ全ての捕食者を警戒し、
/// 一定距離以内に近づくと最も近い捕食者から逃げる汎用スクリプト。
/// 牛・馬・シマウマ・シカ・ウサギ・ゾウなど、被食者となる動物すべてに使い回せる。
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
    [Tooltip("逃走開始時に発火するTrigger名")]
    public string runTrigger = "Run";
    [Tooltip("逃走終了時に発火するTrigger名(通常状態へ戻す)")]
    public string idleTrigger = "Idle";

    [Header("状態確認用(読み取り専用)")]
    [SerializeField] private bool isFleeing = false;
    [SerializeField] private Predator currentThreat;

    // 外部(AnimalIdleBehaviorやPredatorAI、AnimalViewSwitchなど)から参照するための公開プロパティ
    public bool IsFleeing => isFleeing;

    /// <summary>
    /// 現在(または直近)の逃走が開始された時刻(Time.time)。逃走したことが一度も無ければ-1。
    /// スコア計算(憑依?逃走開始までの経過時間の算出)に使用する。
    /// </summary>
    public float FleeStartTime { get; private set; } = -1f;

    /// <summary>
    /// 直近の逃走にかかった時間(秒)。逃走が終わる(または死亡する)たびに確定する。
    /// </summary>
    public float LastFleeDuration { get; private set; } = 0f;

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
                FleeStartTime = Time.time; // 逃走開始時刻を記録
                agent.speed = fleeSpeed;

                if (animator != null)
                {
                    animator.SetTrigger(runTrigger);
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
            ConfirmFleeDuration();

            agent.speed = normalSpeed;
            agent.ResetPath();

            if (animator != null)
            {
                animator.SetTrigger(idleTrigger);
            }
        }
    }

    /// <summary>
    /// 逃走中に記録していた開始時刻から、経過時間をLastFleeDurationとして確定させる。
    /// </summary>
    private void ConfirmFleeDuration()
    {
        if (FleeStartTime >= 0f)
        {
            LastFleeDuration = Time.time - FleeStartTime;
        }
    }

    /// <summary>
    /// 捕食されて死亡するときに、AnimalHealth.Kill()から呼び出す。
    /// 移動のみを止め、逃走時間を確定させる。
    /// </summary>
    public void StopForDeath()
    {
        if (isFleeing)
        {
            ConfirmFleeDuration();
        }

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