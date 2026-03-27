using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance; // どこからでもアクセスできるようにする

    public int selectedPlayerID; // 1Pが選んだキャラID
    public int selectedEnemyID;  // 2P（またはCPU）が選んだキャラID

    void Awake()
    {
        // シーンが変わっても自分を消さない魔法の呪文
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}