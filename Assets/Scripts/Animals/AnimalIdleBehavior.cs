using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 動物の通常時の行動(待機・歩く・食べる)をランダムに繰り返す汎用スクリプト。
/// 逃走中(FleeFromPredators作動中)や、PredatorAIによる狩り・ケンカ中は自動的に一時停止する。
/// 群れを作る動物の場合、HerdBehaviorが設定されていれば徘徊先に群れの結合・分離を加味する。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AnimalIdleBehavior : MonoBehaviour
{
    private enum ActionType { Idle, Walk, Eat }

    [Header("アニメーションの設定")]
    [Tooltip("この動物のAnimatorコンポーネント")]
    public Animator animator;
    [Tooltip("Idleステートへ遷移させるTriggerパラメータ名")]
    public string idleTrigger = "Idle";
    [Tooltip("Walkステートへ遷移させるTriggerパラメータ名")]
    public string walkTrigger = "Walk";
    [Tooltip("Eatステートへ遷移させるTriggerパラメータ名")]
    public string eatTrigger = "Eat";

    [Header("行動時間の設定(秒)")]
    public Vector2 idleDurationRange = new Vector2(2f, 5f);
    public Vector2 eatDurationRange = new Vector2(3f, 6f);

    [Header("歩行の設定")]
    [Tooltip("徘徊時の移動速度")]
    public float wanderSpeed = 1.2f;
    [Tooltip("現在地からどれくらいの範囲をランダムに歩き回るか(メートル)")]
    public float wanderRadius = 10f;
    [Tooltip("目的地への到達判定距離")]
    public float arrivalThreshold = 0.5f;

    [Header("行動の選ばれやすさ(重み。大きいほど選ばれやすい)")]
    public float idleWeight = 1f;
    public float walkWeight = 2f;
    public float eatWeight = 1f;

    [Header("外部連携(任意)")]
    [Tooltip("この動物のFleeFromPredators。設定すると逃走中は自動で徘徊を止める")]
    public FleeFromPredators fleeBehavior;
    [Tooltip("群れを作る動物の場合、同じGameObjectのHerdBehaviorを設定する(未設定なら通常のランダム徘徊)")]
    public HerdBehavior herdBehavior;

    private NavMeshAgent agent;
    private ActionType currentAction;
    private float actionTimer;
    private bool isPaused = false;
    private bool isWaitingForArrival = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (herdBehavior == null)
        {
            herdBehavior = GetComponent<HerdBehavior>();
        }
        agent.stoppingDistance = 0f; // 到着判定はスクリプト側のarrivalThresholdで一元管理する
    }

    void Start()
    {
        StartAction(ActionType.Idle, RandomRange(idleDurationRange));
    }

    void Update()
    {
        // 逃走中は徘徊AIを完全に止める(FleeFromPredators側の移動命令を優先させる)
        if (fleeBehavior != null && fleeBehavior.IsFleeing)
        {
            isPaused = true;
            return;
        }

        // PredatorAI等、外部からPause()されている間はここで待機
        if (isPaused)
        {
            return;
        }

        if (currentAction == ActionType.Walk)
        {
            if (isWaitingForArrival && !agent.pathPending && agent.remainingDistance <= arrivalThreshold)
            {
                isWaitingForArrival = false;
                PickNextAction();
            }
            return;
        }

        actionTimer -= Time.deltaTime;
        if (actionTimer <= 0f)
        {
            PickNextAction();
        }
    }

    /// <summary>
    /// 外部から徘徊AIを一時停止させたいとき(憑依開始時、狩り・ケンカ中など)に呼ぶ。
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
        }
    }

    /// <summary>
    /// 外部から徘徊AIを再開させたいとき(憑依解除時、狩り・ケンカ終了時など)に呼ぶ。
    /// </summary>
    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        StartAction(ActionType.Idle, RandomRange(idleDurationRange));
    }

    private void PickNextAction()
    {
        float total = idleWeight + walkWeight + eatWeight;
        float rand = Random.Range(0f, total);

        if (rand < idleWeight)
        {
            StartAction(ActionType.Idle, RandomRange(idleDurationRange));
        }
        else if (rand < idleWeight + walkWeight)
        {
            StartWalk();
        }
        else
        {
            StartAction(ActionType.Eat, RandomRange(eatDurationRange));
        }
    }

    private void StartAction(ActionType action, float duration)
    {
        currentAction = action;
        actionTimer = duration;

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        switch (action)
        {
            case ActionType.Idle:
                PlayAnimation(idleTrigger);
                break;
            case ActionType.Eat:
                PlayAnimation(eatTrigger);
                break;
        }
    }

    private void StartWalk()
    {
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;

        // 群れを作る動物の場合、ランダムな徘徊先に群れの結合・分離を加味する
        if (herdBehavior != null)
        {
            randomPoint = herdBehavior.GetHerdAdjustedDestination(randomPoint);
        }

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            currentAction = ActionType.Walk;
            isWaitingForArrival = true;

            agent.isStopped = false;
            agent.speed = wanderSpeed;
            agent.SetDestination(hit.position);

            PlayAnimation(walkTrigger);
        }
        else
        {
            StartAction(ActionType.Idle, RandomRange(idleDurationRange));
        }
    }

    private void PlayAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    private float RandomRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }
}