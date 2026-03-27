using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class MoveData
{
    public string id;
    public string moveName;
    public int usableCharID;
    public int totalFrames;

    public List<int> hitFrames = new List<int>();
    public List<int> activeFrames = new List<int>(); // ★追加：[5] 持続フレーム
    public List<int> damages = new List<int>();

    public List<int> comboCounts; // ★追加：コンボ数（"1,1,1" などに対応）

    public int saValue; // [8] SA値(優先順位)
    public int saBreak; // [9] SA削り値

    public bool isThrow; // ★追加：この技は「掴み（投げ）」であるか？

    public List<int> hitStunFrames = new List<int>(); // ★追加：[10] 仰け反り値

    // ▼ 変数の追加（saBreak の下あたりに追加）
    public List<string> hitTypes = new List<string>(); // [11] 判定 (上, 中, 下)

    // ▼▼▼ 今回追加：当たり判定の範囲データ ▼▼▼
    public List<float> hitboxMinX = new List<float>(); // [12] 判定最小X
    public List<float> hitboxMaxX = new List<float>(); // [13] 判定最大X
    public List<float> hitboxMinY = new List<float>(); // [14] 判定最小Y
    public List<float> hitboxMaxY = new List<float>(); // [15] 判定最大Y

    public List<float> moveX = new List<float>();
    public List<float> moveY = new List<float>();
    public List<int> moveStartX = new List<int>();
    public List<int> moveEndX = new List<int>();
    public List<int> moveStartY = new List<int>();
    public List<int> moveEndY = new List<int>();

    public List<float> knockbackX = new List<float>(); // 吹き飛ばし(X)
    public List<float> knockbackY = new List<float>(); // 吹き飛ばし(Y)

    public int usableLocation; // [25] 空中使用可否

    // ==========================================
    // ★追加：飛び道具・罠システム用の変数
    // ==========================================
    public bool isProjectile;         // 弾かどうか（CSVで 1 なら true にする）
    public float projectileSpeedX;    // X方向の弾速
    public float projectileSpeedY;    // Y方向の弾速
    public int maxProjectileCount;    // 画面に出せる上限数
    public float projectileLifeTime;  // 弾が自然消滅するまでの秒数

    public void SetData(string[] row)
    {
        id = row[0];
        moveName = row.Length > 1 ? row[1] : "";

        // ==========================================
        // ★追加：3列目（インデックス2）から使用可能キャラIDを読み込む！
        // ==========================================
        if (row.Length > 2)
        {
            int.TryParse(row[2], out usableCharID);
        }

        totalFrames = ParseInt(row[3]);

        hitFrames = ParseIntList(row.Length > 4 ? row[4] : "");
        activeFrames = ParseIntList(row.Length > 5 ? row[5] : "");
        comboCounts = ParseIntList(row.Length > 6 ? row[6] : "1");
        damages = ParseIntList(row.Length > 7 ? row[7] : "");

        saValue = ParseInt(row.Length > 8 ? row[8] : "0");

        string saBreakStr = row.Length > 9 ? row[9].Trim() : "0";
        if (saBreakStr == "貫通")
        {
            saBreak = 9999;   // 絶対にSAを破壊する特大数値にしておく！
            isThrow = true;   // 投げ技フラグをONにする！
        }
        else
        {
            saBreak = ParseInt(saBreakStr);
            isThrow = false;  // 通常の打撃技
        }

        hitStunFrames = ParseIntList(row.Length > 10 ? row[10] : "");

        hitTypes = ParseStringList(row.Length > 11 ? row[11] : "");

        // ▼ 当たり判定の読み込み ▼
        hitboxMinX = ParseFloatList(row.Length > 12 ? row[12] : "");
        hitboxMaxX = ParseFloatList(row.Length > 13 ? row[13] : "");
        hitboxMinY = ParseFloatList(row.Length > 14 ? row[14] : "");
        hitboxMaxY = ParseFloatList(row.Length > 15 ? row[15] : "");

        // ▼ 移動データの読み込み（列番号を最新のCSVに合わせました） ▼
        moveX = ParseFloatList(row.Length > 16 ? row[16] : "");
        moveY = ParseFloatList(row.Length > 17 ? row[17] : "");
        moveStartX = ParseIntList(row.Length > 18 ? row[18] : "");
        moveEndX = ParseIntList(row.Length > 19 ? row[19] : "");
        moveStartY = ParseIntList(row.Length > 20 ? row[20] : "");
        moveEndY = ParseIntList(row.Length > 21 ? row[21] : "");

        AdjustMoveFrames(moveX, moveStartX, moveEndX, totalFrames);
        AdjustMoveFrames(moveY, moveStartY, moveEndY, totalFrames);

        // ▼ 吹き飛ばしデータの読み込み（※列番号は実際のCSVに合わせて修正してください！）
        knockbackX = ParseFloatList(row.Length > 22 ? row[23] : "");
        knockbackY = ParseFloatList(row.Length > 23 ? row[24] : "");

        usableLocation = ParseInt(row.Length > 24 ? row[25] : "0");

        this.isProjectile = (row.Length > 25 && row[26].Trim() == "1");
        if (row.Length > 26) float.TryParse(row[27].Trim(), out this.projectileSpeedX);
        if (row.Length > 27) float.TryParse(row[28].Trim(), out this.projectileSpeedY);
        if (row.Length > 28) int.TryParse(row[29].Trim(), out this.maxProjectileCount);
        if (row.Length > 29) float.TryParse(row[30].Trim(), out this.projectileLifeTime);

        
    }

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
}