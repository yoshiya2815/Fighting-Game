using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// マスターデータ(CSV)の読み込みとメモリキャッシュを一元管理するデータプロバイダー。
/// ハードコーディングを排除し、プランナーが調整したデータを安全かつ高速に各システムへ供給する（データ駆動アーキテクチャ）。
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("Memory Cache")]
    private Dictionary<string, MoveData> moveLibrary = new Dictionary<string, MoveData>();
    private Dictionary<int, CharaData> charaLibrary = new Dictionary<int, CharaData>();

    [Header("CSV Assets")]
    public TextAsset charaDataCsv;
    public TextAsset enemyPatternCsv;

    public List<EnemyPatternData> enemyPatterns { get; private set; } = new List<EnemyPatternData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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
            reader.ReadLine(); // ヘッダーのスキップ

            while (reader.Peek() != -1)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // 独自の安全なパーサーを用いて列を分割
                string[] row = SplitCsvLine(line);
                if (row.Length < 1 || string.IsNullOrEmpty(row[0]) || !char.IsDigit(row[0][0])) continue;

                MoveData data = new MoveData();
                data.SetData(row);

                if (!moveLibrary.ContainsKey(data.id)) moveLibrary.Add(data.id, data);
            }
        }
        Debug.Log($"[DataManager] 技データをロードしました: {moveLibrary.Count}件");
    }

    private void LoadCharaCSV()
    {
        if (charaDataCsv == null)
        {
            Debug.LogError("[DataManager] 例外エラー: CharaData.csvがアサインされていません。");
            return;
        }

        string[] lines = charaDataCsv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
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
        Debug.Log($"[DataManager] キャラデータをロードしました: {charaLibrary.Count}件");
    }

    private void LoadEnemyPatternData()
    {
        if (enemyPatternCsv == null) return;

        string[] lines = enemyPatternCsv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
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

    /// <summary>
    /// 特定のキャラクターが使用可能な技リスト（共通技含む）を取得する。
    /// </summary>
    public List<MoveData> GetMovesForCharacter(int charaID)
    {
        List<MoveData> availableMoves = new List<MoveData>();
        foreach (var move in moveLibrary.Values)
        {
            if (move.usableCharID == 0 || move.usableCharID == charaID) availableMoves.Add(move);
        }
        return availableMoves;
    }

    /// <summary>
    /// ダブルクォーテーション(DQ)によるエスケープに対応した独自の安全なCSVパーサー。
    /// データ内に文字列としてのカンマ（例: "A,B"）が含まれていても列が破綻しない堅牢性を担保する。
    /// </summary>
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
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }
        result.Add(currentField);
        return result.ToArray();
    }
}