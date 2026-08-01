using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 動物が「殺される」ときの共通処理。全ての動物(被食者・捕食者問わず)に付ける。
/// </summary>
public class AnimalHealth : MonoBehaviour
{
    [Tooltip("死亡アニメーションへ遷移させるTriggerパラメータ名")]
    public string dieTrigger = "Die";
    [Tooltip("死亡後、何秒でオブジェクトを非表示にするか(0以下なら非表示にしない)")]
    public float disableDelayAfterDeath = 5f;

    public bool IsDead { get; private set; } = false;

    private Animator animator;
    private NavMeshAgent agent;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// 捕食者から呼ばれる「殺される」処理。
    /// </summary>
    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        // 他の移動・AI系スクリプトを止める
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        var idleBehavior = GetComponent<AnimalIdleBehavior>();
        if (idleBehavior != null) idleBehavior.enabled = false;

        var fleeBehavior = GetComponent<FleeFromPredators>();
        if (fleeBehavior != null) fleeBehavior.enabled = false;

        var predatorAI = GetComponent<PredatorAI>();
        if (predatorAI != null) predatorAI.enabled = false;

        if (animator != null)
        {
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