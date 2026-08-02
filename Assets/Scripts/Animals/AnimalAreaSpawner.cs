using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 指定したエリア内に、動物ごとに設定した頭数を配置するスポナー。
/// 群れを作る動物(formsHerds=true)は複数の群れに分けて配置し、
/// 各群れ内のメンバーをHerdBehaviorに紐付けて群れ行動をさせる。
/// 単独行動の動物(トラなど)は、群れ・他の単独個体から一定距離を保って散らして配置する。
/// </summary>
public class AnimalAreaSpawner : MonoBehaviour
{
    [System.Serializable]
    public class AnimalSpawnConfig
    {
        [Tooltip("Inspector上での識別用ラベル(任意)")]
        public string label = "動物名";
        [Tooltip("配置するプレハブ")]
        public GameObject prefab;
        [Tooltip("配置する種族(AnimalIdentityと合わせる)")]
        public AnimalIdentity.AnimalSpecies species;
        [Tooltip("このエリア内に配置する総数")]
        public int totalCount = 10;

        [Header("群れ設定")]
        [Tooltip("群れを作る動物かどうか(シマウマ・シカ等はON、トラはOFF)")]
        public bool formsHerds = false;
        [Tooltip("群れを作る場合、1つの群れあたりの頭数の範囲(min~max、ランダム)")]
        public Vector2Int herdSizeRange = new Vector2Int(3, 6);
        [Tooltip("群れの中で個体同士をばらける半径")]
        public float herdSpreadRadius = 4f;
        [Tooltip("群れの中心点同士を最低これだけ離す(群れ同士が重ならないように)")]
        public float minHerdCenterDistance = 15f;

        [Header("単独配置設定(formsHerds=falseの場合に使用)")]
        [Tooltip("単独個体同士・群れの中心から最低これだけ離す")]
        public float minIndividualDistance = 10f;

        [Header("配置後にHerdBehaviorへ適用するパラメータ(formsHerds=trueの場合)")]
        [Tooltip("チェックを入れると、下記の値でHerdBehaviorの初期値を上書きする(動物ごとのチューニング用)")]
        public bool overrideHerdParams = false;
        public float herdRadius = 6f;
        public float cohesionWeight = 1f;
        public float separationDistance = 2f;
        public float separationWeight = 1.5f;
    }

    [Header("配置エリア(ワールド座標基準の矩形)")]
    [Tooltip("エリアの中心座標")]
    public Vector3 areaCenter;
    [Tooltip("エリアの大きさ(X,Z軸のみ使用。Yは無視される)")]
    public Vector3 areaSize = new Vector3(50f, 0f, 50f);

    [Header("NavMesh配置設定")]
    [Tooltip("候補地点からNavMesh上の有効な位置を探す際のサンプリング半径")]
    public float navMeshSampleRadius = 3f;
    [Tooltip("1体あたり、有効な配置位置を探す最大試行回数")]
    public int maxPlacementAttemptsPerAnimal = 30;

    [Header("動物ごとの配置設定")]
    public List<AnimalSpawnConfig> spawnConfigs = new List<AnimalSpawnConfig>();

    [Header("動作設定")]
    [Tooltip("Playと同時に自動で配置を実行する")]
    public bool spawnOnStart = true;
    [Tooltip("再現性のある配置にしたい場合、乱数シードを固定する")]
    public bool useRandomSeed = false;
    public int randomSeed = 0;

    // 距離チェック用に、これまでに配置した全ての座標(群れの中心・単独個体問わず)を保持する
    private readonly List<Vector3> placedPositions = new List<Vector3>();

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnAll();
        }
    }

    /// <summary>
    /// spawnConfigsに設定された内容に従って、全ての動物を配置する。
    /// </summary>
    [ContextMenu("配置を実行")]
    public void SpawnAll()
    {
        if (useRandomSeed)
        {
            Random.InitState(randomSeed);
        }

        placedPositions.Clear();

        foreach (var config in spawnConfigs)
        {
            if (config.prefab == null)
            {
                Debug.LogWarning($"AnimalAreaSpawner: 「{config.label}」のprefabが未設定のためスキップします。");
                continue;
            }

            if (config.formsHerds)
            {
                SpawnHerdingSpecies(config);
            }
            else
            {
                SpawnSolitarySpecies(config);
            }
        }
    }

    /// <summary>
    /// 群れを作る種族を、複数の群れに分けて配置する。
    /// </summary>
    private void SpawnHerdingSpecies(AnimalSpawnConfig config)
    {
        int remaining = config.totalCount;
        int safetyCounter = 0;

        while (remaining > 0 && safetyCounter < 200)
        {
            safetyCounter++;

            int herdSize = Mathf.Min(remaining, Random.Range(config.herdSizeRange.x, config.herdSizeRange.y + 1));
            herdSize = Mathf.Max(1, herdSize);

            Vector3? herdCenter = FindValidPosition(config.minHerdCenterDistance);
            if (herdCenter == null)
            {
                Debug.LogWarning($"AnimalAreaSpawner: 「{config.label}」の群れ中心がエリア内に見つかりませんでした。残り{remaining}頭は配置を諦めます。エリアを広げるかminHerdCenterDistanceを下げてください。");
                break;
            }

            placedPositions.Add(herdCenter.Value);

            List<Transform> herdMembers = new List<Transform>();

            for (int i = 0; i < herdSize; i++)
            {
                Vector2 offset2D = Random.insideUnitCircle * config.herdSpreadRadius;
                Vector3 candidate = herdCenter.Value + new Vector3(offset2D.x, 0f, offset2D.y);

                if (!TrySampleNavMesh(candidate, out Vector3 finalPos))
                {
                    continue; // NavMesh上に有効な位置が無ければこの1体はスキップ
                }

                GameObject go = Instantiate(config.prefab, finalPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                herdMembers.Add(go.transform);
            }

            // 群れメンバー同士を紐付け、必要ならHerdBehaviorのパラメータも上書きする
            foreach (var member in herdMembers)
            {
                var herd = member.GetComponent<HerdBehavior>();
                if (herd == null)
                {
                    continue; // HerdBehaviorが付いていないプレハブなら何もしない
                }

                herd.SetHerdMembers(herdMembers);

                if (config.overrideHerdParams)
                {
                    herd.herdRadius = config.herdRadius;
                    herd.cohesionWeight = config.cohesionWeight;
                    herd.separationDistance = config.separationDistance;
                    herd.separationWeight = config.separationWeight;
                }
            }

            remaining -= herdSize;
        }
    }

    /// <summary>
    /// 単独行動の種族(トラなど)を、他の個体・群れの中心から距離を保ちつつ配置する。
    /// </summary>
    private void SpawnSolitarySpecies(AnimalSpawnConfig config)
    {
        for (int i = 0; i < config.totalCount; i++)
        {
            Vector3? pos = FindValidPosition(config.minIndividualDistance);
            if (pos == null)
            {
                Debug.LogWarning($"AnimalAreaSpawner: 「{config.label}」の配置位置が見つかりませんでした({i + 1}/{config.totalCount}体目)。エリアを広げるかminIndividualDistanceを下げてください。");
                continue;
            }

            if (!TrySampleNavMesh(pos.Value, out Vector3 finalPos))
            {
                continue;
            }

            Instantiate(config.prefab, finalPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            placedPositions.Add(finalPos);
        }
    }

    /// <summary>
    /// エリア内でランダムな候補地点を探し、既存の配置済み座標から
    /// minDistanceFromOthers以上離れている地点が見つかるまで試行する。
    /// </summary>
    private Vector3? FindValidPosition(float minDistanceFromOthers)
    {
        for (int attempt = 0; attempt < maxPlacementAttemptsPerAnimal; attempt++)
        {
            Vector3 candidate = areaCenter + new Vector3(
                Random.Range(-areaSize.x / 2f, areaSize.x / 2f),
                0f,
                Random.Range(-areaSize.z / 2f, areaSize.z / 2f));

            bool farEnough = true;
            foreach (var placed in placedPositions)
            {
                if (Vector3.Distance(placed, candidate) < minDistanceFromOthers)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 指定座標付近でNavMesh上の有効な位置を探す。
    /// </summary>
    private bool TrySampleNavMesh(Vector3 desired, out Vector3 result)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = desired;
        return false;
    }

    // エリアをシーンビュー上で視覚的に確認できるようにする
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(areaCenter, new Vector3(areaSize.x, 0.1f, areaSize.z));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(areaCenter, new Vector3(areaSize.x, 0.1f, areaSize.z));
    }
}