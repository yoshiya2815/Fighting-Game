using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class CharaSelectManager : MonoBehaviour
{
    private bool isSelectingP2 = false; // 最初は1P選択モード

    [Header("UI設定")]
    public Text statusText;         // 「1P選択中」などを表示するテキスト
    public GameObject p1ConfirmBtn; // 1P確定ボタン
    public GameObject p2ConfirmBtn; // 2P確定ボタン
    public GameObject vsPanel;

    void Start()
    {
        // 最初は1P確定ボタンだけ出しておく
        p1ConfirmBtn.SetActive(true);
        p2ConfirmBtn.SetActive(false);
        UpdateStatusText();
    }

    // キャラクターボタン（Fighter_1など）が押された時
    public void SelectCharacter(int id)
    {
        if (!isSelectingP2)
        {
            GameSettings.Instance.selectedPlayerID = id;
            Debug.Log($"1Pがキャラ {id} を選択中");
        }
        else
        {
            GameSettings.Instance.selectedEnemyID = id;
            Debug.Log($"2Pがキャラ {id} を選択中");
        }
        UpdateStatusText();
    }

    // 1Pの「決定ボタン」が押された時
    public void OnConfirmP1()
    {
        if (GameSettings.Instance.selectedPlayerID == 0) return; // 未選択なら無視

        isSelectingP2 = true; // 2P選択モードへ切り替え
        p1ConfirmBtn.SetActive(false); // 1Pボタンを隠す
        p2ConfirmBtn.SetActive(true);  // 2Pボタンを出す
        UpdateStatusText();
    }

    // 2Pの「決定ボタン」が押された時
    public void OnConfirmP2()
    {
        if (GameSettings.Instance.selectedEnemyID == 0) return; // 未選択なら無視

        Debug.Log("両方の選択完了！バトル開始！");
        vsPanel.SetActive(true);

        StartCoroutine(VsAnimationRoutine());
    }

    private void UpdateStatusText()
    {
        if (statusText == null) return;

        if (!isSelectingP2)
            statusText.text = $"1P 選択中 (現在: {GameSettings.Instance.selectedPlayerID})";
        else
            statusText.text = $"2P 選択中 (現在: {GameSettings.Instance.selectedEnemyID})";
    }

    private IEnumerator VsAnimationRoutine()
    {
        // ここでエフェクトを鳴らしたり、アニメーションさせたりする
        // 例：P1が左からスライド、P2が右からスライドして真ん中で衝突！

        // 3秒待つ
        yield return new WaitForSeconds(3.0f);

        // バトルシーンへGO！
        SceneManager.LoadScene("BattleScene");
    }
}