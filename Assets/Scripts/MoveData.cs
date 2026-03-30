using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// キャラクターが使用する個々の「技（アクション）」のパラメータを保持するデータモデル。
/// ハードコーディングを避け、CSVから動的にデシリアライズされることで、
/// プログラマの手を介さずにプランナーが当たり判定やフレームデータを調整可能にする。
/// </summary>
[Serializable]
public class MoveData
{
    [Header("Basic Information")]
    public string id;
    public string moveName;
    public int usableCharID;
    public int totalFrames;
    public int usableLocation;

    [Header("Combat & Frame Data")]
    public List<int> hitFrames = new List<int>();
    public List<int> activeFrames = new List<int>();
    public List<int> comboCounts;
    public List<int> damages = new List<int>();
    public List<string> hitTypes = new List<string>();
    public List<int> hitStunFrames = new List<int>();

    [Header("Armor & Throw Mechanics")]
    public int saValue;
    public int saBreak;
    public bool isThrow;

    [Header("Hurtbox & Hitbox Constraints")]
    public List<float> hitboxMinX = new List<float>();
    public List<float> hitboxMaxX = new List<float>();
    public List<float> hitboxMinY = new List<float>();
    public List<float> hitboxMaxY = new List<float>();

    [Header("Physics & Movement")]
    public List<float> moveX = new List<float>();
    public List<float> moveY = new List<float>();
    public List<int> moveStartX = new List<int>();
    public List<int> moveEndX = new List<int>();
    public List<int> moveStartY = new List<int>();
    public List<int> moveEndY = new List<int>();

    public List<float> knockbackX = new List<float>();
    public List<float> knockbackY = new List<float>();

    [Header("Projectile Properties")]
    public bool isProjectile;
    public float projectileSpeedX;
    public float projectileSpeedY;
    public int maxProjectileCount;
    public float projectileLifeTime;

    /// <summary>
    /// CSVの1行（row）からデータを解析し、自身にマッピングするデシリアライズ処理。
    /// 投げ技の判定など、特定の文字列入力に対するフラグ変換もここで吸収する。
    /// </summary>
    public void SetData(string[] row)
    {
        id = row[0];
        moveName = row.Length > 1 ? row[1] : "";

        if (row.Length > 2) int.TryParse(row[2], out usableCharID);

        totalFrames = ParseInt(row[3]);

        hitFrames = ParseIntList(row.Length > 4 ? row[4] : "");
        activeFrames = ParseIntList(row.Length > 5 ? row[5] : "");
        comboCounts = ParseIntList(row.Length > 6 ? row[6] : "1");
        damages = ParseIntList(row.Length > 7 ? row[7] : "");
        saValue = ParseInt(row.Length > 8 ? row[8] : "0");

        string saBreakStr = row.Length > 9 ? row[9].Trim() : "0";
        if (saBreakStr == "貫通")
        {
            saBreak = 9999;
            isThrow = true;
        }
        else
        {
            saBreak = ParseInt(saBreakStr);
            isThrow = false;
        }

        hitStunFrames = ParseIntList(row.Length > 10 ? row[10] : "");
        hitTypes = ParseStringList(row.Length > 11 ? row[11] : "");

        hitboxMinX = ParseFloatList(row.Length > 12 ? row[12] : "");
        hitboxMaxX = ParseFloatList(row.Length > 13 ? row[13] : "");
        hitboxMinY = ParseFloatList(row.Length > 14 ? row[14] : "");
        hitboxMaxY = ParseFloatList(row.Length > 15 ? row[15] : "");

        moveX = ParseFloatList(row.Length > 16 ? row[16] : "");
        moveY = ParseFloatList(row.Length > 17 ? row[17] : "");
        moveStartX = ParseIntList(row.Length > 18 ? row[18] : "");
        moveEndX = ParseIntList(row.Length > 19 ? row[19] : "");
        moveStartY = ParseIntList(row.Length > 20 ? row[20] : "");
        moveEndY = ParseIntList(row.Length > 21 ? row[21] : "");

        AdjustMoveFrames(moveX, moveStartX, moveEndX, totalFrames);
        AdjustMoveFrames(moveY, moveStartY, moveEndY, totalFrames);

        knockbackX = ParseFloatList(row.Length > 22 ? row[23] : "");
        knockbackY = ParseFloatList(row.Length > 23 ? row[24] : "");

        usableLocation = ParseInt(row.Length > 24 ? row[25] : "0");

        isProjectile = (row.Length > 25 && row[26].Trim() == "1");
        if (row.Length > 26) float.TryParse(row[27].Trim(), out projectileSpeedX);
        if (row.Length > 27) float.TryParse(row[28].Trim(), out projectileSpeedY);
        if (row.Length > 28) int.TryParse(row[29].Trim(), out maxProjectileCount);
        if (row.Length > 29) float.TryParse(row[30].Trim(), out projectileLifeTime);
    }

    /// <summary>
    /// CSVの入力漏れや不正値を補正し、アニメーションフレームの整合性を保証する。
    /// </summary>
    private void AdjustMoveFrames(List<float> moveList, List<int> startList, List<int> endList, int maxFrames)
    {
        for (int i = 0; i < moveList.Count; i++)
        {
            if (startList.Count <= i) startList.Add(1);
            if (startList[i] <= 0) startList[i] = 1;

            if (endList.Count <= i) endList.Add(maxFrames);
            if (endList[i] <= 0) endList[i] = maxFrames;
        }
    }

    #region String Parsing Helpers
    private int ParseInt(string s) { return int.TryParse(s, out int r) ? r : 0; }
    private float ParseFloat(string s) { return float.TryParse(s, out float r) ? r : 0f; }

    private List<int> ParseIntList(string s)
    {
        List<int> list = new List<int>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        string[] splits = s.Split(',');
        foreach (var str in splits) list.Add(ParseInt(str));
        return list;
    }

    private List<float> ParseFloatList(string s)
    {
        List<float> list = new List<float>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        string[] splits = s.Split(',');
        foreach (var str in splits) list.Add(ParseFloat(str));
        return list;
    }

    private List<string> ParseStringList(string s)
    {
        List<string> list = new List<string>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        string[] splits = s.Split(',');
        foreach (var str in splits) list.Add(str.Trim());
        return list;
    }
    #endregion
}