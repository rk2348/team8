using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 群れ行動を担当するコンポーネント。
/// AnimalIdleBehaviorが次の徘徊先をランダムに決める際、この群れの中心・仲間との距離を
/// 考慮して目的地を補正する(結合:群れの中心から離れすぎたら引き戻す / 分離:仲間に近すぎたら離す)。
/// メンバーの設定はAnimalAreaSpawnerなど外部のスポナーがSetHerdMembers()経由で行う想定。
/// </summary>
public class HerdBehavior : MonoBehaviour
{
    [Header("群れパラメータ(動物ごとにチューニングしてください)")]
    [Tooltip("群れの中心とみなす範囲の半径。これを超えた徘徊先は中心方向へ補正される")]
    public float herdRadius = 6f;
    [Tooltip("群れの中心から離れすぎている場合、中心方向へ引き戻す強さ")]
    public float cohesionWeight = 1f;
    [Tooltip("この距離より仲間に近づいた場合、離れる方向へ補正する")]
    public float separationDistance = 2f;
    [Tooltip("分離(仲間から離れる)補正の強さ")]
    public float separationWeight = 1.5f;

    private List<Transform> herdMembers = new List<Transform>();

    /// <summary>
    /// 外部(スポナーなど)から群れメンバー(自分自身を含むリスト)を設定する。
    /// </summary>
    public void SetHerdMembers(List<Transform> members)
    {
        herdMembers = members;
    }

    /// <summary>
    /// 現在の群れメンバー数(自分を含む)。
    /// </summary>
    public int HerdSize => herdMembers != null ? herdMembers.Count : 0;

    /// <summary>
    /// 群れの中心(メンバー全員の平均位置)を返す。メンバーが未設定なら自身の位置を返す。
    /// </summary>
    public Vector3 GetHerdCenter()
    {
        if (herdMembers == null || herdMembers.Count == 0) return transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var m in herdMembers)
        {
            if (m == null) continue;
            sum += m.position;
            count++;
        }
        return count > 0 ? sum / count : transform.position;
    }

    /// <summary>
    /// AnimalIdleBehaviorがランダムに選んだ徘徊先(candidate)を、群れの結合・分離を考慮して補正する。
    /// </summary>
    public Vector3 GetHerdAdjustedDestination(Vector3 candidate)
    {
        Vector3 center = GetHerdCenter();

        // 結合: 群れの中心からherdRadiusを超えて離れようとしていたら、中心方向へ引き戻す
        Vector3 toCenter = center - candidate;
        float distFromCenter = toCenter.magnitude;
        if (distFromCenter > herdRadius)
        {
            float pull = (distFromCenter - herdRadius) * cohesionWeight;
            candidate += toCenter.normalized * pull;
        }

        // 分離: 近すぎる仲間がいたら、そこから離れる方向へ補正する
        if (herdMembers != null)
        {
            foreach (var member in herdMembers)
            {
                if (member == null || member == transform) continue;

                Vector3 away = candidate - member.position;
                float dist = away.magnitude;
                if (dist < separationDistance && dist > 0.001f)
                {
                    float push = (separationDistance - dist) * separationWeight;
                    candidate += away.normalized * push;
                }
            }
        }

        return candidate;
    }
}