using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵AIの行動パターン定義を保持するデータモデル。
/// 距離条件や特定のキャラクターIDに基づく専用パターンのフィルタリング条件を定義し、
/// 状況に応じた最適な技リスト（コンボ）をAIシステムへ提供する。
/// </summary>
public class EnemyPatternData
{
    public int patternId;
    public string patternName;
    public int usableCharID;
    public List<string> moveIds = new List<string>();

    [Header("Distance Constraints")]
    public float minDistance; // AIがこのパターンを評価する最小距離
    public float maxDistance; // AIがこのパターンを評価する最大距離

    /// <summary>
    /// CSVの行データを解析し、AIパターンデータとしてマッピングする。
    /// 空白や無効なデータを安全にスキップする。
    /// </summary>
    public void SetData(string[] row)
    {
        int.TryParse(row[0], out patternId);
        patternName = row.Length > 1 ? row[1] : "";

        if (row.Length > 2)
        {
            int.TryParse(row[2], out usableCharID);
        }

        // 技1〜技10のIDリスト化（空要素の除外と空白のトリミング）
        for (int i = 3; i < 13; i++)
        {
            if (row.Length > i && !string.IsNullOrWhiteSpace(row[i]))
            {
                moveIds.Add(row[i].Trim());
            }
        }

        if (row.Length > 13) float.TryParse(row[13].Trim(), out minDistance);
        if (row.Length > 14) float.TryParse(row[14].Trim(), out maxDistance);
    }
}