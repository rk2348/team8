using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PredatorAI : MonoBehaviour
{
    public enum ReactionMode { Ignore, Engage }
    private enum PredatorState { None, Watching, Stalking, Chasing }

    [System.Serializable]
    public class PreyReaction
    {
        public AnimalIdentity.AnimalSpecies species;
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
        public AnimalIdentity.AnimalSpecies species;
        [Tooltip("この距離に入るとケンカ(威嚇・攻撃)を始める")]
        public float engageDistance = 5f;
        [Tooltip("攻撃モーションを再生する間隔(秒)")]
        public float attackInterval = 1.2f;
        [Tooltip("何秒間ケンカを続けたら離れるか")]
        public float fightDuration = 5f;
        [Tooltip("離れる際に確保する距離(メートル)")]
        public float retreatDistance = 10f;
        [Tooltip("離れた後、再度この相手とケンカするまでのクールダウン時間(秒)")]
        public float cooldownAfterFight = 12f;
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
    public string watchTrigger = "Idle";
    public string stalkTrigger = "Walk";
    public string chaseTrigger = "Run";
    public string attackTrigger = "Attack";
    public string eatTrigger = "Eat";

    [Header("捕食にかかる演出時間(秒)")]
    public float attackAnimDuration = 1.0f;
    public float eatAnimDuration = 3.0f;

    [Header("離脱(ケンカ後に離れる)時の移動速度")]
    public float wanderSpeedFallback = 1.4f;

    [Header("索敵の間隔(秒)")]
    public float scanInterval = 0.3f;

    private NavMeshAgent agent;
    private AnimalIdentity selfIdentity;
    private float scanTimer = 0f;
    private bool isBusy = false;

    // ケンカ後、しばらく同じ相手を無視するためのクールダウン管理
    private float rivalCooldownUntil = 0f;

    // 現在の狩り関連の行動状態(Watch/Stalk/Chase)。同じ状態の間はTriggerを再発火させないためのフラグ管理
    private PredatorState currentState = PredatorState.None;

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

        // agentがNavMesh上に乗っていない場合は何もしない(憑依中やスポーン直後などで発生しうる)
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;

        // 1. まずライバル(同格の捕食者)がいないか確認。見つかればケンカを優先。
        if (TryFindRival(out AnimalIdentity rival, out RivalReaction rivalRule))
        {
            currentState = PredatorState.None;
            StartCoroutine(FightSequence(rival, rivalRule));
            return;
        }

        // 2. 狩りの対象を探す
        if (TryFindPrey(out AnimalIdentity prey, out PreyReaction rule, out float distance))
        {
            idleBehavior?.Pause();

            if (distance <= rule.catchDistance)
            {
                currentState = PredatorState.None;
                StartCoroutine(KillSequence(prey));
            }
            else if (distance <= rule.chaseDistance)
            {
                Chase(prey.transform, rule.chaseSpeed);
            }
            else
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
            currentState = PredatorState.None;
        }
    }

    // ---- ターゲット探索 ----

    private bool TryFindPrey(out AnimalIdentity foundPrey, out PreyReaction foundRule, out float foundDistance)
    {
        foundPrey = null;
        foundRule = null;
        foundDistance = float.MaxValue;

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

        // クールダウン中はケンカ相手を探さない(直前にケンカを終えたばかりの場合)
        if (Time.time < rivalCooldownUntil) return false;

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

    // ---- 各状態の処理(状態が変化したときだけTriggerを発火する) ----

    private void Watch(Transform target)
    {
        agent.isStopped = true;
        FaceTarget(target);

        if (currentState != PredatorState.Watching)
        {
            PlayAnim(watchTrigger);
            currentState = PredatorState.Watching;
        }
    }

    private void Stalk(Transform target, float speed)
    {
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(target.position);

        if (currentState != PredatorState.Stalking)
        {
            PlayAnim(stalkTrigger);
            currentState = PredatorState.Stalking;
        }
    }

    private void Chase(Transform target, float speed)
    {
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(target.position);

        if (currentState != PredatorState.Chasing)
        {
            PlayAnim(chaseTrigger);
            currentState = PredatorState.Chasing;
        }
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

    // ---- ケンカシーケンス(一定時間で離脱する) ----

    private IEnumerator FightSequence(AnimalIdentity rival, RivalReaction rule)
    {
        isBusy = true;
        idleBehavior?.Pause();
        agent.isStopped = true;

        float elapsed = 0f;

        // 一定時間(fightDuration)が経過するか、相手が離れる/いなくなるまでケンカを続ける
        while (rival != null
               && elapsed < rule.fightDuration
               && Vector3.Distance(transform.position, rival.transform.position) <= rule.engageDistance)
        {
            FaceTarget(rival.transform);
            PlayAnim(attackTrigger);

            yield return new WaitForSeconds(rule.attackInterval);
            elapsed += rule.attackInterval;
        }

        // 時間切れ、または相手が離れた場合、こちらも相手と反対方向へ離れる
        if (rival != null)
        {
            RetreatFrom(rival.transform, rule.retreatDistance);
            yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= 0.5f);
        }

        // しばらくの間、同じ相手とは再びケンカしないようにする
        rivalCooldownUntil = Time.time + rule.cooldownAfterFight;

        isBusy = false;
        idleBehavior?.Resume();
    }

    private void RetreatFrom(Transform rival, float distance)
    {
        Vector3 retreatDirection = (transform.position - rival.position).normalized;
        Vector3 retreatTargetPosition = transform.position + retreatDirection * distance;

        agent.isStopped = false;
        agent.speed = wanderSpeedFallback;
        PlayAnim(stalkTrigger); // 離脱時はstalkTrigger("Walk")を流用

        if (NavMesh.SamplePosition(retreatTargetPosition, out NavMeshHit hit, distance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else if (NavMesh.SamplePosition(retreatTargetPosition, out NavMeshHit widerHit, distance * 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(widerHit.position);
        }
    }
}