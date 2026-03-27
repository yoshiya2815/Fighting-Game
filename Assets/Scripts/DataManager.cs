using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    private Dictionary<string, MoveData> moveLibrary = new Dictionary<string, MoveData>();
    private Dictionary<int, CharaData> charaLibrary = new Dictionary<int, CharaData>();

    // ==========================================
    // ★ここに追加されているか確認してください！
    // ==========================================
    public TextAsset charaDataCsv;
    public TextAsset enemyPatternCsv;
    public List<EnemyPatternData> enemyPatterns = new List<EnemyPatternData>();

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        LoadMoveCSV();
        LoadCharaCSV();
        LoadEnemyPatternData();
    }

    private void LoadMoveCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>("MoveData");
        if (csvFile == null) return;

        using (StringReader reader = new StringReader(csvFile.text))
        {
            reader.ReadLine();

            while (reader.Peek() != -1)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // ▼ 修正：単純なカンマ区切りではなく、安全な専用機能を使う
                string[] row = SplitCsvLine(line);
                if (row.Length < 1 || string.IsNullOrEmpty(row[0]) || !char.IsDigit(row[0][0])) continue;

                MoveData data = new MoveData();
                data.SetData(row);

                if (!moveLibrary.ContainsKey(data.id)) moveLibrary.Add(data.id, data);
            }
        }
        Debug.Log($"技データをロードしました: {moveLibrary.Count}件");
    }

    private void LoadCharaCSV()
    {
        if (charaDataCsv == null)
        {
            UnityEngine.Debug.LogError("★エラー：DataManagerにCharaData.csvがセットされていません！Inspectorを確認してください！");
            return;
        }

        string[] lines = charaDataCsv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++) // ヘッダーを飛ばす
        {
            string[] row = SplitCsvLine(lines[i]);
            if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0])) continue;

            CharaData chara = new CharaData();
            chara.SetData(row);
            if (!charaLibrary.ContainsKey(chara.id))
            {
                charaLibrary.Add(chara.id, chara);
            }
        }
        Debug.Log($"キャラデータをロードしました: {charaLibrary.Count}件");
        foreach (var key in charaLibrary.Keys)
        {
            UnityEngine.Debug.Log($"【登録チェック】ID: {key} として「{charaLibrary[key].charaName}」を記憶しました！");
        }
    }

    // ==========================================
    // ★追加：CSVからパターンを読み込むメソッド
    // ==========================================
    private void LoadEnemyPatternData()
    {
        if (enemyPatternCsv == null) return;

        string[] lines = enemyPatternCsv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++) // 1行目(ヘッダー)は飛ばす
        {
            string[] row = lines[i].Split(',');
            if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0])) continue;

            EnemyPatternData pattern = new EnemyPatternData();
            pattern.SetData(row);
            enemyPatterns.Add(pattern);
        }
    }

    public CharaData GetChara(int id)
    {
        if (charaLibrary.TryGetValue(id, out CharaData data)) return data;
        return null;
    }

    public MoveData GetMove(string id)
    {
        if (moveLibrary.TryGetValue(id, out MoveData data)) return data;
        return null;
    }

    public List<MoveData> GetMovesForCharacter(int charaID)
    {
        List<MoveData> availableMoves = new List<MoveData>();
        foreach (var move in moveLibrary.Values)
        {
            if (move.usableCharID == 0 || move.usableCharID == charaID) availableMoves.Add(move);
        }
        return availableMoves;
    }

    // ========================================================
    // ▼ 今回の目玉：ダブルクォーテーションで囲まれたカンマを無視する機能
    // ========================================================
    private string[] SplitCsvLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                inQuotes = !inQuotes; // 「"」が来たら、囲み状態を切り替える
            }
            else if (c == ',' && !inQuotes)
            {
                // 「"」で囲まれていない本物の区切りカンマが来たら、列を分ける
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        result.Add(currentField); // 最後の列を追加
        return result.ToArray();
    }
}