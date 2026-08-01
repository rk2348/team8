using UnityEngine;
public class AnimalViewSwitch : MonoBehaviour
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

    [Header("HUDの設定")]
    public PossessionController hudController;
    [Tooltip("この動物の体力(仮。動物ごとにHealthコンポーネントがあるならそちらを参照)")]
    public float currentHealth = 100f;
    public float maxHealth = 100f;
    [Tooltip("このパネルに表示するミッション文言")]
    public string missionMessage = "動物を操作して探索しよう";
    [Tooltip("この動物が今いるエリア名")]
    public string locationName = "サバンナエリア";


    // 憑依解除時に元へ戻すための情報
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool wasRigidbodyKinematic;
    private Rigidbody animalRb;

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
        if (playerRig == null) return;

        if (!isPossessing)
        {
            // プレイヤーが設定されていない場合は処理をスキップ
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
        else
        {
            // 憑依中にBボタンが押されたら解除
            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                ReleaseAnimal();
            }
        }
    }
    private void PossessAnimal()
    {
        isPossessing = true;

        // 憑依前の状態を保存(解除時に元へ戻すため)
        originalParent = animalRoot.parent;
        originalPosition = animalRoot.position;
        originalRotation = animalRoot.rotation;

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
        animalRb = animalRoot.GetComponent<Rigidbody>();
        if (animalRb != null)
        {
            wasRigidbodyKinematic = animalRb.isKinematic;
            animalRb.isKinematic = true;
        }
        // CharacterControllerを再度有効化
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        Debug.Log("動物に乗り移り、実態と同期しました！");

        Debug.Log("動物に乗り移り、実態と同期しました！");

        // HUD表示
        if (hudController != null)
        {
            hudController.ShowHUD(missionMessage, locationName, currentHealth, maxHealth);
        }
    }

    private void ReleaseAnimal()
    {
        isPossessing = false;

        // 1. 動物の親子関係を解除し、元の親に戻す（見た目の位置関係は維持したまま外す）
        animalRoot.SetParent(originalParent, true);

        // 2. 動物を憑依前の位置・向きに戻す
        animalRoot.position = originalPosition;
        animalRoot.rotation = originalRotation;

        // 3. Rigidbodyのkinematic状態を元に戻す
        if (animalRb != null)
        {
            animalRb.isKinematic = wasRigidbodyKinematic;
        }

        Debug.Log("憑依を解除し、動物を元の位置に戻しました！");

        // HUD非表示
        if (hudController != null)
        {
            hudController.HideHUD();
        }
    }
}