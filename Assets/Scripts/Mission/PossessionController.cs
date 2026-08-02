using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 憑依中のHUD管理。
/// 憑依開始時はまずミッションパネルを表示し、一定時間後に自動で
/// 体力・空腹・危険度の3パネル(ステータス)表示へ切り替わる。
/// Xボタンでいつでもミッションを見返せ(その間ステータスは隠れる)、
/// 同じXボタンでステータス表示に戻る。
/// Aボタンはステータス全体(3パネル)の表示/非表示を切り替える。
/// ミッション達成時は完了パネル→スコアパネルの順で自動表示する。
/// テキストは使わず、パネル(GameObject)の表示/非表示とスライダーの数値のみで表現する。
/// </summary>
public class PossessionController : MonoBehaviour
{
    [Header("ミッションパネル")]
    public GameObject missionPanel;
    [Tooltip("憑依開始時、ミッションパネルを表示し続ける秒数(経過後は自動でステータス表示に切り替わる)")]
    public float missionDisplayDuration = 4f;

    [Header("ミッション完了パネル")]
    public GameObject missionCompletePanel;
    [Tooltip("完了パネルを表示し続ける秒数(経過後は自動でスコアパネルへ切り替わる)")]
    public float missionCompleteDisplayDuration = 2.5f;

    [Header("スコアパネル")]
    public GameObject scorePanel;
    [Tooltip("スコアをスライダーで表現する場合に使用(任意)")]
    public Slider scoreSlider;
    [Tooltip("スコアスライダーの最大値の目安")]
    public float maxScoreForSlider = 1000f;

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

    [Header("操作案内パネル(憑依前・憑依中で表示を切り替え)")]
    [Tooltip("憑依前、接近時に表示するパネル")]
    public GameObject possessPromptPanel;
    [Tooltip("憑依中に表示するパネル(操作案内)")]
    public GameObject possessingActionPanel;

    private bool panelsVisible = true;
    private bool missionViewActive = false;
    private Coroutine autoSwitchCoroutine;
    private Coroutine missionCompleteCoroutine;

    void Awake()
    {
        SetStatPanelsActive(false);
        SetMissionPanelActive(false);
        SetMissionCompletePanelActive(false);
        SetScorePanelActive(false);
        HideActionPanels();
    }

    /// <summary>
    /// 憑依開始時に呼び出す。まずミッションパネルを表示し、
    /// missionDisplayDuration秒後に自動でステータス3パネルへ切り替える。
    /// </summary>
    public void ShowHUD(float currentHealth, float maxHealth, float currentHunger, float maxHunger, float dangerLevel)
    {
        UpdateHealth(currentHealth, maxHealth);
        UpdateHunger(currentHunger, maxHunger);
        UpdateDanger(dangerLevel);

        panelsVisible = true;

        SetMissionCompletePanelActive(false);
        SetScorePanelActive(false);

        missionViewActive = true;
        RefreshVisibility();

        ShowPossessingActionPanel();

        if (autoSwitchCoroutine != null) StopCoroutine(autoSwitchCoroutine);
        autoSwitchCoroutine = StartCoroutine(AutoSwitchToStats());

        Debug.Log("PossessionController: ShowHUD呼び出し完了。ミッションパネルを表示しました。");
    }

    private IEnumerator AutoSwitchToStats()
    {
        yield return new WaitForSeconds(missionDisplayDuration);
        missionViewActive = false;
        RefreshVisibility();
        autoSwitchCoroutine = null;
    }

    /// <summary>
    /// 憑依解除時に呼び出す。全パネルを非表示にする。
    /// </summary>
    public void HideHUD()
    {
        if (autoSwitchCoroutine != null)
        {
            StopCoroutine(autoSwitchCoroutine);
            autoSwitchCoroutine = null;
        }
        if (missionCompleteCoroutine != null)
        {
            StopCoroutine(missionCompleteCoroutine);
            missionCompleteCoroutine = null;
        }

        panelsVisible = true;
        missionViewActive = false;

        SetStatPanelsActive(false);
        SetMissionPanelActive(false);
        SetMissionCompletePanelActive(false);
        SetScorePanelActive(false);
        HideActionPanels();
    }

    public void TogglePanels()
    {
        panelsVisible = !panelsVisible;
        RefreshVisibility();
    }

    public void ToggleMissionView()
    {
        if (autoSwitchCoroutine != null)
        {
            StopCoroutine(autoSwitchCoroutine);
            autoSwitchCoroutine = null;
        }

        missionViewActive = !missionViewActive;
        RefreshVisibility();
    }

    /// <summary>
    /// ミッション達成時に呼び出す。ステータス・ミッションパネルを隠し、
    /// 「ミッション完了」パネル→一定時間後に自動でスコアパネルへ切り替える。
    /// </summary>
    public void ShowMissionComplete(int score)
    {
        Debug.Log($"PossessionController: ShowMissionCompleteが呼ばれました。score={score}");

        if (autoSwitchCoroutine != null)
        {
            StopCoroutine(autoSwitchCoroutine);
            autoSwitchCoroutine = null;
        }

        missionViewActive = false;
        SetStatPanelsActive(false);
        SetMissionPanelActive(false);
        HideActionPanels();

        SetMissionCompletePanelActive(true);
        SetScorePanelActive(false);

        if (scoreSlider != null)
        {
            scoreSlider.maxValue = maxScoreForSlider;
            scoreSlider.value = score;
        }

        if (missionCompleteCoroutine != null) StopCoroutine(missionCompleteCoroutine);
        missionCompleteCoroutine = StartCoroutine(AutoSwitchToScore());
    }

    private IEnumerator AutoSwitchToScore()
    {
        yield return new WaitForSeconds(missionCompleteDisplayDuration);
        SetMissionCompletePanelActive(false);
        SetScorePanelActive(true);
        missionCompleteCoroutine = null;
    }

    private void RefreshVisibility()
    {
        SetMissionPanelActive(missionViewActive);
        SetStatPanelsActive(!missionViewActive && panelsVisible);
    }

    public void ShowPossessPrompt()
    {
        if (possessPromptPanel != null) possessPromptPanel.SetActive(true);
        if (possessingActionPanel != null) possessingActionPanel.SetActive(false);
    }

    public void ShowPossessingActionPanel()
    {
        if (possessPromptPanel != null) possessPromptPanel.SetActive(false);
        if (possessingActionPanel != null) possessingActionPanel.SetActive(true);
    }

    public void HideActionPanels()
    {
        if (possessPromptPanel != null) possessPromptPanel.SetActive(false);
        if (possessingActionPanel != null) possessingActionPanel.SetActive(false);
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

    private void SetStatPanelsActive(bool active)
    {
        if (healthPanel != null) healthPanel.SetActive(active);
        if (hungerPanel != null) hungerPanel.SetActive(active);
        if (dangerPanel != null) dangerPanel.SetActive(active);
    }

    private void SetMissionPanelActive(bool active)
    {
        if (missionPanel != null) missionPanel.SetActive(active);
    }

    private void SetMissionCompletePanelActive(bool active)
    {
        if (missionCompletePanel != null) missionCompletePanel.SetActive(active);
    }

    private void SetScorePanelActive(bool active)
    {
        if (scorePanel != null) scorePanel.SetActive(active);
    }
}