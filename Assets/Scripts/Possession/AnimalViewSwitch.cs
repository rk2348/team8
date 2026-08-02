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
    [Tooltip("HMDのカメラ（CenterEyeAnchorなど）。Intro Display Objectを視界固定で表示する際の親として使う")]
    public Transform head;

    // 既に動物に乗り移っているかの判定
    private bool isPossessing = false;

    [Header("憑依時の演出設定")]
    [Tooltip("Aボタンを押してから実際に憑依するまでの間、カメラが動物の周りを1周する演出を行う")]
    public bool playPossessionIntro = true;
    [Tooltip("演出アニメーションを再生する対象のAnimator(この動物のAnimatorを直接アタッチしてください)。" +
              "Possession Intro Clipを使う場合は不要です")]
    public Animator possessionIntroAnimator;
    [Tooltip("周回終了後に再生する演出アニメーションのTrigger名(possessionIntroAnimatorのController内にStateとして" +
              "組み込まれているものを指定)。Possession Intro Clipを設定した場合はこちらは無視されます")]
    public string possessionIntroAnimTrigger = "Awaken";
    [Tooltip("Trigger名の代わりに、AnimationClipアセットを直接アタッチして再生したい場合はこちらを設定してください。" +
              "Animator Controller側にStateとして用意する必要がなくなります。設定するとTrigger方式より優先されます")]
    public AnimationClip possessionIntroClip;
    [Tooltip("Possession Intro Clipを再生するためのレガシーAnimationコンポーネント。" +
              "Possession Intro Clipを使う場合は、動物のオブジェクトにAnimationコンポーネントを追加してここにアタッチしてください")]
    public Animation possessionIntroAnimationComponent;
    [Tooltip("周回の半径(動物の中心からの水平距離、メートル)")]
    public float orbitRadius = 4.0f;
    [Tooltip("周回時の高さ(動物のルート位置からの相対的な高さ、メートル)。大きいほど『上から見下ろす』感が強くなる")]
    public float orbitHeight = 3.0f;
    [Tooltip("周回の開始位置を、動物の正面から見て何度の方向にするか。0=真正面、90=真右、-90=真左。" +
              "「右上前」なら正面よりやや右(例:45度)がおすすめ")]
    public float orbitStartAngleFromFront = 45f;
    [Tooltip("周回開始位置まで移動するのにかける時間(秒)")]
    public float moveToOrbitStartDuration = 1.0f;
    [Tooltip("動物の周りを1周するのにかける時間(秒)")]
    public float cameraOrbitDuration = 3.0f;
    [Tooltip("演出アニメーションの再生が終わるまでの待機時間(秒)。この後に実際の憑依処理を行う。" +
              "Possession Intro Clipを設定している場合はそのClipの長さが自動で使われます")]
    public float possessionIntroAnimDuration = 2.0f;
    [Tooltip("演出アニメーションを再生するタイミングで表示するオブジェクト(3Dオブジェクト・エフェクト・アイコンなど何でも可)。" +
              "シーン上に配置したGameObjectをアタッチしてください(表示/非表示のみこのスクリプトが制御します)")]
    public GameObject introDisplayObject;
    [Tooltip("Intro Display Objectを自動でhead(CenterEyeAnchor)の子にし、視界内の固定位置に配置する")]
    public bool autoAttachIntroDisplayToHead = true;
    [Tooltip("head基準のローカル座標。右下に見せたい場合の目安: X(右)はプラス、Y(下)はマイナス、Z(奥行き)はプラス")]
    public Vector3 introDisplayLocalPosition = new Vector3(0.3f, -0.2f, 0.5f);
    [Tooltip("head基準のローカル回転(度)")]
    public Vector3 introDisplayLocalEulerAngles = Vector3.zero;
    [Tooltip("head基準のローカルスケール")]
    public Vector3 introDisplayLocalScale = Vector3.one;
    [Tooltip("演出アニメーションを再生するタイミングで鳴らす効果音のAudioSource")]
    public AudioSource introSfxAudioSource;
    [Tooltip("再生する効果音のAudioClip")]
    public AudioClip introSfxClip;
    [Tooltip("演出アニメーション再生開始から何秒後にカメラを微振動させるか")]
    public float cameraShakeDelay = 0.5f;
    [Tooltip("カメラを微振動させ続ける時間(秒)")]
    public float cameraShakeDuration = 0.8f;
    [Tooltip("微振動の揺れ幅(メートル)")]
    public float cameraShakeMagnitude = 0.03f;
    private bool isPlayingPossessionIntro = false;

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

    [Tooltip("攻撃アニメーション(attackTrigger)を再生する対象のAnimator。" +
              "この動物にAnimatorが複数アタッチされている場合は、意図した方を直接ここに指定してください。" +
              "未設定ならGetComponentInChildrenで自動取得しますが、複数ある場合は狙った方が取れるとは限りません")]
    public Animator animator;
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

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        selfIdentity = GetComponent<AnimalIdentity>();

        if (possessionIntroAnimator == null)
        {
            possessionIntroAnimator = animator;
        }

        if (idleBehavior == null) idleBehavior = GetComponent<AnimalIdleBehavior>();
        if (fleeBehavior == null) fleeBehavior = GetComponent<FleeFromPredators>();
        if (predatorAI == null) predatorAI = GetComponent<PredatorAI>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

        SetupIntroDisplayObject();
    }

    /// <summary>
    /// Intro Display Objectをhead(CenterEyeAnchor)の子オブジェクトにし、
    /// 指定したローカル座標・回転・スケールに配置する。
    /// これにより、プレイヤーがどこを向いても常に視界の同じ位置(例:右下)に表示できる。
    /// 開始時は非表示にしておき、演出のタイミングでのみSetActive(true)にする。
    /// </summary>
    private void SetupIntroDisplayObject()
    {
        if (introDisplayObject == null) return;

        if (autoAttachIntroDisplayToHead && head != null)
        {
            introDisplayObject.transform.SetParent(head, false);
            introDisplayObject.transform.localPosition = introDisplayLocalPosition;
            introDisplayObject.transform.localRotation = Quaternion.Euler(introDisplayLocalEulerAngles);
            introDisplayObject.transform.localScale = introDisplayLocalScale;
        }
        else if (autoAttachIntroDisplayToHead && head == null)
        {
            Debug.LogWarning("AnimalViewSwitch: autoAttachIntroDisplayToHeadが有効ですが、headが設定されていません。" +
                "InspectorでCenterEyeAnchorなどをheadにアタッチしてください。");
        }

        introDisplayObject.SetActive(false); // 演出開始まで非表示にしておく
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

                if (!isPlayingPossessionIntro && OVRInput.GetDown(OVRInput.RawButton.A))
                {
                    if (playPossessionIntro)
                    {
                        StartCoroutine(PossessionIntroSequence());
                    }
                    else
                    {
                        PossessAnimal();
                    }
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

    /// <summary>
    /// Aボタンが押されてから実際に憑依するまでの演出シーケンス。
    /// 1. プレイヤーの通常操作を止める
    /// 2. playerRig(プレイヤーのリグ全体)を動物の周りに円軌道で1周させ、常に動物の方を向かせる
    /// 3. 周り終えたら、動物側で指定した演出アニメーションを再生し、同時に演出オブジェクトの表示とSEの再生を行う
    /// 4. アニメーション再生時間分待機
    /// 5. 演出オブジェクトを非表示に戻し、通常操作を戻し、実際の憑依処理(PossessAnimal)を行う
    /// </summary>
    private IEnumerator PossessionIntroSequence()
    {
        isPlayingPossessionIntro = true;

        // 演出中はVRMovement側の移動入力を無効化する(CharacterControllerを無効化するとMove()が効かなくなる)
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        yield return StartCoroutine(MoveToOrbitStart());
        yield return StartCoroutine(OrbitAroundAnimal());

        // Possession Intro Clipが設定されていればそちらを優先し、レガシーAnimationコンポーネントで直接再生する。
        // 未設定ならこれまで通りAnimator ControllerのTrigger経由で再生する。
        float waitDuration = possessionIntroAnimDuration;

        if (possessionIntroClip != null)
        {
            if (possessionIntroAnimationComponent != null)
            {
                possessionIntroAnimationComponent.clip = possessionIntroClip;
                possessionIntroAnimationComponent.Play(possessionIntroClip.name);
                waitDuration = possessionIntroClip.length; // Clipの長さをそのまま待機時間として使う
            }
            else
            {
                Debug.LogWarning("AnimalViewSwitch: possessionIntroClipは設定されていますが、" +
                    "possessionIntroAnimationComponent(Animationコンポーネント)がアタッチされていません。" +
                    "動物のオブジェクトにAnimationコンポーネントを追加してアタッチしてください。");
            }
        }
        else if (possessionIntroAnimator != null && !string.IsNullOrEmpty(possessionIntroAnimTrigger))
        {
            possessionIntroAnimator.SetTrigger(possessionIntroAnimTrigger);
        }
        else if (possessionIntroAnimator == null)
        {
            Debug.LogWarning("AnimalViewSwitch: possessionIntroAnimatorもpossessionIntroClipも設定されていません。" +
                "どちらか一方をInspectorで設定してください。");
        }

        // アニメーション再生と同じタイミングで、演出オブジェクトの表示とSEの再生を行う
        if (introDisplayObject != null)
        {
            introDisplayObject.SetActive(true);
        }
        if (introSfxAudioSource != null && introSfxClip != null)
        {
            introSfxAudioSource.PlayOneShot(introSfxClip);
        }

        // アニメーション再生開始と同時に、指定秒数後のカメラ微振動を並行して開始する
        // (メインの待機処理をブロックしないよう、別コルーチンとして起動するだけにする)
        StartCoroutine(ShakeCameraAfterDelay(cameraShakeDelay, cameraShakeDuration, cameraShakeMagnitude));

        yield return new WaitForSeconds(waitDuration);

        // 演出が終わったのでオブジェクトは非表示に戻す(この後PossessAnimal()で本来のHUDが表示される)
        if (introDisplayObject != null)
        {
            introDisplayObject.SetActive(false);
        }

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        isPlayingPossessionIntro = false;

        PossessAnimal();
    }

    /// <summary>
    /// delay秒待ってから、playerRigの位置をduration秒間だけ小刻みに揺らす(微振動)。
    /// 揺れ幅は時間経過とともに徐々に収まっていく。
    /// このコルーチンは呼び出し元(PossessionIntroSequence)の待機処理をブロックしないよう、
    /// StartCoroutineで並行実行する前提で作っている。
    /// </summary>
    private IEnumerator ShakeCameraAfterDelay(float delay, float duration, float magnitude)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (playerRig == null || duration <= 0f || magnitude <= 0f)
        {
            yield break;
        }

        Vector3 basePosition = playerRig.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / duration); // 時間経過で徐々に揺れが収まる

            Vector3 offset = Random.insideUnitSphere * magnitude * damper;
            offset.y *= 0.5f; // 上下の揺れはVR酔いを避けるため控えめにする

            playerRig.position = basePosition + offset;

            yield return null;
        }

        playerRig.position = basePosition;
    }

    /// <summary>
    /// 動物の向き(forward/right)を基準に「正面から見てorbitStartAngleFromFront度の方向、
    /// 高さorbitHeight、半径orbitRadius」の位置(=周回の開始位置)を算出し、
    /// そこまでplayerRigを滑らかに移動させる(常に動物の方を向かせながら)。
    /// </summary>
    private IEnumerator MoveToOrbitStart()
    {
        Vector3 startPosition = CalculateOrbitPosition(orbitStartAngleFromFront);
        Vector3 fromPosition = playerRig.position;
        Quaternion fromRotation = playerRig.rotation;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveToOrbitStartDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            playerRig.position = Vector3.Lerp(fromPosition, startPosition, t);

            // Y成分も含めて動物の方を向く(高さがある分、自然に見下ろす角度になる)
            Vector3 lookDir = animalRoot.position - playerRig.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                playerRig.rotation = Quaternion.Slerp(fromRotation, lookRot, t);
            }

            yield return null;
        }

        playerRig.position = startPosition;
        Vector3 finalLookDir = animalRoot.position - playerRig.position;
        if (finalLookDir.sqrMagnitude > 0.0001f)
        {
            playerRig.rotation = Quaternion.LookRotation(finalLookDir.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// 動物の正面方向を基準として、angleFromFrontDegrees度(右回り)・orbitRadius・orbitHeightから
    /// ワールド座標を算出する。0度=動物の正面、正の値=右方向。
    /// </summary>
    private Vector3 CalculateOrbitPosition(float angleFromFrontDegrees)
    {
        Vector3 animalForward = animalRoot.forward;
        animalForward.y = 0f;
        if (animalForward.sqrMagnitude < 0.0001f)
        {
            animalForward = Vector3.forward;
        }
        animalForward.Normalize();

        Quaternion rot = Quaternion.AngleAxis(angleFromFrontDegrees, Vector3.up);
        Vector3 direction = rot * animalForward;

        Vector3 position = animalRoot.position + direction * orbitRadius;
        position.y = animalRoot.position.y + orbitHeight;
        return position;
    }

    /// <summary>
    /// playerRigを動物(animalRoot)の周りに、orbitRadius・orbitHeightを使った円軌道で
    /// (現在いる開始位置から)1周(360度)させる。周回中は常に動物の方を向くようにする
    /// (ヘッド自体はHMDの実際の向きに追従するため、リグ全体の向きだけを制御する)。
    /// </summary>
    private IEnumerator OrbitAroundAnimal()
    {
        Vector3 pivot = animalRoot.position;

        // 半径はorbitRadius(Inspectorで設定した値、例:4m)で固定する。MoveToOrbitStart()でこの半径の
        // 位置まで移動しているはずだが、開始角度だけは現在位置から逆算して滑らかに繋げる。
        float radius = Mathf.Max(orbitRadius, 0.1f);

        Vector3 startOffset = playerRig.position - pivot;
        startOffset.y = 0f;
        if (startOffset.sqrMagnitude < 0.0001f)
        {
            startOffset = new Vector3(radius, 0f, 0f);
        }

        float startAngle = Mathf.Atan2(startOffset.z, startOffset.x);
        float startHeight = pivot.y + orbitHeight;

        float elapsed = 0f;
        while (elapsed < cameraOrbitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraOrbitDuration);
            float angle = startAngle + t * (Mathf.PI * 2f); // 360度分回転

            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 newPosition = pivot + offset;
            newPosition.y = startHeight; // 高さは変えない

            playerRig.position = newPosition;

            // Y成分も含めて動物を向く(高さがある分、周回中もずっと見下ろす角度を保つ)
            Vector3 lookDir = pivot - newPosition;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                playerRig.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            yield return null;
        }

        // 誤差が溜まっている可能性があるため、最後に開始角度ピッタリの位置へ合わせて1周を確実に終える
        Vector3 finalOffset = new Vector3(Mathf.Cos(startAngle), 0f, Mathf.Sin(startAngle)) * radius;
        Vector3 finalPosition = pivot + finalOffset;
        finalPosition.y = startHeight;
        playerRig.position = finalPosition;

        Vector3 finalLookDir2 = pivot - finalPosition;
        if (finalLookDir2.sqrMagnitude > 0.0001f)
        {
            playerRig.rotation = Quaternion.LookRotation(finalLookDir2.normalized, Vector3.up);
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