using UnityEngine;
using UnityEngine.AI;

public class AnimalHealth : MonoBehaviour
{
    [Tooltip("死亡アニメーションへ遷移させるTriggerパラメータ名(保険として残す)")]
    public string dieTrigger = "Die";
    [Tooltip("Animator内の死亡StateName(Play()で直接指定するため、Base Layer内の名前と完全一致させる)")]
    public string dieStateName = "die";
    [Tooltip("死亡後、何秒でオブジェクトを非表示にするか(0以下なら非表示にしない)")]
    public float disableDelayAfterDeath = 5f;

    public bool IsDead { get; private set; } = false;

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
    }

    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        var idleBehavior = GetComponent<AnimalIdleBehavior>();
        if (idleBehavior != null) idleBehavior.enabled = false;

        var fleeBehavior = GetComponent<FleeFromPredators>();
        if (fleeBehavior != null)
        {
            fleeBehavior.StopForDeath();
            fleeBehavior.enabled = false;
        }

        var predatorAI = GetComponent<PredatorAI>();
        if (predatorAI != null) predatorAI.enabled = false;

        var herdBehavior = GetComponent<HerdBehavior>();
        if (herdBehavior != null) herdBehavior.enabled = false;

        if (animator != null)
        {
            // Play()はブレンドや遷移条件を一切介さず、指定したStateへ「次のフレーム」ではなく
            // 「このフレーム内」で強制的に切り替える。他のTriggerとの競合や遅延を受けない。
            animator.Play(dieStateName, 0, 0f);

            // 念のためTriggerも立てておく(dieStateNameの綴りミス等でPlay()が失敗した場合の保険)
            animator.SetTrigger(dieTrigger);
        }

        if (disableDelayAfterDeath > 0f)
        {
            Invoke(nameof(DisableObject), disableDelayAfterDeath);
        }
    }

    private void DisableObject()
    {
        gameObject.SetActive(false);
    }
}