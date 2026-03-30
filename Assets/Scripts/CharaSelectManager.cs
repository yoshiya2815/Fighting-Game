using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// キャラクター選択画面（アウトゲーム）の進行状態とUIを管理するコントローラー。
/// 1Pと2Pの選択フェーズを制御し、バトルシーンへの非同期遷移を行う。
/// </summary>
public class CharaSelectManager : MonoBehaviour
{
    private bool isSelectingP2 = false;

    [Header("UI References")]
    public Text statusText;
    public GameObject p1ConfirmBtn;
    public GameObject p2ConfirmBtn;
    public GameObject vsPanel;

    private void Start()
    {
        p1ConfirmBtn.SetActive(true);
        p2ConfirmBtn.SetActive(false);
        UpdateStatusText();
    }

    /// <summary>
    /// UIボタンからキャラクターIDを受け取り、現在の選択フェーズに応じてGameSettingsへ登録する
    /// </summary>
    public void SelectCharacter(int id)
    {
        if (!isSelectingP2)
        {
            GameSettings.Instance.selectedPlayerID = id;
            Debug.Log($"[CharaSelect] 1P Selected: ID {id}");
        }
        else
        {
            GameSettings.Instance.selectedEnemyID = id;
            Debug.Log($"[CharaSelect] 2P Selected: ID {id}");
        }
        UpdateStatusText();
    }

    public void OnConfirmP1()
    {
        if (GameSettings.Instance.selectedPlayerID == 0) return; // 未選択時のガード節

        isSelectingP2 = true; // 2P選択フェーズへ移行
        p1ConfirmBtn.SetActive(false);
        p2ConfirmBtn.SetActive(true);
        UpdateStatusText();
    }

    public void OnConfirmP2()
    {
        if (GameSettings.Instance.selectedEnemyID == 0) return;

        Debug.Log("[CharaSelect] 両プレイヤーの選択が完了しました。バトルシーンへ遷移します。");
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

    /// <summary>
    /// VS演出の待機時間を挟み、バトルシーンへの非同期ロードを実行する
    /// </summary>
    private IEnumerator VsAnimationRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        SceneManager.LoadScene("BattleScene");
    }
}