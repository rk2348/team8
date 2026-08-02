using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class ObjectMover : MonoBehaviour
{
    [System.Serializable]
    public class Waypoint
    {
        [Tooltip("目的地のTransform")]
        public Transform point;

        [Tooltip("到着後の待機時間（秒）")]
        [Min(0f)] public float waitTime = 1.0f;

        [Space(10), Header("--- SE(効果音)設定 ---")]
        [Tooltip("到着時にSEを再生するかどうか")]
        public bool enableSE = false;
        [Tooltip("再生する音声クリップのリスト（複数設定すると同時に再生されます）")]
        public List<AudioClip> seClips = new List<AudioClip>();
        [Tooltip("ループ再生するかどうか")]
        public bool loopSE = false;
    }

    [Header("ルート設定")]
    [SerializeField, Tooltip("移動するポイントのリスト")]
    private List<Waypoint> route = new List<Waypoint>();
    [SerializeField, Tooltip("最後まで行ったら最初に戻るか")]
    private bool loop = false;

    [Header("移動速度の設定(全区間共通)")]
    [SerializeField, Tooltip("全区間で共通して使う移動速度(メートル/秒)")]
    [Min(0.1f)] private float moveSpeed = 5.0f;

    [Header("曲線移動の設定")]
    [SerializeField, Tooltip("ONにするとWaypoint間を滑らかな曲線(Catmull-Romスプライン)で移動する。OFFなら従来通りの直線移動")]
    private bool useSmoothCurve = true;
    [SerializeField, Tooltip("待機(waitTime)がある区間の前後だけは、曲線を使わず一旦完全停止させる")]
    private bool stopFullyAtWaitPoints = true;

    [Header("向きの設定")]
    [SerializeField, Tooltip("進行方向を向くかどうか")]
    private bool lookAtTarget = true;
    [SerializeField, Tooltip("向きを合わせる速さ。大きいほど進行方向にきびきび追従する")]
    private float rotationSpeed = 10.0f;

    // 内部ステータス
    private Coroutine _pathCoroutine;
    private bool _isPaused = false;

    // 複数のAudioSourceを管理するリスト
    private List<AudioSource> _audioSources = new List<AudioSource>();

    /// <summary>
    /// 全区間共通の移動速度。実行中に変更したい場合はこのプロパティを使う。
    /// </summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0.1f, value);
    }

    void Awake()
    {
        AudioSource initialSource = GetComponent<AudioSource>();
        if (initialSource != null)
        {
            initialSource.playOnAwake = false;
            _audioSources.Add(initialSource);
        }
    }

    void Start()
    {
        if (route == null || route.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: Waypointが設定されていません。");
            return;
        }
        _pathCoroutine = StartCoroutine(FollowPathRoutine());
    }

    /// <summary>
    /// メインの移動コルーチン。
    /// useSmoothCurveがONの場合、待機時間が無い区間同士は速度を落とさず滑らかに繋げて曲がる。
    /// </summary>
    private IEnumerator FollowPathRoutine()
    {
        int currentIndex = 0;
        Vector3 currentLogicPosition = transform.position;

        while (currentIndex < route.Count)
        {
            Waypoint wp = route[currentIndex];

            if (wp.point != null)
            {
                Vector3 prevPoint = currentIndex > 0 && route[currentIndex - 1].point != null
                    ? route[currentIndex - 1].point.position
                    : currentLogicPosition;
                Vector3 nextPoint = (currentIndex + 1 < route.Count && route[currentIndex + 1].point != null)
                    ? route[currentIndex + 1].point.position
                    : wp.point.position;

                yield return StartCoroutine(MoveToTarget(
                    wp,
                    currentLogicPosition,
                    prevPoint,
                    nextPoint,
                    (newPos) => currentLogicPosition = newPos));

                HandleArrivalActions(wp);

                if (wp.waitTime > 0)
                {
                    yield return StartCoroutine(WaitWithPause(wp.waitTime));
                }
            }

            currentIndex++;
            if (currentIndex >= route.Count)
            {
                if (loop)
                {
                    currentIndex = 0;
                }
                else
                {
                    ResetMedia();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// 目的地への移動処理。useSmoothCurveがONなら、前後の区間も考慮した
    /// Catmull-Rom補間でカーブしながら進む。速度は全区間共通のmoveSpeedを使う。
    /// </summary>
    private IEnumerator MoveToTarget(Waypoint wp, Vector3 startPos, Vector3 prevPoint, Vector3 nextPoint, System.Action<Vector3> onUpdateLogicPos)
    {
        Vector3 endPos = wp.point.position;
        float distance = Vector3.Distance(startPos, endPos);
        if (distance < 0.001f)
        {
            onUpdateLogicPos?.Invoke(endPos);
            yield break;
        }

        float duration = distance / moveSpeed;
        float elapsed = 0f;

        // Catmull-Rom用の制御点(始点の前・終点の後ろ)
        Vector3 p0 = prevPoint;
        Vector3 p1 = startPos;
        Vector3 p2 = endPos;
        Vector3 p3 = nextPoint;

        while (elapsed < duration)
        {
            yield return new WaitUntil(() => !_isPaused);

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 targetPosition = useSmoothCurve
                ? CatmullRom(p0, p1, p2, p3, t)
                : Vector3.Lerp(p1, p2, t);

            transform.position = targetPosition;

            if (lookAtTarget)
            {
                // ほんの少し先の点を見ることで、カーブの接線方向に自然に向きを合わせる
                float lookAheadT = Mathf.Clamp01(t + 0.05f);
                Vector3 lookAheadPos = useSmoothCurve
                    ? CatmullRom(p0, p1, p2, p3, lookAheadT)
                    : Vector3.Lerp(p1, p2, lookAheadT);

                Vector3 direction = (lookAheadPos - targetPosition);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            onUpdateLogicPos?.Invoke(targetPosition);

            yield return null;
        }

        transform.position = endPos;
        onUpdateLogicPos?.Invoke(endPos);
    }

    /// <summary>
    /// Catmull-Romスプライン補間。p1→p2の間を、前後の制御点p0・p3を考慮して滑らかに補間する。
    /// 折れ線ではなく曲線として経路をつなぐことで、Waypoint通過時のカクつきを解消する。
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

    private void HandleArrivalActions(Waypoint wp)
    {
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Stop();
        }

        if (wp.enableSE && wp.seClips != null && wp.seClips.Count > 0)
        {
            for (int i = 0; i < wp.seClips.Count; i++)
            {
                AudioClip clip = wp.seClips[i];
                if (clip == null) continue;

                AudioSource source = GetOrCreateAudioSource(i);
                source.clip = clip;
                source.loop = wp.loopSE;
                source.Play();
            }
        }
    }

    private AudioSource GetOrCreateAudioSource(int index)
    {
        if (index < _audioSources.Count)
        {
            return _audioSources[index];
        }

        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        _audioSources.Add(newSource);
        return newSource;
    }

    private IEnumerator WaitWithPause(float waitTime)
    {
        float timer = 0f;
        while (timer < waitTime)
        {
            yield return new WaitUntil(() => !_isPaused);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void ResetMedia()
    {
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Stop();
        }
    }

    public void PauseMovement()
    {
        _isPaused = true;
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Pause();
        }
    }

    public void ResumeMovement()
    {
        _isPaused = false;
        foreach (var source in _audioSources)
        {
            source.UnPause();
        }
    }

    private void OnDrawGizmos()
    {
        if (route == null || route.Count < 2) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (route[i].point != null && route[i + 1].point != null)
            {
                Gizmos.DrawLine(route[i].point.position, route[i + 1].point.position);
                Gizmos.DrawWireSphere(route[i].point.position, 0.2f);
            }
        }

        if (route[route.Count - 1].point != null)
        {
            Gizmos.DrawWireSphere(route[route.Count - 1].point.position, 0.2f);
        }

        if (loop && route[route.Count - 1].point != null && route[0].point != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(route[route.Count - 1].point.position, route[0].point.position);
        }
    }
}