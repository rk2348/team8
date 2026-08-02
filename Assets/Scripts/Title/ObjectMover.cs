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

        [Tooltip("この区間の移動速度")]
        [Min(0.1f)] public float speed = 5.0f;

        [Tooltip("到着後の待機時間（秒）")]
        [Min(0f)] public float waitTime = 1.0f;

        [Space(10), Header("--- 振動設定 ---")]
        [Tooltip("この区間の移動中に振動するかどうか")]
        public bool vibrate = false;
        [Tooltip("振動の強さ（揺れ幅）")]
        public float vibrationIntensity = 0.05f;

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

    [Header("向きの設定")]
    [SerializeField, Tooltip("進行方向を向くかどうか")]
    private bool lookAtTarget = true;
    [SerializeField, Tooltip("振り向くスピード")]
    private float rotationSpeed = 10.0f;

    // 内部ステータス
    private Coroutine _pathCoroutine;
    private bool _isPaused = false;

    // 複数のAudioSourceを管理するリスト
    private List<AudioSource> _audioSources = new List<AudioSource>();

    void Awake()
    {
        // 最初にアタッチされているAudioSourceを取得してリストに追加
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
        // 移動開始
        _pathCoroutine = StartCoroutine(FollowPathRoutine());
    }

    /// <summary>
    /// メインの移動コルーチン
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
                // 1. 移動処理
                yield return StartCoroutine(MoveToTarget(wp, currentLogicPosition, (newPos) => currentLogicPosition = newPos));

                // 2. 到着時の処理（音、イベント）
                HandleArrivalActions(wp);

                // 3. 待機処理（ポーズ対応）
                if (wp.waitTime > 0)
                {
                    yield return StartCoroutine(WaitWithPause(wp.waitTime));
                }
            }

            // 次のポイントへ
            currentIndex++;
            if (currentIndex >= route.Count)
            {
                if (loop)
                {
                    currentIndex = 0; // ループ設定なら最初に戻る
                }
                else
                {
                    // ゴール時の後処理
                    ResetMedia();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// 目的地への移動処理を行うサブコルーチン
    /// </summary>
    private IEnumerator MoveToTarget(Waypoint wp, Vector3 startLogicPos, System.Action<Vector3> onUpdateLogicPos)
    {
        Vector3 currentLogicPosition = startLogicPos;

        while (Vector3.Distance(currentLogicPosition, wp.point.position) > 0.01f)
        {
            yield return new WaitUntil(() => !_isPaused);

            // 論理的な位置を更新
            currentLogicPosition = Vector3.MoveTowards(currentLogicPosition, wp.point.position, wp.speed * Time.deltaTime);
            onUpdateLogicPos?.Invoke(currentLogicPosition); // 親の変数を更新

            // 振動と位置の反映
            if (wp.vibrate)
            {
                Vector3 vibrationOffset = Random.insideUnitSphere * wp.vibrationIntensity;
                transform.position = currentLogicPosition + vibrationOffset;
            }
            else
            {
                transform.position = currentLogicPosition;
            }

            // 向きの更新
            if (lookAtTarget)
            {
                Vector3 direction = (wp.point.position - currentLogicPosition).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            yield return null;
        }

        // 最終的な位置合わせ
        transform.position = wp.point.position;
        onUpdateLogicPos?.Invoke(wp.point.position);
    }

    /// <summary>
    /// ポイント到着時の各種アクションを実行
    /// </summary>
    private void HandleArrivalActions(Waypoint wp)
    {
        // 現在鳴っている全てのSEを停止
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Stop();
        }

        // 複数のSEを同時に再生
        if (wp.enableSE && wp.seClips != null && wp.seClips.Count > 0)
        {
            for (int i = 0; i < wp.seClips.Count; i++)
            {
                AudioClip clip = wp.seClips[i];
                if (clip == null) continue;

                // 必要な数だけAudioSourceを動的に割り当て/生成
                AudioSource source = GetOrCreateAudioSource(i);
                source.clip = clip;
                source.loop = wp.loopSE;
                source.Play();
            }
        }
    }

    /// <summary>
    /// 必要なインデックスのAudioSourceを取得、足りなければ自動生成する
    /// </summary>
    private AudioSource GetOrCreateAudioSource(int index)
    {
        // 既に生成済みのAudioSourceがあればそれを返す
        if (index < _audioSources.Count)
        {
            return _audioSources[index];
        }

        // 足りない場合は新しくコンポーネントを追加してリストに登録する
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        _audioSources.Add(newSource);
        return newSource;
    }

    /// <summary>
    /// ポーズ機能を考慮した待機コルーチン
    /// </summary>
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

    /// <summary>
    /// ループ終了時などに音声をリセットする
    /// </summary>
    private void ResetMedia()
    {
        // 全ての音声を停止
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Stop();
        }
    }

    // --- 外部からの制御用API ---

    public void PauseMovement()
    {
        _isPaused = true;
        // 再生中の全ての音声を一時停止
        foreach (var source in _audioSources)
        {
            if (source.isPlaying) source.Pause();
        }
    }

    public void ResumeMovement()
    {
        _isPaused = false;
        // 全ての音声の一時停止を解除
        foreach (var source in _audioSources)
        {
            source.UnPause();
        }
    }

    // --- Sceneビューでの可視化 ---
    private void OnDrawGizmos()
    {
        if (route == null || route.Count < 2) return;

        // 変更箇所：通常のパスの線を赤色にする
        Gizmos.color = Color.red;
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (route[i].point != null && route[i + 1].point != null)
            {
                Gizmos.DrawLine(route[i].point.position, route[i + 1].point.position);
                Gizmos.DrawWireSphere(route[i].point.position, 0.2f);
            }
        }

        // 最後のポイントの球を描画
        if (route[route.Count - 1].point != null)
        {
            Gizmos.DrawWireSphere(route[route.Count - 1].point.position, 0.2f);
        }

        // ループする場合は終点と始点を繋ぐ
        if (loop && route[route.Count - 1].point != null && route[0].point != null)
        {
            // 変更箇所：ループの線も赤色にする
            Gizmos.color = Color.red;
            Gizmos.DrawLine(route[route.Count - 1].point.position, route[0].point.position);
        }
    }
}