using UnityEngine;

/// <summary>
/// バトル開始時のアクター（Fighter）の動的生成と初期配置を担当するスポーナー。
/// シーン上の固定オブジェクトへの依存を減らし、将来的なキャラクター切り替えや
/// マルチプレイ拡張に対応しやすい疎結合な初期化フローを提供する。
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("Spawn Points")]
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;

    private void Start()
    {
        SpawnCharacters();
    }

    /// <summary>
    /// プレハブのインスタンス化とTransformの初期設定を行う
    /// </summary>
    private void SpawnCharacters()
    {
        if (playerPrefab != null && playerSpawnPoint != null)
        {
            Instantiate(playerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        }

        if (enemyPrefab != null && enemySpawnPoint != null)
        {
            Instantiate(enemyPrefab, enemySpawnPoint.position, enemySpawnPoint.rotation);
        }

        Debug.Log("[BattleManager] キャラクターの動的生成が完了しました。");
    }
}