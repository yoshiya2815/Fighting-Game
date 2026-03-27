using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("呼び出すプレハブ")]
    public GameObject playerPrefab; // 1Pとして出すキャラ（例：スピードキャラ）
    public GameObject enemyPrefab;  // 2Pとして出すキャラ（例：重戦車）

    [Header("出現位置（スポーンポイント）")]
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;

    void Start()
    {
        // ゲームが始まった瞬間にキャラクターを生成する
        SpawnCharacters();
    }

    void SpawnCharacters()
    {
        // 1Pの生成（Instantiate）
        if (playerPrefab != null && playerSpawnPoint != null)
        {
            // 指定したプレハブを、指定した位置と角度で出現させる
            Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        }

        // 2Pの生成
        if (enemyPrefab != null && enemySpawnPoint != null)
        {
            Instantiate(enemyPrefab, enemySpawnPoint.position, enemySpawnPoint.rotation);
        }

        Debug.Log("キャラクターの生成が完了しました！");
    }
}