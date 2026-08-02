using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoSceneLoader : MonoBehaviour
{
    [Header("シーン遷移設定")]
    [Tooltip("遷移先のシーン名を入力してください")]
    public string nextSceneName;

    [Tooltip("遷移するまでの待機時間（秒）")]
    public float delayTime = 10f;

    void Start()
    {
        // スクリプトが有効になった時（シーン開始時）にカウントダウンを開始
        StartCoroutine(LoadSceneAfterDelay());
    }

    private IEnumerator LoadSceneAfterDelay()
    {
        // 指定した秒数だけ処理を待機
        yield return new WaitForSeconds(delayTime);

        // シーン名が空でなければ遷移を実行
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("遷移先のシーン名が設定されていません。Inspectorから設定してください。");
        }
    }
}