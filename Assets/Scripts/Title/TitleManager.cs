using UnityEngine;
using System.Collections;

/// <summary>
/// タイトル画面の演出を管理する。
/// シーン開始時はパネル・モデルともに非表示にしておき、
/// BGMのフェードインを開始した後、少し遅れてそれぞれが下から迫り上がってきながら表示される。
/// </summary>
public class TitleManager : MonoBehaviour
{
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

    [Header("パネルの演出設定(GameObject)")]
    [Tooltip("下から迫り上がってこさせたいパネルのTransform")]
    public Transform panelTransform;
    [Tooltip("パネルの最終的な位置(表示完了時のローカル座標)")]
    public Vector3 panelTargetLocalPosition;
    [Tooltip("最終位置からどれだけ下にずらした位置からスタートするか(メートル)")]
    public float panelStartOffsetY = 1f;
    [Tooltip("パネルが最終位置に到達するまでの時間(秒)。0以下ならBGMと同じfadeDurationを使う")]
    public float panelMoveDuration = 0f;
    [Tooltip("BGM開始から、パネルの迫り上がりを始めるまでの遅延時間(秒)")]
    public float panelStartDelay = 1f;
    [Tooltip("動きに緩急を付けるカーブ(徐々に減速して止まる動きにするため)")]
    public AnimationCurve panelMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("モデルの演出設定(GameObject)")]
    [Tooltip("下から迫り上がってこさせたい3DモデルのTransform")]
    public Transform modelTransform;
    [Tooltip("モデルの最終的な位置(表示完了時のローカル座標)")]
    public Vector3 modelTargetLocalPosition;
    [Tooltip("最終位置からどれだけ下にずらした位置からスタートするか(メートル)")]
    public float modelStartOffsetY = 2f;
    [Tooltip("モデルが最終位置に到達するまでの時間(秒)。0以下ならBGMと同じfadeDurationを使う")]
    public float modelMoveDuration = 0f;
    [Tooltip("BGM開始から、モデルの迫り上がりを始めるまでの遅延時間(秒)")]
    public float modelStartDelay = 1.5f;
    [Tooltip("動きに緩急を付けるカーブ(徐々に減速して止まる動きにするため)")]
    public AnimationCurve modelMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine fadeCoroutine;
    private Coroutine panelCoroutine;
    private Coroutine modelCoroutine;
    private Coroutine panelDelayCoroutine;
    private Coroutine modelDelayCoroutine;

    void Awake()
    {
        // 演出開始前は、パネル・モデルともに非表示にしておく
        if (panelTransform != null)
        {
            panelTransform.gameObject.SetActive(false);
        }
        if (modelTransform != null)
        {
            modelTransform.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (playOnStart)
        {
            StartTitleSequence();
        }
    }

    /// <summary>
    /// BGMのフェードインをすぐに開始し、パネル・モデルの迫り上がりはそれぞれ
    /// 設定した遅延時間の後に開始する。
    /// </summary>
    public void StartTitleSequence()
    {
        StartBgmFadeIn();

        if (panelDelayCoroutine != null) StopCoroutine(panelDelayCoroutine);
        panelDelayCoroutine = StartCoroutine(DelayedStart(panelStartDelay, StartPanelRiseUp));

        if (modelDelayCoroutine != null) StopCoroutine(modelDelayCoroutine);
        modelDelayCoroutine = StartCoroutine(DelayedStart(modelStartDelay, StartModelRiseUp));
    }

    /// <summary>
    /// 指定した秒数待ってから、渡されたアクションを実行する共通処理。
    /// </summary>
    private IEnumerator DelayedStart(float delay, System.Action action)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        action?.Invoke();
    }

    /// <summary>
    /// BGMのフェードインのみを開始する(単体でも呼び出せる)。
    /// </summary>
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

    /// <summary>
    /// パネル(GameObject)を表示し、目標位置より下の地点からスタートさせ、
    /// 時間をかけて目標位置まで迫り上げる。
    /// </summary>
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
        panelTransform.gameObject.SetActive(true); // ここで初めて表示する

        float duration = panelMoveDuration > 0f ? panelMoveDuration : fadeDuration;
        panelCoroutine = StartCoroutine(RiseUpRoutine(panelTransform, startPos, panelTargetLocalPosition, panelMoveCurve, duration, result => panelCoroutine = null));
    }

    /// <summary>
    /// 3Dモデル(GameObject)を表示し、目標位置より下の地点からスタートさせ、
    /// 時間をかけて目標位置まで迫り上げる。
    /// </summary>
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
        modelTransform.gameObject.SetActive(true); // ここで初めて表示する

        float duration = modelMoveDuration > 0f ? modelMoveDuration : fadeDuration;
        modelCoroutine = StartCoroutine(RiseUpRoutine(modelTransform, startPos, modelTargetLocalPosition, modelMoveCurve, duration, result => modelCoroutine = null));
    }

    /// <summary>
    /// 任意のTransformを、startPosからtargetPosへcurveに沿って時間をかけて移動させる共通処理。
    /// パネル・モデルの両方で使い回す。
    /// </summary>
    private IEnumerator RiseUpRoutine(Transform target, Vector3 startPos, Vector3 targetPos, AnimationCurve curve, float duration, System.Action<bool> onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = curve.Evaluate(t);
            target.localPosition = Vector3.Lerp(startPos, targetPos, curved);
            yield return null;
        }

        target.localPosition = targetPos;
        onComplete?.Invoke(true);
    }
}