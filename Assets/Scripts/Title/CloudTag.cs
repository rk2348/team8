using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 指定したタグ（デフォルト: "CloudTag"）を持つオブジェクトに触れたらシーンを切り替える。
/// このスクリプトは「触れられる側（雲オブジェクトなど）」ではなく、
/// 「触れてくる側（プレイヤーの手・コントローラー・体など）」にアタッチして使うことを想定しています。
/// Colliderには IsTrigger をONにしたものを使ってください。
/// </summary>
public class CloudTag : MonoBehaviour
{
    [Header("判定設定")]
    [Tooltip("このタグを持つオブジェクトに触れたらシーン遷移する")]
    public string targetTag = "CloudTag";

    [Header("遷移先シーン設定")]
    [Tooltip("チェックを入れるとシーン名で指定、外すとビルド設定のシーンIndexで指定")]
    public bool useSceneName = true;
    [Tooltip("遷移先シーン名（File > Build Settings に追加されている必要があります）")]
    public string sceneName;
    [Tooltip("遷移先シーンのビルドIndex（useSceneNameがfalseの場合に使用）")]
    public int sceneBuildIndex;

    [Header("オプション")]
    [Tooltip("一度触れたら以降は反応しないようにする（多重発火防止）")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        TryHandleTouch(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHandleTouch(collision.gameObject);
    }

    private void TryHandleTouch(GameObject other)
    {
        if (triggerOnlyOnce && hasTriggered)
        {
            return;
        }

        if (!other.CompareTag(targetTag))
        {
            return;
        }

        hasTriggered = true;
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (useSceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("CloudTagSceneChanger: sceneNameが設定されていません。Inspectorで遷移先シーン名を指定してください。");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneBuildIndex);
        }
    }
}