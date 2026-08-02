using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 憑依中のHUD管理。
/// 憑依開始時はまずミッションパネルを表示し、一定時間後に自動で
/// 体力・空腹・危険度の3パネル(ステータス)表示へ切り替わる。
/// Xボタンでいつでもミッションを見返せ(その間ステータスは隠れる)、
/// 同じXボタンでステータス表示に戻る。
/// Aボタンはステータス全体(3パネル)の表示/非表示を切り替える。
/// ミッション達成時は完了パネル→スコアパネル(合計のみ)の順で自動表示する。
///
/// パネルの表示/非表示は全てCanvasGroupのalphaをフェードさせる形の動的な切り替えになっている
/// (瞬間的なSetActiveのオン/オフではなく、滑らかにフェードイン/フェードアウトする)。
/// </summary>
public class PossessionController : MonoBehaviour
{
    [Header("パネル切り替えの演出設定")]
    [Tooltip("パネルが表示/非表示に切り替わる際のフェード時間(秒)")]
    public float panelFadeDuration = 0.25f;

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
    [Tooltip("合計スコアの表示")]
    public TextMeshProUGUI totalScoreText;
    [Tooltip("表示フォーマット。{0}にスコアの数値が入る")]
    public string totalScoreFormat = "{0}";

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

    // パネルごとのフェード中コルーチンを管理(同じパネルに対して複数のフェードが同時に走らないようにするため)
    private readonly Dictionary<GameObject, Coroutine> fadeCoroutines = new Dictionary<GameObject, Coroutine>();

    void Awake()
    {
        SetStatPanelsActive(false, true);
        SetMissionPanelActive(false, true);
        SetMissionCompletePanelActive(false, true);
        SetScorePanelActive(false, true);
        HideActionPanels(true);
    }

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
    }

    private IEnumerator AutoSwitchToStats()
    {
        yield return new WaitForSeconds(missionDisplayDuration);
        missionViewActive = false;
        RefreshVisibility();
        autoSwitchCoroutine = null;
    }

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
    /// 「ミッション完了」パネル→一定時間後に自動で合計スコアパネルへ切り替える。
    /// </summary>
    public void ShowMissionComplete(int totalScore)
    {
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

        if (totalScoreText != null) totalScoreText.text = string.Format(totalScoreFormat, totalScore);

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
        SetPanelActive(possessPromptPanel, true);
        SetPanelActive(possessingActionPanel, false);
    }

    public void ShowPossessingActionPanel()
    {
        SetPanelActive(possessPromptPanel, false);
        SetPanelActive(possessingActionPanel, true);
    }

    public void HideActionPanels(bool instant = false)
    {
        SetPanelActive(possessPromptPanel, false, instant);
        SetPanelActive(possessingActionPanel, false, instant);
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

    private void SetStatPanelsActive(bool active, bool instant = false)
    {
        SetPanelActive(healthPanel, active, instant);
        SetPanelActive(hungerPanel, active, instant);
        SetPanelActive(dangerPanel, active, instant);
    }

    private void SetMissionPanelActive(bool active, bool instant = false)
    {
        SetPanelActive(missionPanel, active, instant);
    }

    private void SetMissionCompletePanelActive(bool active, bool instant = false)
    {
        SetPanelActive(missionCompletePanel, active, instant);
    }

    private void SetScorePanelActive(bool active, bool instant = false)
    {
        SetPanelActive(scorePanel, active, instant);
    }

    /// <summary>
    /// パネルの表示/非表示を、瞬間切り替えではなくCanvasGroupのalphaフェードで動的に行う。
    /// 表示時: SetActive(true) → alphaを0から1へフェードイン
    /// 非表示時: alphaを現在値から0へフェードアウト → SetActive(false)
    /// instant=trueの場合は従来通り瞬時に切り替える(初期化時など、フェード不要な場面用)。
    /// </summary>
    private void SetPanelActive(GameObject panel, bool active, bool instant = false)
    {
        if (panel == null) return;

        // 同じパネルに対して既にフェード中のコルーチンがあれば止めてから新しいフェードを開始する
        if (fadeCoroutines.TryGetValue(panel, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            fadeCoroutines[panel] = null;
        }

        if (instant || panelFadeDuration <= 0f)
        {
            CanvasGroup instantGroup = GetOrAddCanvasGroup(panel);
            instantGroup.alpha = active ? 1f : 0f;
            instantGroup.interactable = active;
            instantGroup.blocksRaycasts = active;
            panel.SetActive(active);
            return;
        }

        Coroutine c = StartCoroutine(FadePanel(panel, active));
        fadeCoroutines[panel] = c;
    }

    private IEnumerator FadePanel(GameObject panel, bool active)
    {
        CanvasGroup group = GetOrAddCanvasGroup(panel);

        if (active)
        {
            panel.SetActive(true);
        }

        float startAlpha = group.alpha;
        float endAlpha = active ? 1f : 0f;
        float elapsed = 0f;

        // 表示中の当たり判定はフェード開始時点で切り替えておく
        // (フェードイン中は既に操作できてよく、フェードアウト中は早めに操作を受け付けなくする)
        group.interactable = active;
        group.blocksRaycasts = active;

        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / panelFadeDuration);
            group.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        group.alpha = endAlpha;

        if (!active)
        {
            panel.SetActive(false);
        }

        fadeCoroutines[panel] = null;
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject panel)
    {
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = panel.AddComponent<CanvasGroup>();
        }
        return group;
    }
}