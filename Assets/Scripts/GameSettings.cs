using UnityEngine;

/// <summary>
/// シーン遷移を跨いでゲーム設定（選択されたキャラクターIDなど）を保持するデータコンテナ。
/// DontDestroyOnLoadを用いたシングルトンパターンにより、アウトゲームとインゲーム間でデータの受け渡しを行う。
/// </summary>
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    [Header("Battle Configurations")]
    public int selectedPlayerID;
    public int selectedEnemyID;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン遷移時に破棄されないよう保護
        }
        else
        {
            Destroy(gameObject); // 重複するインスタンスの破棄
        }
    }
}