using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// タイトル画面の演出を管理する。
/// シーン開始時はパネル・モデル・案内オブジェクト(いずれも3D GameObject)ともに非表示にしておき、
/// BGMのフェードインを開始した後、少し遅れてパネル・モデルが
/// 「下から迫り上がりながら」「小さい状態から本来のサイズへ拡大しながら」表示される。
/// 両方の表示が完了した後に案内用GameObjectを表示し、
/// その状態でAボタンを押すと、
/// 1) パネル・モデルが縮小しながら高速に下へ落下して消え、
/// 2) このスクリプトがアタッチされているオブジェクト自身も、一定時間待ってからX軸方向へ
///    ゆっくり回転し、回転完了後に複数のWaypointを経由しながら移動する(速度は一括管理)。
/// </summary>
public class TitleManager : MonoBehaviour
{
    [System.Serializable]
    public class MoveWaypoint
    {
        [Tooltip("経由するTransform")]
        public Transform point;
        [Tooltip("到着後の待機時間(秒)")]
        [Min(0f)] public float waitTime = 0f;
    }

    [Header("対象のBGM")]
    [Tooltip("フェードインさせるBGM用のAudioSource")]
    public AudioSource bgmSource;

    [Header("フェードの設定")]
    [Tooltip("音量が0から目標値に達するまでの時間(秒)")]
    public float fadeDuration = 5f;
    [Tooltip("最終的な目標音量(0?1)")]
    [Range(0f, 1f)] public float targetVolume = 1f;
    [Tooltip("シーン開始時に自動でフェードインを始めるか")]
    public bool playOnStart = true;

    [Header("パネルの演出設定(3D GameObject)")]
    [Tooltip("下から迫り上がってこさせたいパネルのTransform")]
    public Transform panelTransform;
    [Tooltip("パネルの最終的な位置(表示完了時のローカル座標)")]
    public Vector3 panelTargetLocalPosition;
    [Tooltip("パネルの最終的なスケール(通常は元のLocalScaleと同じ値)")]
    public Vector3 panelTargetLocalScale = Vector3.one;
    [Tooltip("最終位置からどれだけ下にずらした位置からスタートするか(メートル)")]
    public float panelStartOffsetY = 1f;
    [Tooltip("パネルが最終位置に到達するまでの時間(秒)。0以下ならBGMと同じfadeDurationを使う")]
    public float panelMoveDuration = 0f;
    [Tooltip("BGM開始から、パネルの迫り上がりを始めるまでの遅延時間(秒)")]
    public float panelStartDelay = 1f;
    [Tooltip("動きに緩急を付けるカーブ(位置・スケールの両方に使う)")]
    public AnimationCurve panelMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("モデルの演出設定(3D GameObject)")]
    [Tooltip("下から迫り上がってこさせたい3DモデルのTransform")]
    public Transform modelTransform;
    [Tooltip("モデルの最終的な位置(表示完了時のローカル座標)")]
    public Vector3 modelTargetLocalPosition;
    [Tooltip("モデルの最終的なスケール(通常は元のLocalScaleと同じ値)")]
    public Vector3 modelTargetLocalScale = Vector3.one;
    [Tooltip("最終位置からどれだけ下にずらした位置からスタートするか(メートル)")]
    public float modelStartOffsetY = 2f;
    [Tooltip("モデルが最終位置に到達するまでの時間(秒)。0以下ならBGMと同じfadeDurationを使う")]
    public float modelMoveDuration = 0f;
    [Tooltip("BGM開始から、モデルの迫り上がりを始めるまでの遅延時間(秒)")]
    public float modelStartDelay = 1.5f;
    [Tooltip("動きに緩急を付けるカーブ(位置・スケールの両方に使う)")]
    public AnimationCurve modelMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("案内オブジェクトの設定(GameObject)")]
    [Tooltip("パネル・モデルの登場が完了した後に表示するGameObject(TMP_Textが付いていれば文言も自動設定する)")]
    public GameObject promptObject;
    [Tooltip("案内オブジェクトにTMP_Text(TextMeshPro/TextMeshProUGUIどちらも可)が付いている場合、表示する文言")]
    public string promptMessage = "Aボタンでスタート";
    [Tooltip("パネル・モデル両方の登場完了から、案内オブジェクトを表示するまでの追加待機時間(秒)")]
    public float promptDelayAfterRiseUp = 0.5f;

    [Header("退場演出の設定(パネル・モデル、Aボタンで発動)")]
    [Tooltip("退場時、下へ落下する速さの目安(1秒あたりの移動距離、メートル)")]
    public float exitFallSpeed = 15f;
    [Tooltip("退場時、縮小・落下にかける時間(秒)")]
    public float exitDuration = 0.4f;
    [Tooltip("退場の動きに緩急を付けるカーブ(急加速するイメージ)")]
    public AnimationCurve exitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("自分自身(このオブジェクト)の回転設定(Aボタンで発動)")]
    [Tooltip("Aボタン押下から、回転を始めるまでの待機時間(秒)")]
    public float selfRotateDelay = 1.0f;
    [Tooltip("回転にかける時間(秒)。ゆっくり回転させたい場合は長めに")]
    public float selfRotateDuration = 1.5f;
    [Tooltip("回転させるX軸の最終角度")]
    public float selfRotationX = 90f;
    [Tooltip("回転の緩急を付けるカーブ")]
    public AnimationCurve selfRotateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("自分自身(このオブジェクト)の移動設定(回転完了後、Waypoint経由)")]
    [Tooltip("回転完了後に経由していくポイントのリスト")]
    public List<MoveWaypoint> selfMoveRoute = new List<MoveWaypoint>();
    [Tooltip("全区間で共通して使う移動速度(メートル/秒)")]
    [Min(0.1f)] public float selfMoveSpeed = 20f;
    [Tooltip("進行方向を向くかどうか(Waypoint移動中)")]
    public bool selfMoveLookAtTarget = true;
    [Tooltip("向きを合わせる速さ")]
    public float selfMoveRotationSpeed = 10f;
    [Tooltip("ONにするとWaypoint間を滑らかな曲線(Catmull-Romスプライン)で移動する")]
    public bool selfMoveUseSmoothCurve = true;

    private Coroutine fadeCoroutine;
    private Coroutine panelCoroutine;
    private Coroutine modelCoroutine;
    private Coroutine panelDelayCoroutine;
    private Coroutine modelDelayCoroutine;
    private Coroutine exitCoroutine;
    private Coroutine selfRotateAndMoveCoroutine;

    // 両方の登場アニメーションが完了したかどうかを個別に管理
    private bool panelRiseUpDone = false;
    private bool modelRiseUpDone = false;
    // Aボタンでの退場を受け付けてよい状態かどうか(演出中の誤入力を防ぐ)
    private bool canExit = false;

    void Awake()
    {
        if (panelTransform != null)
        {
            panelTransform.gameObject.SetActive(false);
        }
        if (modelTransform != null)
        {
            modelTransform.gameObject.SetActive(false);
        }
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }
    }

    void Start()
    {
        if (playOnStart)
        {
            StartTitleSequence();
        }
    }

    void Update()
    {
        if (canExit && OVRInput.GetDown(OVRInput.RawButton.A))
        {
            canExit = false; // 連打による多重発火を防ぐ

            if (promptObject != null)
            {
                promptObject.SetActive(false);
            }

            StartExitSequence();
            StartSelfRotateAndFastMove();
        }
    }

    public void StartTitleSequence()
    {
        StartBgmFadeIn();

        panelRiseUpDone = false;
        modelRiseUpDone = false;
        canExit = false;

        if (panelDelayCoroutine != null) StopCoroutine(panelDelayCoroutine);
        panelDelayCoroutine = StartCoroutine(DelayedStart(panelStartDelay, StartPanelRiseUp));

        if (modelDelayCoroutine != null) StopCoroutine(modelDelayCoroutine);
        modelDelayCoroutine = StartCoroutine(DelayedStart(modelStartDelay, StartModelRiseUp));
    }

    private IEnumerator DelayedStart(float delay, System.Action action)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        action?.Invoke();
    }

    public void StartBgmFadeIn()
    {
        if (bgmSource == null)
        {
            Debug.LogWarning("TitleManager: bgmSourceが設定されていません。");
            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        bgmSource.volume = 0f;

        if (!bgmSource.isPlaying)
        {
            bgmSource.Play();
        }

        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        bgmSource.volume = targetVolume;
        fadeCoroutine = null;
    }

    public void StartPanelRiseUp()
    {
        if (panelTransform == null)
        {
            Debug.LogWarning("TitleManager: panelTransformが設定されていません。");
            return;
        }

        if (panelCoroutine != null)
        {
            StopCoroutine(panelCoroutine);
        }

        Vector3 startPos = panelTargetLocalPosition + Vector3.down * panelStartOffsetY;
        panelTransform.localPosition = startPos;
        panelTransform.localScale = Vector3.zero;

        panelTransform.gameObject.SetActive(true);

        float duration = panelMoveDuration > 0f ? panelMoveDuration : fadeDuration;
        panelCoroutine = StartCoroutine(RiseUpRoutine(panelTransform, startPos, panelTargetLocalPosition, panelTargetLocalScale, panelMoveCurve, duration, () =>
        {
            panelCoroutine = null;
            panelRiseUpDone = true;
            CheckBothRiseUpDone();
        }));
    }

    public void StartModelRiseUp()
    {
        if (modelTransform == null)
        {
            Debug.LogWarning("TitleManager: modelTransformが設定されていません。");
            return;
        }

        if (modelCoroutine != null)
        {
            StopCoroutine(modelCoroutine);
        }

        Vector3 startPos = modelTargetLocalPosition + Vector3.down * modelStartOffsetY;
        modelTransform.localPosition = startPos;
        modelTransform.localScale = Vector3.zero;

        modelTransform.gameObject.SetActive(true);

        float duration = modelMoveDuration > 0f ? modelMoveDuration : fadeDuration;
        modelCoroutine = StartCoroutine(RiseUpRoutine(modelTransform, startPos, modelTargetLocalPosition, modelTargetLocalScale, modelMoveCurve, duration, () =>
        {
            modelCoroutine = null;
            modelRiseUpDone = true;
            CheckBothRiseUpDone();
        }));
    }

    /// <summary>
    /// パネル・モデル両方の登場アニメーションが完了したら、案内オブジェクトを表示する。
    /// </summary>
    private void CheckBothRiseUpDone()
    {
        if (panelRiseUpDone && modelRiseUpDone)
        {
            StartCoroutine(ShowPromptAfterDelay());
        }
    }

    private IEnumerator ShowPromptAfterDelay()
    {
        if (promptDelayAfterRiseUp > 0f)
        {
            yield return new WaitForSeconds(promptDelayAfterRiseUp);
        }

        if (promptObject != null)
        {
            var tmpText = promptObject.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = promptMessage;
            }

            promptObject.SetActive(true);
        }

        canExit = true; // ここでAボタンの入力を受け付け開始
    }

    private IEnumerator RiseUpRoutine(Transform target, Vector3 startPos, Vector3 targetPos, Vector3 targetScale, AnimationCurve curve, float duration, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = curve.Evaluate(t);

            target.localPosition = Vector3.Lerp(startPos, targetPos, curved);
            target.localScale = Vector3.Lerp(Vector3.zero, targetScale, curved);

            yield return null;
        }

        target.localPosition = targetPos;
        target.localScale = targetScale;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Aボタンが押されたときに呼ぶ。パネル・モデル両方を、縮小しながら
    /// 高速に下方向へ落下させて消す。
    /// </summary>
    public void StartExitSequence()
    {
        if (exitCoroutine != null)
        {
            StopCoroutine(exitCoroutine);
        }
        exitCoroutine = StartCoroutine(ExitSequenceRoutine());
    }

    private IEnumerator ExitSequenceRoutine()
    {
        Vector3 panelStart = panelTransform != null ? panelTransform.localPosition : Vector3.zero;
        Vector3 panelStartScale = panelTransform != null ? panelTransform.localScale : Vector3.zero;
        Vector3 modelStart = modelTransform != null ? modelTransform.localPosition : Vector3.zero;
        Vector3 modelStartScale = modelTransform != null ? modelTransform.localScale : Vector3.zero;

        float fallDistance = exitFallSpeed * exitDuration;
        Vector3 panelExitPos = panelStart + Vector3.down * fallDistance;
        Vector3 modelExitPos = modelStart + Vector3.down * fallDistance;

        float elapsed = 0f;

        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);
            float curved = exitCurve.Evaluate(t);

            if (panelTransform != null)
            {
                panelTransform.localPosition = Vector3.Lerp(panelStart, panelExitPos, curved);
                panelTransform.localScale = Vector3.Lerp(panelStartScale, Vector3.zero, curved);
            }
            if (modelTransform != null)
            {
                modelTransform.localPosition = Vector3.Lerp(modelStart, modelExitPos, curved);
                modelTransform.localScale = Vector3.Lerp(modelStartScale, Vector3.zero, curved);
            }

            yield return null;
        }

        if (panelTransform != null)
        {
            panelTransform.gameObject.SetActive(false);
        }
        if (modelTransform != null)
        {
            modelTransform.gameObject.SetActive(false);
        }

        exitCoroutine = null;

        Debug.Log("パネル・モデルの退場演出が終了しました。");
    }

    /// <summary>
    /// Aボタンが押されたときに呼ぶ。selfRotateDelay秒待ってから、
    /// このオブジェクト自身をX軸方向へゆっくり回転させ、
    /// 回転が完了したらselfMoveRouteのWaypointを順に経由しながら移動する。
    /// </summary>
    public void StartSelfRotateAndFastMove()
    {
        if (selfRotateAndMoveCoroutine != null)
        {
            StopCoroutine(selfRotateAndMoveCoroutine);
        }
        selfRotateAndMoveCoroutine = StartCoroutine(SelfRotateThenMoveRoutine());
    }

    private IEnumerator SelfRotateThenMoveRoutine()
    {
        // 1. 一定時間待つ
        if (selfRotateDelay > 0f)
        {
            yield return new WaitForSeconds(selfRotateDelay);
        }

        // 2. ゆっくり回転する
        Quaternion startRotation = transform.localRotation;
        Quaternion targetRotation = Quaternion.Euler(selfRotationX, 0f, 0f);

        float rotateElapsed = 0f;
        while (rotateElapsed < selfRotateDuration)
        {
            rotateElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(rotateElapsed / selfRotateDuration);
            float curved = selfRotateCurve.Evaluate(t);

            transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, curved);

            yield return null;
        }
        transform.localRotation = targetRotation;

        // 3. 回転完了後、Waypointを順に経由しながら移動する(速度はselfMoveSpeedで一括管理)
        yield return StartCoroutine(FollowSelfMoveRoute());

        selfRotateAndMoveCoroutine = null;
        Debug.Log("自分自身の回転・Waypoint移動演出が終了しました。");
    }

    /// <summary>
    /// selfMoveRouteに登録されたWaypointを順番に、Catmull-Romスプラインで滑らかに経由しながら移動する。
    /// </summary>
    private IEnumerator FollowSelfMoveRoute()
    {
        if (selfMoveRoute == null || selfMoveRoute.Count == 0)
        {
            yield break;
        }

        Vector3 currentPos = transform.position;

        for (int i = 0; i < selfMoveRoute.Count; i++)
        {
            MoveWaypoint wp = selfMoveRoute[i];
            if (wp.point == null) continue;

            Vector3 prevPoint = i > 0 && selfMoveRoute[i - 1].point != null
                ? selfMoveRoute[i - 1].point.position
                : currentPos;
            Vector3 nextPoint = (i + 1 < selfMoveRoute.Count && selfMoveRoute[i + 1].point != null)
                ? selfMoveRoute[i + 1].point.position
                : wp.point.position;

            yield return StartCoroutine(MoveToWaypoint(wp, currentPos, prevPoint, nextPoint, (newPos) => currentPos = newPos));

            if (wp.waitTime > 0f)
            {
                yield return new WaitForSeconds(wp.waitTime);
            }
        }
    }

    /// <summary>
    /// 1区間分の移動。selfMoveUseSmoothCurveがONなら前後のWaypointを考慮した
    /// Catmull-Rom補間でカーブしながら進む。速度は全区間共通のselfMoveSpeedを使う。
    /// </summary>
    private IEnumerator MoveToWaypoint(MoveWaypoint wp, Vector3 startPos, Vector3 prevPoint, Vector3 nextPoint, System.Action<Vector3> onUpdatePos)
    {
        Vector3 endPos = wp.point.position;
        float distance = Vector3.Distance(startPos, endPos);
        if (distance < 0.001f)
        {
            onUpdatePos?.Invoke(endPos);
            yield break;
        }

        float duration = distance / selfMoveSpeed;
        float elapsed = 0f;

        Vector3 p0 = prevPoint;
        Vector3 p1 = startPos;
        Vector3 p2 = endPos;
        Vector3 p3 = nextPoint;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 targetPosition = selfMoveUseSmoothCurve
                ? CatmullRom(p0, p1, p2, p3, t)
                : Vector3.Lerp(p1, p2, t);

            transform.position = targetPosition;

            if (selfMoveLookAtTarget)
            {
                float lookAheadT = Mathf.Clamp01(t + 0.05f);
                Vector3 lookAheadPos = selfMoveUseSmoothCurve
                    ? CatmullRom(p0, p1, p2, p3, lookAheadT)
                    : Vector3.Lerp(p1, p2, lookAheadT);

                Vector3 direction = lookAheadPos - targetPosition;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, selfMoveRotationSpeed * Time.deltaTime);
                }
            }

            onUpdatePos?.Invoke(targetPosition);

            yield return null;
        }

        transform.position = endPos;
        onUpdatePos?.Invoke(endPos);
    }

    /// <summary>
    /// Catmull-Romスプライン補間。p1→p2の間を、前後の制御点p0・p3を考慮して滑らかに補間する。
    /// </summary>
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private void OnDrawGizmos()
    {
        if (selfMoveRoute == null || selfMoveRoute.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < selfMoveRoute.Count - 1; i++)
        {
            if (selfMoveRoute[i].point != null && selfMoveRoute[i + 1].point != null)
            {
                Gizmos.DrawLine(selfMoveRoute[i].point.position, selfMoveRoute[i + 1].point.position);
                Gizmos.DrawWireSphere(selfMoveRoute[i].point.position, 0.2f);
            }
        }

        if (selfMoveRoute[selfMoveRoute.Count - 1].point != null)
        {
            Gizmos.DrawWireSphere(selfMoveRoute[selfMoveRoute.Count - 1].point.position, 0.2f);
        }
    }
}