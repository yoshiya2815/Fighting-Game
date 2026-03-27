using System;
using UnityEngine;

[Serializable]
public class CharaData
{
    public int id;                   // [0] ID
    public string charaName;         // [1] キャラ名
    public int maxHp;                // [2] HP
    public float damageMultiplier;   // [3] ダメージ計算 (技ダメージにかかる基本倍率)
    public float comboMultiplier;    // [4] コンボダメージ倍率 (コンボ時に増える倍率)
    public int hitStunResist;        // [5] 仰け反り耐性
    public int guts;                 // [6] 根性値
    public int guardEndurance;       // [7] ガード耐久値
    public int guardRecovery;        // [8] ガード回復量
    public float moveSpeedMultiplier;// [9] 移動量補正
    public int saBonus;              // [10] SA補正
    public float weight;             // [11] 重量 (吹き飛びにくさなどに使用)
    public string prefabName;        // [12] プレハブ名

    /// <summary>
    /// CSVの1行分のデータ（配列）を受け取って、各変数に割り当てる
    /// </summary>
    public void SetData(string[] row)
    {
        // ★修正：すべての読み込みに .Trim() をつけて、見えない空白によるバグを完全防止！
        int.TryParse(row[0].Trim(), out id);
        charaName = row.Length > 1 ? row[1].Trim() : "";
        
        if (row.Length > 2) int.TryParse(row[2].Trim(), out maxHp);
        if (row.Length > 3) float.TryParse(row[3].Trim(), out damageMultiplier);
        if (row.Length > 4) float.TryParse(row[4].Trim(), out comboMultiplier);
        if (row.Length > 5) int.TryParse(row[5].Trim(), out hitStunResist);
        if (row.Length > 6) int.TryParse(row[6].Trim(), out guts);
        if (row.Length > 7) int.TryParse(row[7].Trim(), out guardEndurance);
        if (row.Length > 8) int.TryParse(row[8].Trim(), out guardRecovery);
        if (row.Length > 9) float.TryParse(row[9].Trim(), out moveSpeedMultiplier);
        if (row.Length > 10) int.TryParse(row[10].Trim(), out saBonus);
        if (row.Length > 11) float.TryParse(row[11].Trim(), out weight);
        
        prefabName = row.Length > 12 ? row[12].Trim() : "";
    }

    // --- 文字列を安全に数字に変換する便利メソッド ---
    private int ParseInt(string s)
    {
        return int.TryParse(s, out int r) ? r : 0;
    }

    private float ParseFloat(string s)
    {
        return float.TryParse(s, out float r) ? r : 0f;
    }
}