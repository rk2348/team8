using UnityEngine;
using System.Collections;

public class CloudsGenerator : MonoBehaviour
{
    public float Density = 2;
    public GameObject[] Prefabs = new GameObject[0];
    public Vector3 StartPos = Vector3.zero;

    [Tooltip("Densityが 2 のときの基準となる終了位置")]
    public Vector3 EndPos = new Vector3(100, 0, 100);
    public Texture2D HeightMap;

    [Header("ゆらゆら設定")]
    public float swaySpeed = 0.5f;
    public float swayAmount = 10f;

    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.position;
        int cnt = 0;

        // --- 追加：Densityに合わせて範囲を自動で広げる ---
        // Density = 2 の時を基準（1倍）としてスケールを計算
        float scale = Density / 2f;

        // StartPosを起点にして、本来の範囲（EndPos - StartPos）にスケールを掛けて実際の終了位置を算出
        Vector3 actualEndPos = StartPos + (EndPos - StartPos) * scale;

        Vector3 curPos = StartPos;

        // 元の EndPos の代わりに actualEndPos を使うように変更
        while (curPos.z < actualEndPos.z)
        {
            curPos.z += Random.Range(Density / 2, Density * 1.5f);
            curPos.x = StartPos.x;
            while (curPos.x < actualEndPos.x)
            {
                curPos.x += Random.Range(Density / 5, Density * 5);

                // 画像のどの位置を読み取るか、全体の何％の位置にいるかで計算（StartPosが0以外でもずれないように改善）
                float percentX = (curPos.x - StartPos.x) / (actualEndPos.x - StartPos.x);
                float percentZ = (curPos.z - StartPos.z) / (actualEndPos.z - StartPos.z);

                int x = (int)(HeightMap.width * percentX);
                int y = (int)(HeightMap.height * percentZ);

                // 計算の誤差で画像サイズを超えてエラーにならないよう、安全対策を追加
                x = Mathf.Clamp(x, 0, HeightMap.width - 1);
                y = Mathf.Clamp(y, 0, HeightMap.height - 1);

                if (HeightMap.GetPixel(x, y).g < 0.75f)
                {
                    continue;
                }

                float height = HeightMap.GetPixel(x, y).g * 46 - 30;
                float width = HeightMap.GetPixel(x, y).b * 40;
                height *= 5;
                width *= 5;
                curPos.y = -800;

                int id = Random.Range(0, Prefabs.Length);
                cnt++;
                GameObject placed = (GameObject)Instantiate(Prefabs[id], curPos, Quaternion.identity);

                placed.transform.localScale = new Vector3(width, height, width);
                placed.transform.parent = transform;
            }
        }
        Debug.Log("生成された雲の数: " + cnt);
    }

    void Update()
    {
        float offsetX = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        float offsetZ = Mathf.Cos(Time.time * swaySpeed * 0.8f) * swayAmount;
        transform.position = initialPosition + new Vector3(offsetX, 0, offsetZ);
    }
}