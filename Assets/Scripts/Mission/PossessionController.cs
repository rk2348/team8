using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PossessionController : MonoBehaviour
{
    [Header("パネル本体（憑依中だけ表示するオブジェクト）")]
    public GameObject missionPanel;
    public GameObject timeOfDayPanel;
    public GameObject locationPanel;
    public GameObject healthPanel;

    [Header("ミッションパネルの中身")]
    public TextMeshProUGUI missionText;

    [Header("時間帯パネルの中身")]
    public TextMeshProUGUI timeOfDayText;

    [Header("場所パネルの中身")]
    public TextMeshProUGUI locationText;

    [Header("体力ゲージパネルの中身")]
    //public Slider healthSlider;
    public TextMeshProUGUI healthValueText;

    void Awake()
    {
        // 初期状態は非表示にしておく
        SetAllPanelsActive(false);
    }

    /// <summary>
    /// 憑依開始時に呼び出す。4パネルを表示し、初期値をセットする。
    /// </summary>
    public void ShowHUD(string missionMessage, string locationName, float currentHealth, float maxHealth)
    {
        SetAllPanelsActive(true);

        if (missionText != null) missionText.text = missionMessage;
        if (locationText != null) locationText.text = locationName;

        //UpdateHealth(currentHealth, maxHealth);
    }

    /// <summary>
    /// 憑依解除時に呼び出す。4パネルを非表示にする。
    /// </summary>
    public void HideHUD()
    {
        SetAllPanelsActive(false);
    }

    /// <summary>
    /// 体力ゲージを更新する（ダメージや回復のたびに呼び出す）
    /// </summary>
    /*public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        if (healthValueText != null)
        {
            healthValueText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
    }*/

    /// <summary>
    /// ミッション内容だけを差し替えたいとき用
    /// </summary>
    public void UpdateMission(string missionMessage)
    {
        if (missionText != null) missionText.text = missionMessage;
    }

    /// <summary>
    /// 現在地表示だけを差し替えたいとき用
    /// </summary>
    public void UpdateLocation(string locationName)
    {
        if (locationText != null) locationText.text = locationName;
    }

    private void SetAllPanelsActive(bool active)
    {
        if (missionPanel != null) missionPanel.SetActive(active);
        if (timeOfDayPanel != null) timeOfDayPanel.SetActive(active);
        if (locationPanel != null) locationPanel.SetActive(active);
        if (healthPanel != null) healthPanel.SetActive(active);
    }
}