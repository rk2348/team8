using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 同じ種族の個体同士で群れ(ハード)を形成する。
/// 群れの中心へ緩やかに引き寄せられ(結合)、近すぎる仲間とはぶつからないよう距離を取る(分離)。
/// AnimalIdleBehaviorの徘徊先(Walk先)にバイアスをかける形で動作するため、単体では何もしない。
/// </summary>
public class HerdBehavior : MonoBehaviour
{
    [Header("群れの設定")]
    [Tooltip("この距離以内にいる同種を群れの仲間とみなす")]
    public float herdRadius = 15f;
    [Tooltip("群れの中心へ引き寄せられる強さ(0?1、大きいほど群れがまとまる)")]
    [Range(0f, 1f)] public float cohesionWeight = 0.4f;
    [Tooltip("近すぎる仲間から離れようとする強さ(0?1)")]
    [Range(0f, 1f)] public float separationWeight = 0.6f;
    [Tooltip("この距離より仲間と近づくと分離行動が働く")]
    public float separationDistance = 3f;
    [Tooltip("群れの中心からこの距離以上は離れないようにする(はぐれ防止の上限)")]
    public float maxStrayDistance = 25f;

    private AnimalIdentity selfIdentity;
    private static readonly List<AnimalIdentity> neighborCache = new List<AnimalIdentity>();

    void Awake()
    {
        selfIdentity = GetComponent<AnimalIdentity>();
    }

    /// <summary>
    /// AnimalIdleBehaviorが徘徊先を決める際に呼び出す。
    /// ランダムに決めた候補地点(rawDestination)に、群れの結合・分離を加味した補正をかけて返す。
    /// 近くに仲間がいなければ、そのまま元の候補地点を返す(=群れがいない場合は通常の徘徊と同じ)。
    /// </summary>
    public Vector3 GetHerdAdjustedDestination(Vector3 rawDestination)
    {
        if (selfIdentity == null) return rawDestination;

        int neighborCount = GatherNeighbors(out Vector3 herdCenter, out Vector3 separationVector);
        if (neighborCount == 0) return rawDestination;

        // 結合:群れの中心方向へ、ランダム候補地点を引き寄せる
        Vector3 cohesionTarget = Vector3.Lerp(rawDestination, herdCenter, cohesionWeight);

        // 分離:近すぎる仲間がいれば、その反対方向へずらす
        Vector3 adjusted = cohesionTarget + separationVector * separationWeight;

        // 群れの中心から離れすぎないよう上限をかける(はぐれ防止)
        Vector3 fromCenter = adjusted - herdCenter;
        if (fromCenter.magnitude > maxStrayDistance)
        {
            adjusted = herdCenter + fromCenter.normalized * maxStrayDistance;
        }

        return adjusted;
    }

    private int GatherNeighbors(out Vector3 herdCenter, out Vector3 separationVector)
    {
        AnimalIdentity.CollectNearby(selfIdentity.species, transform.position, herdRadius, selfIdentity, neighborCache);

        herdCenter = transform.position;
        separationVector = Vector3.zero;
        if (neighborCache.Count == 0) return 0;

        Vector3 sumPositions = Vector3.zero;
        Vector3 sumSeparation = Vector3.zero;

        foreach (var neighbor in neighborCache)
        {
            sumPositions += neighbor.transform.position;

            float dist = Vector3.Distance(transform.position, neighbor.transform.position);
            if (dist < separationDistance && dist > 0.01f)
            {
                Vector3 away = (transform.position - neighbor.transform.position).normalized;
                sumSeparation += away * (separationDistance - dist); // 近いほど強く反発
            }
        }

        herdCenter = sumPositions / neighborCache.Count;
        separationVector = sumSeparation;
        return neighborCache.Count;
    }
}