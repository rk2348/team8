using UnityEngine;

/// <summary>
/// HUD用のCanvas(World Space)をプレイヤーの視点(カメラ)に追従させる。
/// 酔い防止のため、位置は緩やかに追従、回転はY軸(左右)のみ追従させるビルボード方式。
/// </summary>
public class HUDFollowViewpoint : MonoBehaviour
{
    [Header("追従対象")]
    [Tooltip("プレイヤーのカメラ(OVRCameraRig内のCenterEyeAnchorなど)")]
    public Transform viewpoint;

    [Header("配置オフセット")]
    [Tooltip("視点から見てどの位置にHUDを置くか(前方・左右・上下)")]
    public Vector3 offset = new Vector3(0f, -0.1f, 0.6f);

    [Header("追従の滑らかさ")]
    [Tooltip("位置の追従速度(大きいほどキビキビ動く)")]
    public float positionFollowSpeed = 8f;
    [Tooltip("回転の追従速度")]
    public float rotationFollowSpeed = 8f;

    [Header("回転の制限")]
    [Tooltip("trueの場合、上下の傾き(X軸)は無視してY軸(左右)だけ追従する")]
    public bool lockPitchAndRoll = true;

    void LateUpdate()
    {
        if (viewpoint == null) return;

        // 目標位置 = 視点の位置 + 視点のローカル方向を考慮したオフセット
        Vector3 targetPosition = viewpoint.position
            + viewpoint.right * offset.x
            + viewpoint.up * offset.y
            + viewpoint.forward * offset.z;

        // 位置を滑らかに追従
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionFollowSpeed * Time.deltaTime);

        // 回転を決定
        Quaternion targetRotation;
        if (lockPitchAndRoll)
        {
            // 左右(Y軸)の向きだけ追従し、上下の傾きは無視して酔いを防ぐ
            Vector3 flatForward = viewpoint.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward; // 真上/真下を向いた場合の保険
            targetRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }
        else
        {
            targetRotation = viewpoint.rotation;
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationFollowSpeed * Time.deltaTime);
    }
}