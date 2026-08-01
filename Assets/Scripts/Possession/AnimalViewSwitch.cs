using UnityEngine;

public class AnimalViewSwitchDistance : MonoBehaviour
{
    [Header("動物の設定")]
    [Tooltip("動物のルート（大元）のオブジェクト")]
    public Transform animalRoot;

    [Tooltip("動物の視点となる場所（頭など）のTransform")]
    public Transform animalViewpoint;

    [Header("距離の設定")]
    [Tooltip("Aボタンが反応する距離（メートル）")]
    public float interactionDistance = 3.0f;

    [Header("プレイヤーの設定")]
    [Tooltip("プレイヤーのルートオブジェクト（VRMovementがついているオブジェクト）")]
    public Transform playerRig;

    [Tooltip("プレイヤーのCharacterController")]
    public CharacterController playerController;

    // 既に動物に乗り移っているかの判定
    private bool isPossessing = false;

    void Start()
    {
        // animalRootが未設定の場合、このスクリプトがついているオブジェクトを自動設定
        if (animalRoot == null)
        {
            animalRoot = transform;
        }
    }

    void Update()
    {
        // プレイヤーが設定されていない、または既に乗り移っている場合は処理をスキップ
        if (playerRig == null || isPossessing) return;

        // 動物とプレイヤー間の距離を計算
        float distance = Vector3.Distance(animalRoot.position, playerRig.position);

        if (distance <= interactionDistance)
        {
            // Aボタンが押されたら
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                PossessAnimal();
            }
        }
    }

    private void PossessAnimal()
    {
        isPossessing = true;

        // CharacterControllerが有効なままだと位置の直接変更がブロックされるため一時無効化
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // 1. プレイヤーを動物の視点の位置・向きに合わせる
        playerRig.position = animalViewpoint.position;
        playerRig.rotation = animalViewpoint.rotation;

        // 2. 動物のモデルをプレイヤーの子オブジェクトにする（これで一緒に動くようになります）
        // 第二引数をtrueにすることで、見た目の位置関係を維持したまま親子化します
        animalRoot.SetParent(playerRig, true);

        // ※もし動物にRigidbody（物理演算）がついていて移動の邪魔になる場合は無効化する
        Rigidbody animalRb = animalRoot.GetComponent<Rigidbody>();
        if (animalRb != null)
        {
            animalRb.isKinematic = true;
        }

        // CharacterControllerを再度有効化
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        Debug.Log("動物に乗り移り、実態と同期しました！");
    }
}[
