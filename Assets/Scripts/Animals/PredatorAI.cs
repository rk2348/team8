using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static AnimalIdentity;

/// <summary>
/// 捕食者(トラ・オオカミなど)の汎用AI。
/// どの種族を「無視する/様子を見て追跡する/ゆっくり接近してから追跡する」かはInspectorで設定する。
/// 同種の捕食者同士(トラ×オオカミなど)がケンカする設定も可能。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PredatorAI : MonoBehaviour
{
    public enum ReactionMode { Ignore, Engage }

    [System.Serializable]
    public class PreyReaction
    {
        public AnimalSpecies species;
        public ReactionMode mode = ReactionMode.Engage;

        [Tooltip("この距離に入ると気づく(様子を見る/ゆっくり近づき始める)距離")]
        public float noticeDistance = 15f;
        [Tooltip("この距離まで近づくと本気で追いかける距離")]
        public float chaseDistance = 8f;
        [Tooltip("この距離まで追いつくと仕留める距離")]
        public float catchDistance = 1.5f;
        [Tooltip("気づいた直後の接近速度。0なら『その場で見るだけ』、0より大きいと『ゆっくり近づく(忍び寄り)』になる")]
        public float approachSpeed = 0f;
        [Tooltip("本気の追跡速度")]
        public float chaseSpeed = 7f;
    }

    [System.Serializable]
    public class RivalReaction
    {
        public AnimalSpecies species;
        [Tooltip("この距離に入るとケンカ(威嚇・攻撃)を始める")]
        public float engageDistance = 5f;
        [Tooltip("攻撃モーションを再生する間隔(秒)")]
        public float attackInterval = 1.2f;
    }

    [Header("狩りの対象設定(この動物にとっての各種族への反応)")]
    public List<PreyReaction> preyReactions = new List<PreyReaction>();

    [Header("ケンカする相手の設定(同格の捕食者など)")]
    public List<RivalReaction> rivalReactions = new List<RivalReaction>();

    [Header("参照")]
    public Animator animator;
    [Tooltip("この動物の徘徊AI。狩り・ケンカ中は自動で一時停止させる")]
    public AnimalIdleBehavior idleBehavior;

    [Header("アニメーションのパラメータ名")]
    public string watchTrigger = "Idle";   // その場で見るだけの状態(既存のIdleを流用してもよい)
    public string stalkTrigger = "Walk";   // ゆっくり接近(既存のWalkを流用してもよい)
    public string chaseTrigger = "Run";
    public string attackTrigger = "Attack";
    public string eatTrigger = "Eat";

    [Header("捕食にかかる演出時間(秒)")]
    public float attackAnimDuration = 1.0f;
    public float eatAnimDuration = 3.0f;

    [Header("索敵の間隔(秒)")]
    public float scanInterval = 0.3f;

    private NavMeshAgent agent;
    private AnimalIdentity selfIdentity;
    private float scanTimer = 0f;
    private bool isBusy = false; // 攻撃演出・ケンカ演出中はUpdateの通常判定を止める

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        selfIdentity = GetComponent<AnimalIdentity>();

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (idleBehavior == null) idleBehavior = GetComponent<AnimalIdleBehavior>();
    }

    void Update()
    {
        if (isBusy) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;

        // 1. まずライバル(同格の捕食者)がいないか確認。見つかればケンカを優先。
        if (TryFindRival(out AnimalIdentity rival, out RivalReaction rivalRule))
        {
            StartCoroutine(FightSequence(rival, rivalRule));
            return;
        }

        // 2. 狩りの対象を探す
        if (TryFindPrey(out AnimalIdentity prey, out PreyReaction rule, out float distance))
        {
            idleBehavior?.Pause();

            if (distance <= rule.catchDistance)
            {
                StartCoroutine(KillSequence(prey));
            }
            else if (distance <= rule.chaseDistance)
            {
                Chase(prey.transform, rule.chaseSpeed);
            }
            else // noticeDistance圏内
            {
                if (rule.approachSpeed > 0f)
                {
                    Stalk(prey.transform, rule.approachSpeed);
                }
                else
                {
                    Watch(prey.transform);
                }
            }
        }
        else
        {
            // 何もターゲットがいない → 通常の徘徊に戻す
            agent.isStopped = true;
            idleBehavior?.Resume();
        }
    }

    // ---- ターゲット探索 ----

    private bool TryFindPrey(out AnimalIdentity foundPrey, out PreyReaction foundRule, out float foundDistance)
    {
        foundPrey = null;
        foundRule = null;
        foundDistance = float.MaxValue;

        // 「本気で追跡できる状態にある獲物」を最優先し、その中で最も近いものを選ぶ
        AnimalIdentity bestChaseTarget = null;
        PreyReaction bestChaseRule = null;
        float bestChaseDist = float.MaxValue;

        AnimalIdentity bestNoticeTarget = null;
        PreyReaction bestNoticeRule = null;
        float bestNoticeDist = float.MaxValue;

        foreach (var rule in preyReactions)
        {
            if (rule.mode == ReactionMode.Ignore) continue;

            var target = AnimalIdentity.FindNearest(rule.species, transform.position, selfIdentity);
            if (target == null) continue;

            float dist = Vector3.Distance(transform.position, target.transform.position);

            if (dist <= rule.chaseDistance && dist < bestChaseDist)
            {
                bestChaseDist = dist;
                bestChaseTarget = target;
                bestChaseRule = rule;
            }
            else if (dist <= rule.noticeDistance && dist < bestNoticeDist)
            {
                bestNoticeDist = dist;
                bestNoticeTarget = target;
                bestNoticeRule = rule;
            }
        }

        // 追跡可能な獲物がいればそちらを優先、いなければ様子見中の獲物を対象にする
        if (bestChaseTarget != null)
        {
            foundPrey = bestChaseTarget;
            foundRule = bestChaseRule;
            foundDistance = bestChaseDist;
            return true;
        }
        if (bestNoticeTarget != null)
        {
            foundPrey = bestNoticeTarget;
            foundRule = bestNoticeRule;
            foundDistance = bestNoticeDist;
            return true;
        }
        return false;
    }

    private bool TryFindRival(out AnimalIdentity foundRival, out RivalReaction foundRule)
    {
        foundRival = null;
        foundRule = null;

        foreach (var rule in rivalReactions)
        {
            var rival = AnimalIdentity.FindNearest(rule.species, transform.position, selfIdentity);
            if (rival == null) continue;

            float dist = Vector3.Distance(transform.position, rival.transform.position);
            if (dist <= rule.engageDistance)
            {
                foundRival = rival;
                foundRule = rule;
                return true;
            }
        }
        return false;
    }

    // ---- 各状態の処理 ----

    private void Watch(Transform target)
    {
        agent.isStopped = true;
        FaceTarget(target);
        PlayAnim(watchTrigger);
    }

    private void Stalk(Transform target, float speed)
    {
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(target.position);
        PlayAnim(stalkTrigger);
    }

    private void Chase(Transform target, float speed)
    {
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(target.position);
        PlayAnim(chaseTrigger);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);
    }

    private void PlayAnim(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger))
        {
            animator.SetTrigger(trigger);
        }
    }

    // ---- 捕食シーケンス ----

    private IEnumerator KillSequence(AnimalIdentity prey)
    {
        isBusy = true;
        agent.isStopped = true;
        FaceTarget(prey.transform);
        PlayAnim(attackTrigger);

        yield return new WaitForSeconds(attackAnimDuration);

        // 攻撃モーションの後、獲物を死亡させる
        var preyHealth = prey.GetComponent<AnimalHealth>();
        if (preyHealth != null)
        {
            preyHealth.Kill();
        }

        // 捕食(食事)アニメーション
        PlayAnim(eatTrigger);
        yield return new WaitForSeconds(eatAnimDuration);

        isBusy = false;
        idleBehavior?.Resume();
    }

    // ---- ケンカシーケンス ----

    private IEnumerator FightSequence(AnimalIdentity rival, RivalReaction rule)
    {
        isBusy = true;
        idleBehavior?.Pause();
        agent.isStopped = true;

        while (rival != null && Vector3.Distance(transform.position, rival.transform.position) <= rule.engageDistance)
        {
            FaceTarget(rival.transform);
            PlayAnim(attackTrigger);
            yield return new WaitForSeconds(rule.attackInterval);
        }

        isBusy = false;
        idleBehavior?.Resume();
    }
}