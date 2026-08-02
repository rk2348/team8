using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// 動物への憑依・解除を管理するスクリプト。
/// 距離判定によるAボタンでの憑依、Bボタンでの解除、視点・位置の同期、
/// HUD表示(ステータス・ミッション完了・スコア)、鳴き声、
/// 狩りアクション(トラ等)、憑依中のNPC AI停止、スコア算出までを担当する。
/// </summary>
public class AnimalViewSwitch : MonoBehaviour
{
    /// <summary>
    /// スコアの内訳を保持する構造体。内部計算とデバッグログ用に3要素と合計を保持する。
    /// HUDへは合計(totalScore)のみを渡す。
    /// </summary>
    public struct ScoreBreakdown
    {
        public int stealthScore;   // 憑依?逃走開始までの加点
        public int chaseScore;     // 逃走時間による減点(マイナス値)
        public int huntSpeedScore; // 接近?仕留めまでのスピード加点
        public int totalScore;     // 上記3つ + 基礎点の合計
    }

    [Header("動物の設定")]
    [Tooltip("動物のルート（大元）のオブジェクト")]
    public Transform animalRoot;
    [Tooltip("動物の視点となる場所（頭など）のTransform")]
    public Transform animalViewpoint;

    [Header("距離の設定")]
    [Tooltip("Aボタンが反応する距離（メートル）")]
    public float interactionDistance = 3.0f;

    [Header("プレイヤーの設定")]
    [Tooltip("プレイヤーのルートオブジェクト（VRMovementがついているオブジェクト）")]
    public Transform playerRig;
    [Tooltip("プレイヤーのCharacterController")]
    public CharacterController playerController;

    // 既に動物に乗り移っているかの判定
    private bool isPossessing = false;

    [Header("HUDの設定")]
    public PossessionController hudController;
    [Tooltip("現在の体力")]
    public float currentHealth = 100f;
    public float maxHealth = 100f;
    [Tooltip("現在の空腹度")]
    public float currentHunger = 100f;
    public float maxHunger = 100f;
    [Tooltip("現在の危険度(0?1)")]
    [Range(0f, 1f)] public float dangerLevel = 0f;

    [Header("鳴き声の設定")]
    [Tooltip("鳴き声を再生するAudioSource(動物のオブジェクトにアタッチしておく)")]
    public AudioSource cryAudioSource;
    [Tooltip("再生する鳴き声のAudioClip")]
    public AudioClip crySound;
    [Tooltip("連打防止のためのクールダウン時間(秒)")]
    public float cryCooldown = 1.0f;
    private float lastCryTime = -999f;

    [Header("狩りの設定(この動物が憑依中に狩りをできる場合のみ設定)")]
    [Tooltip("憑依中に狩りアクションを行える動物かどうか(トラならON)")]
    public bool canHunt = false;
    [Tooltip("狩りの対象となる種族")]
    public AnimalIdentity.AnimalSpecies huntTargetSpecies = AnimalIdentity.AnimalSpecies.Deer;
    [Tooltip("この距離内にいる対象を狩れる")]
    public float huntRange = 2.5f;
    [Tooltip("攻撃アニメーションのTrigger名")]
    public string attackTrigger = "Attack";
    [Tooltip("攻撃モーションが終わるまでの時間(秒)")]
    public float attackAnimDuration = 1.0f;
    [Tooltip("狩り成功(対象を倒した)後、ミッション完了パネルを表示するまでの待機時間(秒)")]
    public float missionCompletePanelDelay = 3.0f;
    private bool missionCompleted = false;

    [Header("スコアの設定")]
    [Tooltip("狩り成功で必ず加算される基礎スコア")]
    public int baseScoreValue = 50;
    [Tooltip("『憑依してから逃げ出すまでの時間』1秒あたりの加点(長く気づかれなかったほど高評価)")]
    public float scorePerSecondBeforeFlee = 10f;
    [Tooltip("この秒数までは気づかれボーナスなし(この秒数を超えた分だけ加点)")]
    public float stealthGraceTime = 2f;
    [Tooltip("『シカが逃げていた時間』1秒あたりの減点(追跡に時間がかかるほど低評価)")]
    public float penaltyPerSecondFleeing = 5f;
    [Tooltip("『近づいてから倒すまでの時間』の基準タイム(秒)。これより速いほど加点")]
    public float huntSpeedBenchmark = 3f;
    [Tooltip("基準タイムより1秒速いごとの加点")]
    public float scorePerSecondFasterHunt = 15f;

    private int currentScore = 0;
    private float possessTime = -1f;   // 憑依した時刻(「逃げ出すまでの時間」の起点)
    private float huntStartTime = -1f; // 狩り(接近して攻撃ボタンを押した)を開始した時刻

    [Header("憑依中に停止させるAIコンポーネント")]
    [Tooltip("この動物のAnimalIdleBehavior(徘徊AI)")]
    public AnimalIdleBehavior idleBehavior;
    [Tooltip("被食者の場合、この動物のFleeFromPredators")]
    public FleeFromPredators fleeBehavior;
    [Tooltip("捕食者(トラ・オオカミ)の場合、この動物のPredatorAI")]
    public PredatorAI predatorAI;
    [Tooltip("この動物のNavMeshAgent。憑依中は操作の競合を防ぐため無効化する")]
    public NavMeshAgent navAgent;

    private Animator animator;
    private AnimalIdentity selfIdentity;

    // 憑依解除時に元へ戻すための情報
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool wasRigidbodyKinematic;
    private Rigidbody animalRb;

    void Start()
    {
        if (animalRoot == null)
        {
            animalRoot = transform;
        }

        animator = GetComponentInChildren<Animator>();
        selfIdentity = GetComponent<AnimalIdentity>();

        if (idleBehavior == null) idleBehavior = GetComponent<AnimalIdleBehavior>();
        if (fleeBehavior == null) fleeBehavior = GetComponent<FleeFromPredators>();
        if (predatorAI == null) predatorAI = GetComponent<PredatorAI>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (playerRig == null) return;

        if (!isPossessing)
        {
            float distance = Vector3.Distance(animalRoot.position, playerRig.position);
            if (distance <= interactionDistance)
            {
                if (hudController != null)
                {
                    hudController.ShowPossessPrompt();
                }

                if (OVRInput.GetDown(OVRInput.RawButton.A))
                {
                    PossessAnimal();
                }
            }
            else
            {
                if (hudController != null)
                {
                    hudController.HideActionPanels();
                }
            }
        }
        else
        {
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                if (hudController != null)
                {
                    hudController.TogglePanels();
                }
            }

            if (OVRInput.GetDown(OVRInput.RawButton.X))
            {
                if (hudController != null)
                {
                    hudController.ToggleMissionView();
                }
            }

            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                ReleaseAnimal();
            }

            if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
            {
                PerformSpecialAction();
            }
        }
    }

    private void PossessAnimal()
    {
        isPossessing = true;
        missionCompleted = false;
        possessTime = Time.time;
        huntStartTime = -1f;

        originalParent = animalRoot.parent;
        originalPosition = animalRoot.position;
        originalRotation = animalRoot.rotation;

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        playerRig.position = animalViewpoint.position;
        playerRig.rotation = animalViewpoint.rotation;

        animalRoot.SetParent(playerRig, true);

        animalRb = animalRoot.GetComponent<Rigidbody>();
        if (animalRb != null)
        {
            wasRigidbodyKinematic = animalRb.isKinematic;
            animalRb.isKinematic = true;
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        if (idleBehavior != null) idleBehavior.enabled = false;
        if (fleeBehavior != null) fleeBehavior.enabled = false;
        if (predatorAI != null) predatorAI.enabled = false;
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }

        if (hudController != null)
        {
            hudController.ShowHUD(currentHealth, maxHealth, currentHunger, maxHunger, dangerLevel);
        }

        Debug.Log("動物に乗り移り、実態と同期しました！");
    }

    private void ReleaseAnimal()
    {
        isPossessing = false;

        animalRoot.SetParent(originalParent, true);
        animalRoot.position = originalPosition;
        animalRoot.rotation = originalRotation;

        if (animalRb != null)
        {
            animalRb.isKinematic = wasRigidbodyKinematic;
        }

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (!navAgent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(animalRoot.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    navAgent.Warp(hit.position);
                }
            }
            navAgent.isStopped = false;
        }
        if (idleBehavior != null) idleBehavior.enabled = true;
        if (fleeBehavior != null) fleeBehavior.enabled = true;
        if (predatorAI != null) predatorAI.enabled = true;

        if (hudController != null)
        {
            hudController.HideHUD();
        }

        Debug.Log("憑依を解除し、動物を元の位置に戻しました！");
    }

    private void PerformSpecialAction()
    {
        if (canHunt && !missionCompleted)
        {
            AnimalIdentity target = AnimalIdentity.FindNearest(huntTargetSpecies, animalRoot.position, selfIdentity);
            if (target != null)
            {
                float distance = Vector3.Distance(animalRoot.position, target.transform.position);
                if (distance <= huntRange)
                {
                    huntStartTime = Time.time;
                    StartCoroutine(HuntSequence(target));
                    return;
                }
            }
        }

        PlayCry();
    }

    private IEnumerator HuntSequence(AnimalIdentity target)
    {
        Vector3 direction = target.transform.position - animalRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            animalRoot.rotation = Quaternion.LookRotation(direction);
        }

        if (animator != null)
        {
            animator.SetTrigger(attackTrigger);
        }

        yield return new WaitForSeconds(attackAnimDuration);

        var targetHealth = target.GetComponent<AnimalHealth>();
        var targetFlee = target.GetComponent<FleeFromPredators>();

        if (targetHealth != null)
        {
            targetHealth.Kill();
        }

        missionCompleted = true;
        var breakdown = CalculateScore(targetFlee);
        currentScore = breakdown.totalScore;

        Debug.Log($"狩りに成功しました。スコア: {breakdown.totalScore} (内訳: 潜伏{breakdown.stealthScore} / 追跡{breakdown.chaseScore} / 速度{breakdown.huntSpeedScore})");

        // 倒した瞬間ではなく、少し間を置いてからミッション完了パネルを表示する
        // (仕留めた直後の余韻・演出を見せてからパネルを出すため)
        yield return new WaitForSeconds(missionCompletePanelDelay);

        if (hudController != null)
        {
            hudController.ShowMissionComplete(breakdown.totalScore);
        }
    }

    /// <summary>
    /// 「憑依→逃走開始までの時間」「逃走していた時間」「接近→仕留めるまでの時間」の
    /// 3要素から最終スコアを算出する。内訳はデバッグログ用に保持し、HUDへは合計のみ渡す。
    /// </summary>
    private ScoreBreakdown CalculateScore(FleeFromPredators targetFlee)
    {
        var breakdown = new ScoreBreakdown();

        // 1. 憑依してから、シカが逃げ出すまでの時間(長いほど加点=気づかれず接近できた)
        if (targetFlee != null && targetFlee.FleeStartTime >= 0f && possessTime >= 0f)
        {
            float timeBeforeFlee = Mathf.Max(0f, targetFlee.FleeStartTime - possessTime);
            if (timeBeforeFlee > stealthGraceTime)
            {
                breakdown.stealthScore = Mathf.RoundToInt((timeBeforeFlee - stealthGraceTime) * scorePerSecondBeforeFlee);
            }
        }

        // 2. シカが逃げていた時間(長いほど減点=追跡に手間取った)
        float fleeDuration = targetFlee != null ? targetFlee.LastFleeDuration : 0f;
        breakdown.chaseScore = -Mathf.RoundToInt(fleeDuration * penaltyPerSecondFleeing);

        // 3. 近づいてから倒すまでの時間(短いほど加点=一瞬で仕留めた)
        float huntDuration = huntStartTime >= 0f ? (Time.time - huntStartTime) : huntSpeedBenchmark;
        float diff = huntSpeedBenchmark - huntDuration;
        breakdown.huntSpeedScore = diff > 0f ? Mathf.RoundToInt(diff * scorePerSecondFasterHunt) : 0;

        breakdown.totalScore = Mathf.Max(0, baseScoreValue + breakdown.stealthScore + breakdown.chaseScore + breakdown.huntSpeedScore);

        return breakdown;
    }

    private void PlayCry()
    {
        if (Time.time - lastCryTime < cryCooldown) return;
        lastCryTime = Time.time;

        if (cryAudioSource != null && crySound != null)
        {
            cryAudioSource.PlayOneShot(crySound);
            Debug.Log("鳴き声を再生しました。");
        }
        else
        {
            Debug.LogWarning("AnimalViewSwitch: cryAudioSourceまたはcrySoundが設定されていません。");
        }
    }
}