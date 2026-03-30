using UnityEngine;
using TMPro;

/// <summary>
/// バトル中のコンボ数表示と、それに伴うスケール・カラーアニメーションを制御するViewコンポーネント。
/// ヒット数や特殊な防御メカニクス（コンボ継続）に応じた視覚的なフィードバックを行い、プレイヤーの爽快感（UX）を向上させる。
/// </summary>
public class ComboUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI comboText;

    [Header("Animation Settings")]
    [SerializeField] private float popScale = 1.5f;
    [SerializeField] private float shrinkSpeed = 10f;

    [Header("Color Palettes (Hex)")]
    [SerializeField] private string colorLow = "#FFFFFF";    // 1〜4 hits
    [SerializeField] private string colorMid = "#FFFF00";    // 5〜9 hits
    [SerializeField] private string colorHigh = "#FF0000";   // 10+ hits
    [SerializeField] private string colorSaved = "#00FFFF";  // ダメージ軽減/コンボセーブ発動時

    private Vector3 baseScale;

    private void Start()
    {
        if (comboText != null)
        {
            baseScale = comboText.transform.localScale;
            comboText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// コンボ数を更新し、UIのポップアップアニメーションとカラーコードの再計算を実行する。
    /// </summary>
    /// <param name="currentCombo">現在のヒット数</param>
    /// <param name="bonusDamage">コンボによるボーナスダメージ量</param>
    /// <param name="isSaved">キャラ固有能力等でコンボが途切れず半減維持された特殊状態か</param>
    public void UpdateCombo(int currentCombo, float bonusDamage, bool isSaved = false)
    {
        if (comboText == null) return;

        comboText.gameObject.SetActive(true);
        string hexColor = colorLow;

        // 状態とヒット数に基づくカラー・テキストの動的変更
        if (isSaved)
        {
            hexColor = colorSaved;
            comboText.text = $"<color={hexColor}>{currentCombo} HIT!!</color>\n<size=50%>COMBO SAVED!</size>";
        }
        else
        {
            if (currentCombo >= 10) hexColor = colorHigh;
            else if (currentCombo >= 5) hexColor = colorMid;

            comboText.text = $"<color={hexColor}>{currentCombo} HIT!!</color>\n<size=50%>Bonus +{bonusDamage:F1}</size>";
        }

        // ポップアップ演出のトリガー（Update内で徐々に元のサイズへ戻る）
        comboText.transform.localScale = baseScale * popScale;
    }

    /// <summary>
    /// コンボ表示をリセットし、UIを非表示状態にする。
    /// </summary>
    public void ResetCombo()
    {
        if (comboText != null) comboText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        // ポップアップしたテキストを滑らかに元のサイズへ縮小させるアニメーション処理
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