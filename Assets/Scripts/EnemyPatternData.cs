using System.Collections.Generic;

public class EnemyPatternData
{
    public int patternId;
    public string patternName;
    public int usableCharID;
    public List<string> moveIds = new List<string>(); // 技IDのリスト

    // ==========================================
    // ★追加：このパターンを発動できる距離の範囲
    // ==========================================
    public float minDistance; // 最低この距離以上離れていないと使わない（密着=0）
    public float maxDistance; // この距離より遠いと使わない（遠距離=100など）

    public void SetData(string[] row)
    {
        int.TryParse(row[0], out patternId);
        patternName = row.Length > 1 ? row[1] : "";

        // ★追加・3列目：使用可能キャラID（標準機能の int.TryParse を使います！）
        if (row.Length > 2)
        {
            int.TryParse(row[2], out usableCharID);
        }

        // 技1〜技10 は CSVの [2]列目 から [11]列目 に入っている
        for (int i = 3; i < 13; i++)
        {
            if (row.Length > i && !string.IsNullOrWhiteSpace(row[i]))
            {
                moveIds.Add(row[i].Trim()); // 余計な空白を消してリストに追加！
            }
        }

        if (row.Length > 13) float.TryParse(row[13].Trim(), out minDistance);
        if (row.Length > 14) float.TryParse(row[14].Trim(), out maxDistance);
    }
}