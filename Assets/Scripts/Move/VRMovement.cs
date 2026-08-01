using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRMovement : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("移動速度")]
    public float speed = 3.0f;

    [Header("参照")]
    [Tooltip("HMDのカメラ（CenterEyeAnchorなど）をアタッチ")]
    public Transform head;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private readonly float gravity = -9.81f;

    void Start()
    {
        // アタッチされているCharacterControllerを取得
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 左スティックの入力を取得 (X: 左右, Y: 前後)
        Vector2 input = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);

        // カメラ（頭）の向いている方向を基準に移動方向を計算
        Vector3 forward = head.forward;
        Vector3 right = head.right;

        // Y軸（上下）の傾きを無視して水平移動のみにする
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // 入力値と方向を掛け合わせて移動ベクトルを作成
        Vector3 moveDirection = forward * input.y + right * input.x;

        // 重力の処理（宙に浮かないようにする）
        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f; // 接地を安定させるための微小な下向きの力
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // 最終的な移動量を計算して適用
        Vector3 finalMovement = (moveDirection * speed) + (Vector3.up * verticalVelocity);
        characterController.Move(finalMovement * Time.deltaTime);
    }
}