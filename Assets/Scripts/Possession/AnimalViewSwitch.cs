using UnityEngine;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// 動物への憑依・解除を管理するスクリプト。
/// 距離判定によるAボタンでの憑依、Bボタンでの解除、視点・位置の同期、
/// HUD表示(体力・空腹・危険度)、鳴き声、狩りアクション(トラ等)、
/// 憑依中のNPC AI停止までを担当する。
/// </summary>
public class AnimalViewSwitch : MonoBehaviour
{
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
    private bool missionCompleted = false;

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
        // animalRootが未設定の場合、このスクリプトがついているオブジェクトを自動設定
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
            // 動物とプレイヤー間の距離を計算
            float distance = Vector3.Distance(animalRoot.position, playerRig.position);
            if (distance <= interactionDistance)
            {
                // 接近時、「Aボタンで憑依する」等の案内を表示
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
                // 範囲外になったら案内を消す
                if (hudController != null)
                {
                    hudController.HideActionText();
                }
            }
        }
        else
        {
            // 憑依中にAボタンが押されたらHUDパネルの表示/非表示を切り替える
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                if (hudController != null)
                {
                    hudController.TogglePanels();
                }
            }

            // 憑依中にBボタンが押されたら解除
            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                ReleaseAnimal();
            }

            // 憑依中に右手人差し指トリガーが押されたら特定のアクション(狩り or 鳴き声)
            if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
            {
                PerformSpecialAction();
            }
        }
    }

    private void PossessAnimal()
    {
        isPossessing = true;
        missionCompleted = false; // 憑依のたびにミッション状態をリセット

        // 憑依前の状態を保存(解除時に元へ戻すため)
        originalParent = animalRoot.parent;
        originalPosition = animalRoot.position;
        originalRotation = animalRoot.rotation;

        // CharacterControllerが有効なままだと位置の直接変更がブロックされるため一時無効化
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // 1. プレイヤーを動物の視点の位置・向きに合わせる
        playerRig.position = animalViewpoint.position;
        playerRig.rotation = animalViewpoint.rotation;

        // 2. 動物のモデルをプレイヤーの子オブジェクトにする
        animalRoot.SetParent(playerRig, true);

        // 3. Rigidbody(物理演算)がついていて移動の邪魔になる場合は無効化する
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

        // 憑依中はNPC用のAIを全て止める(勝手に狩り・徘徊・逃走をしないようにする)
        if (idleBehavior != null) idleBehavior.enabled = false;
        if (fleeBehavior != null) fleeBehavior.enabled = false;
        if (predatorAI != null) predatorAI.enabled = false;
        if (navAgent != null)
        {
            navAgent.isStopped = true;
            navAgent.enabled = false; // 無効化することでUpdate自体が呼ばれなくなり、勝手な移動を完全に防げる
        }

        // HUD表示(体力・空腹・危険度)
        if (hudController != null)
        {
            hudController.ShowHUD(currentHealth, maxHealth, currentHunger, maxHunger, dangerLevel);
        }

        Debug.Log("動物に乗り移り、実態と同期しました！");
    }

    private void ReleaseAnimal()
    {
        isPossessing = false;

        // 1. 動物の親子関係を解除し、元の親に戻す
        animalRoot.SetParent(originalParent, true);

        // 2. 動物を憑依前の位置・向きに戻す
        animalRoot.position = originalPosition;
        animalRoot.rotation = originalRotation;

        // 3. Rigidbodyのkinematic状態を元に戻す
        if (animalRb != null)
        {
            animalRb.isKinematic = wasRigidbodyKinematic;
        }

        // 4. NavMeshAgentを先に再有効化してから、AIコンポーネントを戻す
        if (navAgent != null)
        {
            navAgent.enabled = true;
            // 元の位置がNavMesh上から外れていないかを念のため補正
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

        // HUD非表示
        if (hudController != null)
        {
            hudController.HideHUD();
        }

        Debug.Log("憑依を解除し、動物を元の位置に戻しました！");
    }

    /// <summary>
    /// 右人差し指トリガーで発動するアクション。
    /// 狩りが可能な動物(トラ等)で、かつ狩りの対象が射程内にいれば狩りを行う。
    /// 対象がいない場合は鳴き声を再生する。
    /// </summary>
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
                    StartCoroutine(HuntSequence(target));
                    return; // 狩りを実行したら鳴き声は鳴らさない
                }
            }
        }

        PlayCry();
    }

    private IEnumerator HuntSequence(AnimalIdentity target)
    {
        // 対象の方を向いて攻撃モーションを再生
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

        // 攻撃後、対象を死亡させる
        var targetHealth = target.GetComponent<AnimalHealth>();
        if (targetHealth != null)
        {
            targetHealth.Kill();
        }

        missionCompleted = true;

        Debug.Log("狩りに成功しました。");
    }

    private void PlayCry()
    {
        // クールダウン中は連打による多重再生を防ぐ
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