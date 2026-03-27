using UnityEngine;
using TMPro;

public class ComboUIController : MonoBehaviour
{
    [Header("UI設定")]
    public TextMeshProUGUI comboText;

    [Header("演出設定")]
    public float popScale = 1.5f;
    public float shrinkSpeed = 10f;

    [Header("カラー設定 (カラーコード)")]
    public string colorLow = "#FFFFFF";    // 白 (1〜4ヒット)
    public string colorMid = "#FFFF00";    // 黄 (5〜9ヒット)
    public string colorHigh = "#FF0000";   // 赤 (10ヒット〜)
    public string colorSaved = "#00FFFF";  // ★追加：青/シアン (半減して耐えた時)

    private Vector3 baseScale;

    void Start()
    {
        if (comboText != null)
        {
            baseScale = comboText.transform.localScale;
            comboText.gameObject.SetActive(false);
        }
    }

    // ★ 改造：「isSaved」というスイッチ（初期値はfalse）を追加！
    public void UpdateCombo(int currentCombo, float bonusDamage, bool isSaved = false)
    {
        if (comboText == null) return;

        comboText.gameObject.SetActive(true);

        string hexColor = colorLow;

        // ★ 色の決定ロジック
        if (isSaved)
        {
            // 半減して耐えた時（isSavedがtrue）は強制的に青色にする！
            hexColor = colorSaved;

            // （おまけ）文字も「COMBO SAVED!」に変えるとさらにカッコいいです！
            comboText.text = $"<color={hexColor}>{currentCombo} HIT!!</color>\n<size=50%>COMBO SAVED!</size>";
        }
        else
        {
            // 通常のヒット時は今まで通りコンボ数で色を変える
            if (currentCombo >= 10) hexColor = colorHigh;
            else if (currentCombo >= 5) hexColor = colorMid;

            comboText.text = $"<color={hexColor}>{currentCombo} HIT!!</color>\n<size=50%>Bonus +{bonusDamage:F1}</size>";
        }

        // ポップアップ演出（サイズを1.5倍にする）
        comboText.transform.localScale = baseScale * popScale;
    }

    public void ResetCombo()
    {
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (comboText != null && comboText.gameObject.activeSelf)
        {
            comboText.transform.localScale = Vector3.Lerp(
                comboText.transform.localScale,
                baseScale,
                shrinkSpeed * Time.deltaTime
            );
        }
    }
}