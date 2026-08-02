using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 憑依中のHUD管理。体力・空腹・危険度の3パネルをスライダーで表示し、
/// 憑依中はAボタンで表示/非表示をトグルできる。
/// ActionTextは憑依の有無を問わず、状況に応じた操作案内を表示する。
/// </summary>
public class PossessionController : MonoBehaviour
{
    [Header("パネル本体（憑依中だけ表示するオブジェクト）")]
    public GameObject healthPanel;
    public GameObject hungerPanel;
    public GameObject dangerPanel;

    [Header("体力パネルの中身")]
    public Slider healthSlider;

    [Header("空腹パネルの中身")]
    public Slider hungerSlider;

    [Header("危険度パネルの中身")]
    public Slider dangerSlider;
    [Tooltip("危険度が高いときにスライダーのFill色を変える場合に使用")]
    public Image dangerFillImage;
    public Color dangerLowColor = Color.white;
    public Color dangerHighColor = Color.red;
    [Range(0f, 1f)] public float dangerHighThreshold = 0.7f;

    [Header("操作案内テキスト(憑依前・憑依中で内容が変わる)")]
    [Tooltip("ActionTextを表示するパネル本体(nullなら常時表示扱い)")]
    public GameObject actionTextPanel;
    public TextMeshProUGUI actionText;
    [Tooltip("動物に近づいた際、憑依していないときに表示する文言(複数行可)")]
    [TextArea(2, 5)]
    public string possessPromptMessage = "Aボタンで憑依する";
    [Tooltip("憑依中に表示する文言(複数行可)")]
    [TextArea(2, 5)]
    public string possessingActionMessage = "Aボタン:HUD表示切替\nBボタン:憑依解除";

    // 憑依中にAボタンで切り替えられる、パネル全体の表示/非表示フラグ
    private bool panelsVisible = true;

    void Awake()
    {
        SetAllPanelsActive(false);
        HideActionText();
    }

    /// <summary>
    /// 憑依開始時に呼び出す。3パネルを表示し、初期値をセットする。
    /// ActionTextも憑依中用の文言に切り替える。
    /// </summary>
    public void ShowHUD(float currentHealth, float maxHealth, float currentHunger, float maxHunger, float dangerLevel)
    {
        panelsVisible = true;
        SetAllPanelsActive(true);

        UpdateHealth(currentHealth, maxHealth);
        UpdateHunger(currentHunger, maxHunger);
        UpdateDanger(dangerLevel);

        ShowActionText(possessingActionMessage);
    }

    /// <summary>
    /// 憑依解除時に呼び出す。3パネルを非表示にする。
    /// ActionTextも一旦非表示にする(接近判定はAnimalViewSwitch側が再度呼び出す)。
    /// </summary>
    public void HideHUD()
    {
        panelsVisible = true; // 次回憑依時は必ず表示された状態から始まるようにリセット
        SetAllPanelsActive(false);

        HideActionText();
    }

    /// <summary>
    /// Aボタンで呼び出す。表示中なら隠し、隠れているなら表示する。
    /// </summary>
    public void TogglePanels()
    {
        panelsVisible = !panelsVisible;
        SetAllPanelsActive(panelsVisible);
    }

    /// <summary>
    /// 憑依前、動物に接近したときに呼び出す。「Aボタンで憑依する」等の文言を表示する。
    /// </summary>
    public void ShowPossessPrompt()
    {
        ShowActionText(possessPromptMessage);
    }

    /// <summary>
    /// 任意の文言でActionTextを表示する。
    /// </summary>
    public void ShowActionText(string message)
    {
        if (actionTextPanel != null) actionTextPanel.SetActive(true);
        if (actionText != null) actionText.text = message;
    }

    /// <summary>
    /// ActionTextを非表示にする(接近判定が外れたときなどに呼び出す)。
    /// </summary>
    public void HideActionText()
    {
        if (actionTextPanel != null) actionTextPanel.SetActive(false);
    }

    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    public void UpdateHunger(float currentHunger, float maxHunger)
    {
        if (hungerSlider != null)
        {
            hungerSlider.maxValue = maxHunger;
            hungerSlider.value = currentHunger;
        }
    }

    public void UpdateDanger(float dangerLevel01)
    {
        float clamped = Mathf.Clamp01(dangerLevel01);

        if (dangerSlider != null)
        {
            dangerSlider.maxValue = 1f;
            dangerSlider.value = clamped;
        }
        if (dangerFillImage != null)
        {
            dangerFillImage.color = clamped >= dangerHighThreshold ? dangerHighColor : dangerLowColor;
        }
    }

    private void SetAllPanelsActive(bool active)
    {
        if (healthPanel != null) healthPanel.SetActive(active);
        if (hungerPanel != null) hungerPanel.SetActive(active);
        if (dangerPanel != null) dangerPanel.SetActive(active);
    }
}