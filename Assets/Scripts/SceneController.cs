using UnityEngine;
using UnityEngine.SceneManagement; // シーン移動に必須！

public class SceneController : MonoBehaviour
{
    public void GoToCharSelect()
    {
        // "CharSelect" という名前のシーンへ移動
        SceneManager.LoadScene("CharSelect");
    }
}