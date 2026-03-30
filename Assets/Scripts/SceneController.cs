using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル画面やリザルト画面などから、特定のシーンへの遷移を管理するコントローラー。
/// UIボタンのOnClickイベントなどからトリガーされることを想定している。
/// </summary>
public class SceneController : MonoBehaviour
{
    /// <summary>
    /// キャラクター選択シーン（CharSelect）へ非同期で遷移する。
    /// </summary>
    public void GoToCharSelect()
    {
        SceneManager.LoadScene("CharSelect");
    }
}