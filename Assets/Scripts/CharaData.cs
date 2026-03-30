using System;
using UnityEngine;

/// <summary>
/// キャラクターの基礎ステータスや各種補正値を定義するデータモデル。
/// CSVからデシリアライズされ、Fighterクラスの初期化パラメータとして注入される。
/// </summary>
[Serializable]
public class CharaData
{
    [Header("Basic Information")]
    public int id;
    public string charaName;
    public int maxHp;
    public string prefabName;

    [Header("Combat Multipliers")]
    public float damageMultiplier;
    public float comboMultiplier;
    public int saBonus;

    [Header("Defense & Mechanics")]
    public int hitStunResist;
    public int guts;
    public int guardEndurance;
    public int guardRecovery;

    [Header("Physics Parameters")]
    public float moveSpeedMultiplier;
    public float weight;

    /// <summary>
    /// CSVの行データから各パラメータを安全にデシリアライズする。
    /// 不可視の空白文字によるパースエラーを防ぐため、Trim処理によるサニタイズを徹底している。
    /// </summary>
    public void SetData(string[] row)
    {
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

    #region Parsing Helpers
    private int ParseInt(string s)
    {
        return int.TryParse(s, out int r) ? r : 0;
    }

    private float ParseFloat(string s)
    {
        return float.TryParse(s, out float r) ? r : 0f;
    }
    #endregion
}