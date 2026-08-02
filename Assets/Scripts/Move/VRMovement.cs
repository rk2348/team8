using UnityEngine;
[RequireComponent(typeof(CharacterController))]
public class VRMovement : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("通常時の移動速度")]
    public float speed = 3.0f;
    [Tooltip("左人差し指トリガーを押している間のダッシュ速度")]
    public float dashSpeed = 7.0f;
    [Tooltip("トリガーをどこまで押し込んだらダッシュとみなすか(0?1)")]
    [Range(0f, 1f)] public float dashTriggerThreshold = 0.1f;

    [Header("視点回転設定(右スティック/スムーズターン)")]
    [Tooltip("右スティックの左右入力による回転速度(度/秒)")]
    public float turnSpeed = 90f;
    [Tooltip("この値未満のスティック入力は無視する(誤入力・スティックのニュートラルのブレ対策)")]
    [Range(0f, 0.5f)] public float turnDeadzone = 0.15f;

    [Header("参照")]
    [Tooltip("HMDのカメラ（CenterEyeAnchorなど）をアタッチ")]
    public Transform head;
    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private readonly float gravity = -9.81f;
    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }
    void Update()
    {
        Vector2 input = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
        Vector3 forward = head.forward;
        Vector3 right = head.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        Vector3 moveDirection = forward * input.y + right * input.x;
        // 左人差し指トリガーの押し込み量を取得し、閾値を超えていればダッシュ速度を使う
        float triggerValue = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        bool isDashing = triggerValue >= dashTriggerThreshold;
        float currentSpeed = isDashing ? dashSpeed : speed;
        Vector3 finalMovement = (moveDirection * currentSpeed) + (Vector3.up * verticalVelocity);
        characterController.Move(finalMovement * Time.deltaTime);

        HandleSmoothTurn();
    }

    /// <summary>
    /// 右スティックの左右入力(RThumbstick.x)でリグ全体をY軸回転させる、いわゆる「スムーズターン」。
    /// ヘッド(head)自体は回転させず、プレイヤーリグ(このスクリプトがついているオブジェクト)を回すことで
    /// HMDの実際の向きと組み合わさって、体ごと向きが変わったように見える仕組み。
    /// </summary>
    private void HandleSmoothTurn()
    {
        Vector2 rightStickInput = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);

        if (Mathf.Abs(rightStickInput.x) < turnDeadzone)
        {
            return;
        }

        float rotationAmount = rightStickInput.x * turnSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationAmount, Space.World);
    }
}