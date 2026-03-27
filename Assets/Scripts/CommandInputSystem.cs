using UnityEngine;
using System.Collections; // ★コルーチンを使うために必要！
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;

public class CommandInputSystem : MonoBehaviour
{
    public static CommandInputSystem Instance;

    [SerializeField] private int maxTurnFrames = 10;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("タイムラインUI設定")]
    [SerializeField] private RectTransform timelineArea;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private float widthPerFrame = 80f;
    [SerializeField] private float blockHeight = 100f;

    [Header("技ボタン自動生成UI設定")]
    [SerializeField] private RectTransform commandButtonArea;
    [SerializeField] private GameObject commandButtonPrefab;
    [SerializeField] private int testCharaID = 2;

    // ▼▼▼ 今回追加：ステージ上のキャラクターと紐づける枠 ▼▼▼
    [Header("バトルキャラクター")]
    [SerializeField] public Fighter playerFighter;
    [SerializeField] public Fighter enemyFighter;

    private List<MoveData> selectedMoves = new List<MoveData>();
    private List<List<GameObject>> generatedBlocksList = new List<List<GameObject>>();
    private int currentCost = 0;
    // ★追加：敵のコマンドリスト
    public List<MoveData> enemySelectedMoves = new List<MoveData>();

    [Header("バトル生成設定")]
    public GameObject playerPrefab;     // Assetsからセットするプレイヤーのプレハブ
    public GameObject enemyPrefab;      // Assetsからセットする敵のプレハブ
    public Transform playerSpawnPoint;  // 1Pの出現位置（Hierarchyの空オブジェクト）
    public Transform enemySpawnPoint;   // 2Pの出現位置（Hierarchyの空オブジェクト）
    public HPGaugeController playerHpGauge; // 1P側のHPゲージコントローラー
    public HPGaugeController enemyHpGauge;  // 2P側のHPゲージコントローラー

    [Header("敵のAI設定")]
    public bool useRandomEnemyPattern = true; // チェックを入れるとランダム、外すと指定ID
    public int fixedPatternId = 1;            // 指定したいパターンのID（CSVの1列目）

    [Header("デバッグ設定")]
    public bool showHitbox = true; // チェックを外すと攻撃枠が消えます

    [Header("デバッグ表示用")]
    public TMPro.TextMeshProUGUI debugInfoText; // ★追加：画面に情報を出すためのテキスト

    [Header("UI設定")]
    public TextMeshProUGUI actionText;
    public ComboUIController comboUI;  // ★追加：コンボ表示用のテキスト
    public ComboUIController enemyComboUI;   // 敵用（右画面）

    // ==========================================
    // ▼▼▼ この2行をスクリプトの上の方に追加！ ▼▼▼
    // ==========================================
    [Header("UI設定（ステータスアイコン）")]
    public StatusIconController playerStatusIcon; // 1P側のアイコンコントローラー
    public StatusIconController enemyStatusIcon;  // 2P側のアイコンコントローラー

    [Header("演出設定")]
    public int hitStopTimer = 0; // ★追加：ヒットストップの残りフレーム数

    [Header("リザルトUI")]
    public GameObject resultPanel;
    public Text resultWinnerText;

    [Header("ラウンド管理")]
    public int p1Wins = 0; // 1Pの勝利数
    public int p2Wins = 0; // 2Pの勝利数
    public int requiredWins = 2; // 勝利に必要なラウンド数（2本先取）
    public int currentRound = 1; // 現在のラウンド数

    // コンボ数をカウントする変数
    private int currentCombo = 0;
    private int enemyCombo = 0; // ★追加：敵のコンボ数もここで宣言する！
    public int currentTurnCount = 0; // ★追加：現在のターン数を記録する変数

    // 攻撃判定を描画するために一時的に記憶する箱
    private class DebugHitbox
    {
        public float minX, maxX, minY, maxY;
        public float timer; // 画面に表示しておく残り時間
        public bool isGuard; // ★追加：trueなら青色(ガード)、falseなら赤色(攻撃)で描画する！

        // ==========================================
        // ★追加：飛び道具（遠距離弾や罠）を管理するための拡張機能
        // ==========================================
        public Fighter owner;           // 誰が出した判定か（仰け反った時に消すため）
        public MoveData sourceMove;     // 何の技から出た判定か（ダメージ計算や個数制限のため）
        public float speedX;            // X方向の移動スピード
        public float speedY;            // Y方向の移動スピード
        public int facingDir;           // 放たれた時の向き（1 か -1）
                                        // （※hasHitTarget を弾ごとに持たせる必要がありますが、まずは移動と消滅を優先します）
                                        // ==========================================
                                        // ★追加：この弾はすでに相手に当たったか？
                                        // ==========================================
        public bool hasHit;
    }

    private List<DebugHitbox> activeHitboxes = new List<DebugHitbox>();

    // ==========================================
    // ★追加：飛び道具・罠 専用のデータクラス
    // ==========================================
    public class ProjectileData
    {
        public Fighter owner;           // 撃った人
        public MoveData sourceMove;     // 技のデータ
        public float minX, maxX;        // 当たり判定の座標
        public float minY, maxY;
        public float speedX, speedY;    // 飛ぶスピード
        public int facingDir;           // 撃った向き
        public float timer;             // 寿命
        public bool hasHit;             // 命中フラグ
        public GameObject visualBlock;  // 画面に表示する「弾のブロック」本体！
    }

    // 変数宣言エリアに追加
    public GameObject projectilePrefab; // Unityエディタでセットする弾のブロック
    private List<ProjectileData> activeProjectiles = new List<ProjectileData>(); // 弾専用リスト

    [Header("ゲーム状態")]
    public bool isGameOver = false; // ゲームが決着したか？
    public TMPro.TextMeshProUGUI resultText; // 「YOU WIN!」などを表示するテキスト
    // ==========================================
    // ★追加：ターン実行中かどうかを判定するフラグ
    // ==========================================
    private bool isExecutingTurn = false;

    public bool isPlayerAutoMode = false; // 　インスペクターやUIボタンでON/OFFする

    [Header("KO演出用UI")]
    public GameObject battleUIPanel; // HPバーやボタンをまとめた親オブジェクト
    public GameObject koTextObject;  // 「K.O.」の文字オブジェクト

    private void Awake()
    {
        // ▼ 追加
        Instance = this;
    }

    void Start()
    {
        SetupBattle();
        UpdateUI();
        GenerateCommandButtons();

        int pID = GameSettings.Instance.selectedPlayerID;
        int eID = GameSettings.Instance.selectedEnemyID;

        if (playerFighter != null)
        {
            // プレイヤーの子オブジェクトから、StatusIconController を探して、
            // CommandInputSystem（自分）の playerStatusIcon 変数に登録する！
            playerStatusIcon = playerFighter.GetComponentInChildren<StatusIconController>();
        }
        if (enemyFighter != null)
        {
            enemyStatusIcon = enemyFighter.GetComponentInChildren<StatusIconController>();
        }
        // ★ゲーム開始時に、プレイヤーと敵にステータスを流し込むテスト
        if (playerFighter != null) playerFighter.Init(DataManager.Instance.GetChara(pID));
        if (enemyFighter != null) enemyFighter.Init(DataManager.Instance.GetChara(eID)); // 敵はとりあえずID:2(パワー等)にする

        if (playerHpGauge != null && playerFighter != null)
        {
            playerHpGauge.InitHP(playerFighter.maxHp);
        }
        if (enemyHpGauge != null && enemyFighter != null)
        {
            enemyHpGauge.InitHP(enemyFighter.maxHp);
        }

        // ==========================================
        // ★追加：ゲーム開始時に、お互いを向かせる！
        // ==========================================
        if (playerFighter != null && enemyFighter != null)
        {
            playerFighter.LookAtTarget(enemyFighter.transform.position.x);
            enemyFighter.LookAtTarget(playerFighter.transform.position.x);
        }
    }

    private void SetupBattle()
    {
        GameObject p1PrefabToSpawn = playerPrefab; // デフォルトはインスペクターの枠
        GameObject p2PrefabToSpawn = enemyPrefab;  // デフォルトはインスペクターの枠

        // ==========================================
        // ★ステップ3の統合：GameSettingsがあれば上書きする
        // ==========================================
        if (GameSettings.Instance != null)
        {
            // IDを元に、ResourcesフォルダからPrefabを探してくる
            // 例: Resources/Prefabs/Fighter_2.prefab
            GameObject dynamicPlayer = Resources.Load<GameObject>($"Prefabs/Fighter_{GameSettings.Instance.selectedPlayerID}");
            GameObject dynamicEnemy = Resources.Load<GameObject>($"Prefabs/Fighter_{GameSettings.Instance.selectedEnemyID}");

            if (dynamicPlayer != null) p1PrefabToSpawn = dynamicPlayer;
            if (dynamicEnemy != null) p2PrefabToSpawn = dynamicEnemy;
        }

        // ① キャラクターを出現させる（変数をPrefabToSpawnに差し替え）
        GameObject p1Object = Instantiate(p1PrefabToSpawn, playerSpawnPoint.position, playerSpawnPoint.rotation);
        GameObject p2Object = Instantiate(p2PrefabToSpawn, enemySpawnPoint.position, enemySpawnPoint.rotation);

        // --- 以下、今の配線処理はそのまま継続 ---
        playerFighter = p1Object.GetComponent<Fighter>();
        enemyFighter = p2Object.GetComponent<Fighter>();

        if (playerFighter != null)
        {
            playerFighter.SetIndicatorColor(Color.cyan); // 1Pはシアン（明るい青）
        }

        if (enemyFighter != null)
        {
            enemyFighter.SetIndicatorColor(Color.red); // 2Pは赤
        }

        Debug.Log("【システム完了】動的生成と配線が完了しました！");
    }

    private void GenerateCommandButtons()
    {
        if (commandButtonArea == null || commandButtonPrefab == null) return;

        // 1. 古いボタンをすべて消す
        foreach (Transform child in commandButtonArea) Destroy(child.gameObject);

        List<MoveData> availableMoves = DataManager.Instance.GetMovesForCharacter(playerFighter.charaId);

        // ==========================================
        // ★修正：技を「共通技」と「固有技」の2つのリストに仕分ける！
        // ==========================================
        System.Collections.Generic.List<MoveData> commonMoves = new System.Collections.Generic.List<MoveData>();
        System.Collections.Generic.List<MoveData> uniqueMoves = new System.Collections.Generic.List<MoveData>();

        foreach (MoveData move in availableMoves)
        {
            if (int.TryParse(move.id.Trim(), out int parsedID))
            {
                if (parsedID <= 13) continue; // 移動技はスキップ
            }
            else continue;

            // 派生技・成功技はスキップ
            if (move.moveName.Contains("派生") || move.moveName.Contains("成功")) continue;

            // リストに仕分け！
            if (move.usableCharID == 0) commonMoves.Add(move);
            else uniqueMoves.Add(move);
        }

        // ==========================================
        // ★修正：共通技（グレー）を先に並べ、その後に固有技（オレンジ）を並べる
        // ==========================================
        foreach (MoveData move in commonMoves)
        {
            CreateSingleButton(move, new Color(0.8f, 0.8f, 0.8f));
        }

        // もし「共通技と固有技の間に、1個分の空白（隙間）を空けたい！」という場合は、
        // ここで透明なダミーボタンを生成する、という裏技もコードだけで可能です！

        foreach (MoveData move in uniqueMoves)
        {
            CreateSingleButton(move, new Color(1.0f, 0.7f, 0.4f));
        }
    }

    // ==========================================
    // ★追加：ボタンを1つ生成する処理を独立させてスッキリまとめる！
    // ==========================================
    private void CreateSingleButton(MoveData move, Color btnColor)
    {
        GameObject newBtnObj = Instantiate(commandButtonPrefab, commandButtonArea);

        // テキストの設定
        TMPro.TextMeshProUGUI btnText = newBtnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (btnText != null) btnText.text = $"{move.moveName}\n({move.totalFrames}F)";

        // 色の設定
        Image btnImage = newBtnObj.GetComponent<Image>();
        if (btnImage != null) btnImage.color = btnColor;

        // クリックイベントの登録
        Button btn = newBtnObj.GetComponent<Button>();
        string moveIDForButton = move.id; // ※ローカル変数にコピーするのが超重要（クロージャ対策バッチリですね！）
        btn.onClick.AddListener(() => TryAddCommand(moveIDForButton));
    }

    public void TryAddCommand(string moveID)
    {
        MoveData move = DataManager.Instance.GetMove(moveID);
        if (move == null) return;

        if (isExecutingTurn || currentCost >= 10)
        {
            Debug.Log("今はコマンドを入力できません");
            return;
        }

        if (currentCost + move.totalFrames <= maxTurnFrames)
        {
            selectedMoves.Add(move);
            currentCost += move.totalFrames;
            CreateTimelineBlocks(move);
            UpdateUI();
        }
    }

    private void CreateTimelineBlocks(MoveData move)
    {
        if (blockPrefab == null || timelineArea == null) return;
        List<GameObject> blocksForThisMove = new List<GameObject>();

        for (int i = 0; i < move.totalFrames; i++)
        {
            GameObject newBlock = Instantiate(blockPrefab, timelineArea);
            RectTransform rect = newBlock.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(widthPerFrame, blockHeight);

            TextMeshProUGUI nameText = newBlock.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                if (i == 0) nameText.text = move.moveName;
                else nameText.text = "-";
            }
            blocksForThisMove.Add(newBlock);
        }
        generatedBlocksList.Add(blocksForThisMove);
    }

    public void UndoLastCommand()
    {
        if (selectedMoves.Count > 0)
        {
            int lastIndex = selectedMoves.Count - 1;
            currentCost -= selectedMoves[lastIndex].totalFrames;
            selectedMoves.RemoveAt(lastIndex);
            foreach (GameObject block in generatedBlocksList[lastIndex]) Destroy(block);
            generatedBlocksList.RemoveAt(lastIndex);
            UpdateUI();
        }
    }

    public void ClearCommands()
    {
        selectedMoves.Clear();
        currentCost = 0;
        foreach (var blockList in generatedBlocksList) foreach (var block in blockList) Destroy(block);
        generatedBlocksList.Clear();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (statusText != null) statusText.text = $"Total Cost: {currentCost} / {maxTurnFrames}";
    }

    void Update()
    {
        // ==========================================
        // ★追加：時間が止まっている間は、落下計算なども完全にパスする！
        // ==========================================
        if (Time.timeScale == 0f) return;

        // ==========================================
        // ★追加：お互いが生きているなら、常に相手の方向を向く（自動振り向き）
        // ==========================================
        // ※もし「技の実行中は振り向かせたくない」場合は、
        // 「今技を出しているか？」というフラグ（isAttacking等）で囲むとさらに自然になります！
        //if (playerFighter != null && enemyFighter != null)
        //{
        //playerFighter.LookAtTarget(enemyFighter.transform.position.x);
        //enemyFighter.LookAtTarget(playerFighter.transform.position.x);
        //}

        // ==========================================
        // ★修正：アクション実行中（キャラが動いている時）だけ、弾を動かして寿命を減らす！
        // ==========================================
        // ⚠️ 「isExecuting」の部分は、ご自身のプログラムで使っている
        // 「今アクションを実行中だよ」というフラグの名前に書き換えてください！
        if (isExecutingTurn)
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                var proj = activeProjectiles[i];

                // 1. 弾のスピードが設定されていれば移動させる
                if (proj.speedX != 0f)
                {
                    float moveX = proj.speedX * proj.facingDir * Time.deltaTime;
                    proj.minX += moveX;
                    proj.maxX += moveX;

                    // ブロックの見た目（GameObject）も一緒に動かす！
                    if (proj.visualBlock != null)
                    {
                        proj.visualBlock.transform.position += new Vector3(moveX, 0, 0);
                    }
                }

                // ==========================================
                // ★追加：弾（罠）自身が、相手に当たったかチェックしてダメージを与える！
                // ==========================================
                // もしこれが弾(罠)で、かつ、まだ誰にも当たっていなければ
                if (!proj.hasHit)
                {
                    // この弾を出したのがプレイヤーなら、狙う相手は敵（逆も然り）
                    Fighter opponent = (proj.owner == playerFighter) ? enemyFighter : playerFighter;
                    if (opponent != null)
                    {
                        float[] eBox = opponent.GetHurtbox();
                        bool isHitX = (proj.maxX >= eBox[0]) && (proj.minX <= eBox[1]);
                        bool isHitY = (proj.maxY >= eBox[2]) && (proj.minY <= eBox[3]);

                        if (isHitX && isHitY)
                        {
                            proj.hasHit = true;
                            proj.timer = 0f; // 当たったらすぐ消す

                            // 例のリッチなヒット処理を呼ぶ
                            ApplyProjectileHit(proj.owner, opponent, proj.sourceMove);
                        }
                    }
                }

                // 3. 寿命の消費と消滅
                proj.timer -= Time.deltaTime;
                if (proj.timer <= 0)
                {
                    // 画面からブロックを消去する！
                    if (proj.visualBlock != null) Destroy(proj.visualBlock);
                    // リストから削除
                    activeProjectiles.RemoveAt(i);
                }
            }

            // ② 近接攻撃（赤枠）とガード（青枠）の消滅ループ
            // ==========================================
            for (int i = activeHitboxes.Count - 1; i >= 0; i--)
            {
                var box = activeHitboxes[i];

                // 赤枠・青枠の寿命（タイマー）を減らす
                box.timer -= Time.deltaTime;

                // 時間切れならリストから削除、まだ生きているならリストの情報を更新
                if (box.timer <= 0)
                {
                    activeHitboxes.RemoveAt(i);
                }
                else
                {
                    activeHitboxes[i] = box;
                }
            }

        }

        // ==========================================
        // ★追加：デバッグ情報のリアルタイム表示
        // ==========================================
        if (debugInfoText != null && playerFighter != null && enemyFighter != null)
        {
            // P1のHP割合と、P2のHP割合も計算して表示すると、TODの判定予測がしやすくなります！
            float p1Percent = (float)playerFighter.currentHp / playerFighter.maxHp * 100f;
            float p2Percent = (float)enemyFighter.currentHp / enemyFighter.maxHp * 100f;

            debugInfoText.text = $"【TURN: {currentTurnCount} / 40】\n" +
                                 $"1P ({playerFighter.charaId}): HP {playerFighter.currentHp} ({p1Percent:F1}%)\n" +
                                 $"2P ({enemyFighter.charaId}): HP {enemyFighter.currentHp} ({p2Percent:F1}%)";
        }
    }

    public void OnToggleAutoMode(bool isOn)
    {
        isPlayerAutoMode = isOn;

        Time.timeScale = isOn ? 10.0f : 1.0f;

        UnityEngine.Debug.Log($"オートモードが {(isOn ? "ON" : "OFF")} になりました！");

        // もし「コマンド入力待機中」にオートをONにしたら、即座にターンを開始させる便利機能！
        if (isOn && !isExecutingTurn && !isGameOver)
        {
            // 最初の1ターン目を自動でキックする
            StartCoroutine(ExecuteTurnRoutine());
        }
    }

    public void ShowResultUI(string message = "")
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            if (resultWinnerText != null && message != "")
            {
                resultWinnerText.text = message;
            }
        }
    }

    // ★リトライボタン（現在のシーンを読み直す）
    public void OnClickRetry()
    {
        // 現在のシーン名を指定してロードし直す
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        // ※GameSettingsは消えないので、同じキャラで再戦が始まります！
    }

    // ★キャラ選択へ戻るボタン
    public void OnClickToSelect()
    {
        // キャラ選択シーン（名前を確認してください）へ戻る
        SceneManager.LoadScene("CharSelect");
    }

    public void OnClickQuitGame()
    {
        Debug.Log("ゲームを終了します...");

        // もしUnityエディタでプレイ中の場合は、プレイモードを停止する
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        // もしビルドした本番のゲーム（Windows版など）の場合は、アプリを終了する
#else
        Application.Quit();
#endif
    }

    // ========================================================
    // ▼▼▼ 今回のメイン：順番に実行する処理 ▼▼▼
    // ========================================================
    public void ExecuteCommands()
    {
        // ★追加：ゲームが終わっていたらボタンを押しても何も起きないようにする
        if (isGameOver) return;

        if (selectedMoves.Count == 0) return;

        if (isExecutingTurn) return;

        // ★チェック2：10個選んでいないなら実行させない
        if (currentCost < 10)
        {
            if (actionText != null) actionText.text = "コマンドを10個選んでください！";
            Debug.LogWarning("コマンド不足です");
            return;
        }

        // ★ここでフラグを立てる（門を閉める）
        isExecutingTurn = true;

        // コルーチン（順番処理）をスタートさせる
        StartCoroutine(ExecuteTurnRoutine());
    }

    // IEnumerator が「途中で処理を一時停止できる特殊な関数（コルーチン）」の印です
    // ========================================================
    // ▼ 順番に実行する処理（1フレームごとの完全制御版！）
    // ========================================================
    // =========================================================
    // ▼ここから：新しいターン実行システム（全10フレーム同時進行）
    // =========================================================
    private IEnumerator ExecuteTurnRoutine()
    {
        // ==========================================
        // ★追加：ターンの実行が始まったらフラグをON！
        // ==========================================
        isExecutingTurn = true;

        currentTurnCount++; // ★追加：ターン開始時にカウントを1増やす！

        // ==========================================
        // ★追加：TOD（40ターン制限）と割合（%）判定システム
        // ==========================================
        // 【CommandInputSystem.cs のタイムアップ判定部分】

        if (currentTurnCount > 40)
        {
            isGameOver = true;
            Debug.Log("【TIME UP】40ターン到達！判定に入ります。");

            // ★ タイムアップなので true を渡して共通処理へ丸投げ！
            StartCoroutine(ProcessRoundEnd(true));

            isExecutingTurn = false;
            playerFighter.ResetStateToNeutral();
            enemyFighter.ResetStateToNeutral();

            yield break; // ターンの処理をここで強制終了
        }

        // ==========================================
        // ★追加：ピッタリそのターンの開始時に1回だけ、熱いテキストで警告！
        // ==========================================
        if (currentTurnCount == 20)
        {
            if (actionText != null) actionText.text = "【警戒】ダメージ補正 1.3倍に上昇！";
            Debug.Log("サドンデスLv1 突入！");
        }
        else if (currentTurnCount == 25)
        {
            if (actionText != null) actionText.text = "【危険】ダメージ補正 1.5倍に上昇！！";
            Debug.Log("サドンデスLv2 突入！");
        }
        else if (currentTurnCount == 30)
        {
            if (actionText != null) actionText.text = "【致死領域】ダメージ補正 2.0倍！！！";
            Debug.Log("サドンデスLvMAX 突入！");
        }

        Debug.Log("--- ターン開始 ---");
        if (actionText != null) actionText.text = "バトル開始！";

        int currentExtraSA = 0;
        
        // 2. プレイヤーの更新
        if (playerFighter != null)
        {
            if (playerFighter.charaId == 2)
            {
                if (currentTurnCount >= 30) currentExtraSA = 3;
                else if (currentTurnCount >= 20) currentExtraSA = 2;
                else if (currentTurnCount >= 10) currentExtraSA = 1;
            }

            playerFighter.extraSA = currentExtraSA; // ① Fighterに実際の硬さのデータを渡す

            if (playerStatusIcon != null)
            {
                playerStatusIcon.UpdateAuraByLevel(currentExtraSA); // ② コントローラーに見た目の変更を命令する
            }
        }

        currentExtraSA = 0;

        // 3. 敵の更新（敵側のStatusIconControllerの変数名に合わせてください）
        if (enemyFighter != null)
        {
            if (enemyFighter.charaId == 2)
            {
                if (currentTurnCount >= 30) currentExtraSA = 3;
                else if (currentTurnCount >= 20) currentExtraSA = 2;
                else if (currentTurnCount >= 10) currentExtraSA = 1;
            }

            enemyFighter.extraSA = currentExtraSA;

            if (enemyStatusIcon != null)
            {
                enemyStatusIcon.UpdateAuraByLevel(currentExtraSA);
            }
        }

        DecideNextEnemyAction(); // 敵の行動を決定

        if (isPlayerAutoMode)
        {
            DecidePlayerAutoAction();
        }

        float frameDuration = 0.5f; // 1フレームの秒数
        int maxTurnFrames = 10;     // 1ターンの最大フレーム数

        // 各キャラが「今、リストの何番目の技の、何フレーム目か」を管理する変数
        int pMoveIndex = 0; int pLocalFrame = 1;
        int eMoveIndex = 0; int eLocalFrame = 1;

        // 多段ヒット防止用の記憶配列
        bool[] pHasHit = new bool[10];
        bool[] eHasHit = new bool[10];

        // 全10フレームを同時に進めていくループ！
        for (int globalFrame = 1; globalFrame <= maxTurnFrames; globalFrame++)
        {
            // ----------------------------------------
            // ① プレイヤーの行動処理
            // ----------------------------------------
            if (pMoveIndex < selectedMoves.Count)
            {
                MoveData pMove = selectedMoves[pMoveIndex];
                if (pLocalFrame == 1) pHasHit = new bool[Mathf.Max(1, pMove.hitFrames.Count)]; // 技の開始時にリセット

                // ▼ 実際の行動処理（別のメソッドにまとめて呼び出す！）
                ProcessActionFrame(playerFighter, enemyFighter, pMove, pLocalFrame, pHasHit, ref currentCombo, frameDuration, true);

                pLocalFrame++;
                if (pLocalFrame > pMove.totalFrames) // 技が終わったら次の技へ
                {
                    pMoveIndex++; pLocalFrame = 1;
                }
            }

            // ----------------------------------------
            // ② 敵の行動処理
            // ----------------------------------------
            if (eMoveIndex < enemySelectedMoves.Count)
            {
                MoveData eMove = enemySelectedMoves[eMoveIndex];
                if (eLocalFrame == 1) eHasHit = new bool[Mathf.Max(1, eMove.hitFrames.Count)];

                // ▼ 敵も同じように行動処理を呼び出す！（攻撃側と防御側を逆にして渡すだけ！）
                ProcessActionFrame(enemyFighter, playerFighter, eMove, eLocalFrame, eHasHit, ref enemyCombo, frameDuration, false);

                eLocalFrame++;
                if (eLocalFrame > eMove.totalFrames)
                {
                    eMoveIndex++; eLocalFrame = 1;
                }
            }

            // ----------------------------------------
            // ★追加：ガード耐久値の自然回復（ガードしていない時だけ回復！）
            // ----------------------------------------
            if (playerFighter.charaData != null && !playerFighter.isGuarding)
            {
                playerFighter.currentGuard = Mathf.Min(playerFighter.currentGuard + playerFighter.charaData.guardRecovery, playerFighter.charaData.guardEndurance);
            }
            if (enemyFighter.charaData != null && !enemyFighter.isGuarding)
            {
                enemyFighter.currentGuard = Mathf.Min(enemyFighter.currentGuard + enemyFighter.charaData.guardRecovery, enemyFighter.charaData.guardEndurance);
            }

            // ==========================================
            // ★追加：カウンター成立時の「技のすり替え」処理
            // ==========================================
            if (playerFighter.triggerCounter) SwapToCounterMove(playerFighter, selectedMoves, pMoveIndex, ref pLocalFrame, ref pHasHit);
            if (enemyFighter.triggerCounter) SwapToCounterMove(enemyFighter, enemySelectedMoves, eMoveIndex, ref eLocalFrame, ref eHasHit);

            // ----------------------------------------
            // ★追加：毎フレームの終わりに決着判定！
            // ----------------------------------------
            if (playerFighter.currentHp <= 0 || enemyFighter.currentHp <= 0)
            {
                isGameOver = true;
                if (!isGameOver)
                {
                    StartCoroutine(ProcessRoundEnd(false));
                }
                break; // タイムラインのループ（for文）を強制的に抜け出してストップ！
            }

            // --- 1フレーム分の待機 ---
            yield return new WaitForSeconds(frameDuration);
        }

        // ターン終了時に必ず呼ぶ
        if (!isGameOver)
        {
            playerFighter.ResetStateToNeutral();
            enemyFighter.ResetStateToNeutral();
        }

        // ==========================================
        // ★追加：ターンの実行が終わったら（コマンド入力に戻る直前に）フラグをOFF！
        // ==========================================
        isExecutingTurn = false;

        Debug.Log("--- ターン終了 ---");
        if (actionText != null) actionText.text = "コマンド入力待機中...";
        ClearCommands();

        isExecutingTurn = false;

        if (isPlayerAutoMode && !isGameOver)
        {
            // すぐ次のターンが始まると早すぎるので、1秒だけ待機（見栄えのため）
            yield return new WaitForSeconds(1.0f);

            // もう一度この関数自身を呼び出して、永遠にターンを繰り返す！
            StartCoroutine(ExecuteTurnRoutine());
        }
    }

    /// <summary>
    /// カウンター成功時、現在実行中の技を「派生技」にすり替える！
    /// </summary>
    private void SwapToCounterMove(Fighter fighter, List<MoveData> moveList, int moveIndex, ref int localFrame, ref bool[] hasHit)
    {
        fighter.triggerCounter = false;
        fighter.isCounterStance = false;

        MoveData derivedMove = DataManager.Instance.GetMove(fighter.derivedMoveID);
        if (derivedMove != null)
        {
            moveList[moveIndex] = derivedMove;
            localFrame = 1; // 次のフレームから、派生技の「1フレーム目」がスタートする！
            hasHit = new bool[Mathf.Max(1, derivedMove.hitFrames.Count)]; // ヒット履歴もリセット

            if (actionText != null && fighter == playerFighter) actionText.text = $"【反撃】{derivedMove.moveName}！";
            Debug.Log($"【当身発動】{fighter.fighterName} の技が {derivedMove.moveName} に変化した！");
        }
        else
        {
            // ★追加：もしIDの文字が間違っていてデータが見つからない場合は赤字でエラーを出す！
            Debug.LogError($"【エラー】派生技のデータが見つかりません！ ID: {fighter.derivedMoveID} をCSVに登録してください！");
        }
    }

    // =========================================================
    // ★修正版：コンボリセット処理（UIの即時更新を追加！）
    // =========================================================
    private void ApplyComboReset(Fighter defender, bool isAttackerPlayer)
    {
        if (defender.charaId == 3) // 防御側がスピードキャラの場合
        {
            if (isAttackerPlayer)
            {
                // ▼敵のスピードキャラが怯んだ（プレイヤーの攻撃）
                enemyCombo = enemyCombo / 2;
                UnityEngine.Debug.Log("【スピード固有仕様】敵のコンボが半減して継続！");

                // （敵のコンボUIを更新する処理を追加）
                if (enemyComboUI != null)
                {
                    if (enemyCombo > 0)
                    {
                        float enemyCharaComboMult = enemyFighter.charaData != null ? enemyFighter.charaData.comboMultiplier : 1.0f;
                        float enemyBonusDamage = enemyCombo * enemyCharaComboMult;
                        enemyComboUI.UpdateCombo(enemyCombo, enemyBonusDamage, true);
                    }
                    else
                    {
                        enemyComboUI.ResetCombo();
                    }
                }
            }
            else
            {
                // ▼プレイヤーのスピードキャラが怯んだ（以前書いた処理）
                currentCombo = currentCombo / 2;
                UnityEngine.Debug.Log("【スピード固有仕様】プレイヤーのコンボが半減して継続！");

                if (comboUI != null)
                {
                    if (currentCombo > 0)
                    {
                        float charaComboMult = playerFighter.charaData != null ? playerFighter.charaData.comboMultiplier : 1.0f;
                        float bonusDamage = currentCombo * charaComboMult;

                        // ★ 半減した時も、もう一度ポップアップさせて目立たせる！
                        comboUI.UpdateCombo(currentCombo, bonusDamage, true);
                    }
                    else
                    {
                        // ★ 0になったらリセット（非表示）
                        comboUI.ResetCombo();
                    }
                }
            }
        }
        else
        {
            // 通常キャラの場合のリセット
            if (isAttackerPlayer)
            {
                enemyCombo = 0;
                if (enemyComboUI != null) enemyComboUI.ResetCombo(); // ★追加
            }
            else
            {
                currentCombo = 0;
                if (comboUI != null) comboUI.ResetCombo();
            }
        }
    }

    // =========================================================
    // ▼ここから：行動処理の本体（移動・攻撃・ダメージ計算など全て）
    // =========================================================
    private void ProcessActionFrame(Fighter attacker, Fighter defender, MoveData move, int currentFrame, bool[] hasHitTarget, ref int comboCount, float frameDuration, bool isPlayer)
    {
        // 1. 硬直（スタン）チェック
        if (CheckStun(attacker, isPlayer)) return;

        // 2. 状態の更新（しゃがみ・ガード・カウンター判定）
        bool isCounterFrame = UpdateStancesAndGetCounter(attacker, move, currentFrame);

        // 3. 1フレーム目の初期化（使用条件、向き、SA増加など）
        if (currentFrame == 1)
        {
            if (!ProcessFirstFrameSetup(attacker, move, isPlayer)) return; // 飛べない等の条件落ちなら終了
        }

        // 4. 移動処理（X・Y）
        ProcessMovement(attacker, move, currentFrame, frameDuration);

        // 5. ガード判定（青枠）の展開
        // ※ガード技やカウンター技の場合はここで枠を出して処理を終了する
        if (attacker.isGuarding)
        {
            ProcessGuardBox(attacker, move, isPlayer, frameDuration, isCounterFrame);
            return;
        }

        // 6. 攻撃判定（赤枠/弾）とダメージ・当たり判定の処理
        ProcessAttacks(attacker, defender, move, currentFrame, hasHitTarget, ref comboCount, frameDuration, isPlayer);
        
    }

    // =========================================================
    // ① 硬直チェック
    // =========================================================
    private bool CheckStun(Fighter attacker, bool isPlayer)
    {
        if (attacker.stunTimer > 0)
        {
            if (isPlayer && actionText != null) actionText.text = "【硬直中】うおぉっ！";
            attacker.stunTimer--;
            return true; // 動けない
        }
        return false; // 動ける
    }

    // =========================================================
    // ② 状態（しゃがみ・ガード等）の更新
    // =========================================================
    private bool UpdateStancesAndGetCounter(Fighter attacker, MoveData move, int currentFrame)
    {
        attacker.isCrouching = move.moveName.Contains("しゃがみ") || move.id == "20" || move.id == "21" || move.id == "303";

        attacker.isShieldBashing = (move.id == "702");

        bool isCounterFrame = move.moveName.Contains("カウンター") && !move.moveName.Contains("派生") && currentFrame == 1;
        attacker.isGuarding = move.moveName.Contains("ガード") || move.id == "701" || isCounterFrame;

        if (isCounterFrame)
        {
            attacker.isCounterStance = true;
            attacker.derivedMoveID = move.id.ToString().Trim() + "0"; // 派生技のID
        }
        else if (currentFrame == 1)
        {
            attacker.isCounterStance = false;
        }

        // 技の開始時（またはしゃがみガードの入力時）
        if (attacker.isCrouching && attacker.isGrounded)
        {
            attacker.SetVisualCrouch(true);
        }
        else
        {
            // しゃがみ技ではない、もしくは空中にいる場合は立ち状態にする
            attacker.SetVisualCrouch(false);
        }

        return isCounterFrame;
    }

    // =========================================================
    // ③ 1フレーム目の初期設定
    // =========================================================
    private bool ProcessFirstFrameSetup(Fighter attacker, MoveData move, bool isPlayer)
    {
        RecordMoveUsage(isPlayer, move);

        // 空中・地上の使用条件チェック
        if (move.usableLocation == 1 && !attacker.isGrounded) return false;
        if (move.usableLocation == 2 && attacker.isGrounded) return false;

        attacker.currentSA = move.saValue + attacker.extraSA;

        if (isPlayer && actionText != null) actionText.text = $"【発動】{move.moveName}";

        // 向きの変更
        if (move.moveName.Contains("右向き") || move.moveName.Contains("右移動") || move.moveName.Contains("右上"))
            attacker.SetFacingDirection(1);
        else if (move.moveName.Contains("左向き") || move.moveName.Contains("左移動") || move.moveName.Contains("左上"))
            attacker.SetFacingDirection(-1);
        else if (move.moveName.Contains("振り向き"))
            attacker.SetFacingDirection(attacker.facingDir * -1);

        return true; // 正常に発動
    }

    // =========================================================
    // ④ 移動処理
    // =========================================================
    private void ProcessMovement(Fighter attacker, MoveData move, int currentFrame, float frameDuration)
    {
        // X軸の移動
        for (int i = 0; i < move.moveX.Count; i++)
        {
            if (move.moveStartX[i] == currentFrame)
            {
                if (move.moveName.Contains("右移動")) attacker.SetFacingDirection(1);
                if (move.moveName.Contains("左移動")) attacker.SetFacingDirection(-1);

                float actualMoveX = move.moveX[i] * attacker.facingDir;
                float timeToMove = (move.moveEndX[i] - move.moveStartX[i] + 1) * frameDuration;
                attacker.MoveX(actualMoveX, timeToMove);
            }
        }

        // Y軸（ジャンプ）の移動
        for (int i = 0; i < move.moveY.Count; i++)
        {
            if (move.moveStartY[i] == currentFrame)
            {
                attacker.Jump(move.moveY[i]);
            }
        }
    }

    // =========================================================
    // ⑤ ガード判定（青枠）の展開処理
    // =========================================================
    private void ProcessGuardBox(Fighter attacker, MoveData move, bool isPlayer, float frameDuration, bool isCounterFrame)
    {
        Vector3 pPos = attacker.transform.position;
        float mult = attacker.distanceMultiplier;
        float pFeetY = pPos.y - ((attacker.bodyHeight * mult) / 2f);
        int dir = attacker.facingDir;

        MoveData boxData = move;
        if (isCounterFrame)
        {
            MoveData defaultGuard = DataManager.Instance.GetMove("8");
            if (defaultGuard != null) boxData = defaultGuard;
        }

        float valA = pPos.x + ((boxData.hitboxMinX.Count > 0 ? boxData.hitboxMinX[0] : 0f) * mult * 0.4f * dir);
        float valB = pPos.x + ((boxData.hitboxMaxX.Count > 0 ? boxData.hitboxMaxX[0] : 1f) * mult * 0.4f * dir);
        float minX = Mathf.Min(valA, valB);
        float maxX = Mathf.Max(valA, valB);

        float minY = pFeetY + ((boxData.hitboxMinY.Count > 0 ? boxData.hitboxMinY[0] : 0f) * mult * 0.4f);
        float maxY = pFeetY + ((boxData.hitboxMaxY.Count > 0 ? boxData.hitboxMaxY[0] : 1.5f) * mult * 0.4f);

        attacker.guardBox[0] = minX;
        attacker.guardBox[1] = maxX;
        attacker.guardBox[2] = minY;
        attacker.guardBox[3] = maxY;

        if (showHitbox)
        {
            // ※ isProjectile や projectileSpeedX はそちらの環境の変数名に合わせてください
            float projSpeedX = move.isProjectile ? move.projectileSpeedX : 0f;

            activeHitboxes.Add(new DebugHitbox
            {
                minX = minX,
                maxX = maxX,
                minY = minY,
                maxY = maxY,
                timer = frameDuration,
                isGuard = true,
                owner = attacker,
                sourceMove = move,
                speedX = projSpeedX,
                facingDir = dir
            });
            // ==========================================
            // ★ ここに追加！ガード判定を青いブロックで可視化する
            // ==========================================
            float width = Mathf.Abs(maxX - minX);
            float height = Mathf.Abs(maxY - minY);
            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            Vector2 guardPos = new Vector2(centerX, centerY);
            Vector2 guardSize = new Vector2(width, height);

            // ガードは青色（Color.blue）で表示！
            HitboxVisualizer.Instance.ShowHitbox(guardPos, guardSize, Color.blue, frameDuration);
            // ==========================================
        }
    }

    // =========================================================
    // ⑥ 攻撃判定・ダメージ処理（DamageInfo 対応版）
    // =========================================================
    private void ProcessAttacks(Fighter attacker, Fighter defender, MoveData move, int currentFrame, bool[] hasHitTarget, ref int comboCount, float frameDuration, bool isPlayer)
    {
        for (int hitIndex = 0; hitIndex < move.hitFrames.Count; hitIndex++)
        {
            int startFrame = move.hitFrames[hitIndex];
            int duration = (hitIndex < move.activeFrames.Count && move.activeFrames[hitIndex] > 0) ? move.activeFrames[hitIndex] : 1;
            int endFrame = startFrame + duration - 1;

            if (currentFrame >= startFrame && currentFrame <= endFrame)
            {
                if (attacker != null && defender != null && hitIndex < move.damages.Count)
                {
                    // =====================================
                    // 1. 判定枠の計算
                    // =====================================
                    Vector3 pPos = attacker.transform.position;
                    float mult = attacker.distanceMultiplier;
                    float pFeetY = pPos.y - ((attacker.bodyHeight * mult) / 2f);
                    int dir = attacker.facingDir;

                    int idxMinX = Mathf.Min(hitIndex, Mathf.Max(0, move.hitboxMinX.Count - 1));
                    int idxMaxX = Mathf.Min(hitIndex, Mathf.Max(0, move.hitboxMaxX.Count - 1));
                    int idxMinY = Mathf.Min(hitIndex, Mathf.Max(0, move.hitboxMinY.Count - 1));
                    int idxMaxY = Mathf.Min(hitIndex, Mathf.Max(0, move.hitboxMaxY.Count - 1));

                    float valA = pPos.x + ((move.hitboxMinX.Count > 0 ? move.hitboxMinX[idxMinX] : 0) * mult * 0.4f * dir);
                    float valB = pPos.x + ((move.hitboxMaxX.Count > 0 ? move.hitboxMaxX[idxMaxX] : 1) * mult * 0.4f * dir);
                    float minX = Mathf.Min(valA, valB);
                    float maxX = Mathf.Max(valA, valB);
                    float minY = pFeetY + ((move.hitboxMinY.Count > 0 ? move.hitboxMinY[idxMinY] : 0) * mult * 0.4f);
                    float maxY = pFeetY + ((move.hitboxMaxY.Count > 0 ? move.hitboxMaxY[idxMaxY] : 1) * mult * 0.4f);

                    if (attacker.isGuarding)
                    {
                        attacker.guardBox[0] = minX; attacker.guardBox[1] = maxX;
                        attacker.guardBox[2] = minY; attacker.guardBox[3] = maxY;
                        if (showHitbox) activeHitboxes.Add(new DebugHitbox { minX = minX, maxX = maxX, minY = minY, maxY = maxY, timer = frameDuration, isGuard = true });

                        

                        return;
                    }

                    if (move.isProjectile)
                    {
                        float width = Mathf.Abs(maxX - minX);
                        float height = Mathf.Abs(maxY - minY);
                        float centerX = (minX + maxX) / 2f;
                        float centerY = (minY + maxY) / 2f;

                        GameObject blockObj = null;
                        if (projectilePrefab != null)
                        {
                            blockObj = Instantiate(projectilePrefab);
                            blockObj.transform.position = new Vector3(centerX, centerY, 0);
                            blockObj.transform.localScale = new Vector3(width, height, 1);
                        }

                        activeProjectiles.Add(new ProjectileData
                        {
                            owner = attacker,
                            sourceMove = move,
                            minX = minX,
                            maxX = maxX,
                            minY = minY,
                            maxY = maxY,
                            speedX = move.projectileSpeedX * 160f,
                            speedY = move.projectileSpeedY * 160f,
                            facingDir = dir,
                            timer = move.projectileLifeTime,
                            hasHit = false,
                            visualBlock = blockObj
                        });
                        continue;
                    }
                    else
                    {
                        if (showHitbox)
                        {
                            activeHitboxes.Add(new DebugHitbox
                            {
                                minX = minX,
                                maxX = maxX,
                                minY = minY,
                                maxY = maxY,
                                timer = frameDuration,
                                isGuard = false,
                                owner = attacker,
                                sourceMove = move,
                                speedX = 0f,
                                facingDir = dir
                            });

                            // ==========================================
                            // ★ 修正：CSVのデータ（min/max）から中心位置とサイズを計算する！
                            // ==========================================
                            float width = Mathf.Abs(maxX - minX);   // 横幅
                            float height = Mathf.Abs(maxY - minY);  // 縦幅
                            float centerX = (minX + maxX) / 2f;     // 中心のX座標
                            float centerY = (minY + maxY) / 2f;     // 中心のY座標

                            Vector2 attackPos = new Vector2(centerX, centerY);
                            Vector2 attackSize = new Vector2(width, height);

                            // 判定ブロックを表示！（フレームの持続時間だけ表示すると綺麗です）
                            HitboxVisualizer.Instance.ShowHitbox(attackPos, attackSize, Color.red, frameDuration);
                        }
                    }

                    // =====================================
                    // 2. 当たり判定（ヒット）チェック
                    // =====================================
                    float[] eBox = defender.GetHurtbox();
                    bool isHitX = (maxX >= eBox[0]) && (minX <= eBox[1]);
                    bool isHitY = (maxY >= eBox[2]) && (minY <= eBox[3]);

                    if (isHitX && isHitY && !hasHitTarget[hitIndex])
                    {
                        hasHitTarget[hitIndex] = true;

                        bool isGuarded = false;
                        if (defender.isGuarding && !move.isThrow)
                        {
                            float[] gBox = defender.guardBox;
                            bool guardHitX = (maxX >= gBox[0]) && (minX <= gBox[1]);
                            bool guardHitY = (maxY >= gBox[2]) && (minY <= gBox[3]);
                            if (guardHitX && guardHitY) isGuarded = true;
                        }

                        if (isGuarded)
                        {
                            // -----------------------------
                            // ① 防御成功（ガード/カウンター）
                            // -----------------------------
                            if (defender.isCounterStance)
                            {
                                defender.triggerCounter = true;
                                Debug.Log($"【当身成功】{defender.fighterName} が相手の攻撃を受け止めた！");
                            }
                            else
                            {
                                float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
                                float baseDamage = move.damages[hitIndex];
                                int chipDamage = Mathf.Max(1, Mathf.RoundToInt((baseDamage * charaDmgMult) * 0.1f));

                                // ★ DamageInfoを使って削りダメージを渡す！
                                DamageInfo chipInfo = new DamageInfo
                                {
                                    damage = chipDamage,
                                    moveId = move.id,
                                    attacker = attacker,
                                    isReflect = false,
                                    isSAActive = true
                                };
                                defender.TakeDamage(chipInfo);

                                if (defender == playerFighter && playerHpGauge != null)
                                {
                                    playerHpGauge.UpdateHP(playerFighter.currentHp); // 1Pが殴られたら1Pのゲージを減らす
                                }
                                else if (defender == enemyFighter && enemyHpGauge != null)
                                {
                                    enemyHpGauge.UpdateHP(enemyFighter.currentHp);   // 2Pが殴られたら2Pのゲージを減らす
                                }

                                defender.currentGuard -= chipDamage;

                                if (defender.currentHp <= 0 && !isGameOver)
                                {
                                    StartCoroutine(ProcessRoundEnd(false));
                                    return; // ガードクラッシュ処理などに進ませない！
                                }

                                RecordMoveBlock(isPlayer, move);

                                if (defender.currentGuard <= 0) // ガードクラッシュ
                                {
                                    defender.currentGuard = 0;
                                    defender.isGuarding = false;
                                    defender.AddStun(5);
                                    ApplyComboReset(defender, isPlayer);
                                    defender.MoveX(-1.0f * attacker.facingDir, 0.2f);
                                }
                                else
                                {
                                    if (defender.charaId == 7) defender.MoveX(0f, 0.2f);
                                    else defender.MoveX(-0.5f * attacker.facingDir, 0.2f);

                                    // シールダーの大ガード反射処理も DamageInfo 経由にする！
                                    if (defender.charaId == 7 && defender.isGuarding)
                                    {
                                        int reflectDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.03f));
                                        DamageInfo reflectInfo = new DamageInfo
                                        {
                                            damage = reflectDamage,
                                            moveId = "701",
                                            attacker = defender,
                                            isReflect = true,
                                            isSAActive = false
                                        };
                                        attacker.TakeDamage(reflectInfo);

                                        if (attacker.currentHp <= 0)
                                        {
                                            if (!isGameOver)
                                            {
                                                StartCoroutine(ProcessRoundEnd(false));
                                            }
                                            return; // isGameOverがtrueでもfalseでも、HPが0なら絶対にここで処理を強制終了する！
                                        }

                                        MoveData guardMove = DataManager.Instance.GetMove("701");
                                        if (guardMove != null)
                                        {
                                            // 攻撃者(attacker)が isPlayer なら、防御者(defender)は !isPlayer
                                            RecordMoveHit(!isPlayer, guardMove, reflectDamage);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // -----------------------------
                            // ② 攻撃ヒット時
                            // -----------------------------
                            int addCombo = (move.comboCounts != null && move.comboCounts.Count > hitIndex) ? move.comboCounts[hitIndex] : 1;
                            // ★ スピードキャラ（ID: 3）ならコンボ加算を2倍にする！
                            if (attacker.charaId == 3)
                            {
                                addCombo = addCombo * 2;
                            }
                            if (attacker == playerFighter) currentCombo += addCombo; else enemyCombo += addCombo;

                            float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
                            float charaComboMult = attacker.charaData != null ? attacker.charaData.comboMultiplier : 1.0f;
                            float saDamageBonus = attacker.charaData != null ? (attacker.currentSA * attacker.charaData.saBonus) : 0f;
                            float baseDamage = move.damages[hitIndex];

                            int rawDamage = Mathf.RoundToInt((baseDamage * charaDmgMult) + (comboCount * charaComboMult) + saDamageBonus);

                            int attackerSABreak = move.saBreak;
                            int defenderSA = defender.currentSA;
                            bool isSAActive = !move.isThrow && (attackerSABreak < defenderSA);

                            RecordMoveHit(isPlayer, move, rawDamage);
                            StartCoroutine(HitStopRoutine(Mathf.Clamp(rawDamage / 200f, 0.05f, 0.5f)));

                            // ==========================================
                            // ★ 生のダメージとSA発動フラグをパックにして、相手に投げる！
                            // ==========================================
                            DamageInfo info = new DamageInfo
                            {
                                damage = rawDamage,
                                moveId = move.id,
                                attacker = attacker,
                                isReflect = false,
                                isSAActive = isSAActive
                            };
                            defender.TakeDamage(info);

                            if (defender == playerFighter && playerHpGauge != null)
                            {
                                playerHpGauge.UpdateHP(playerFighter.currentHp); // 1Pが殴られたら1Pのゲージを減らす
                            }
                            else if (defender == enemyFighter && enemyHpGauge != null)
                            {
                                enemyHpGauge.UpdateHP(enemyFighter.currentHp);   // 2Pが殴られたら2Pのゲージを減らす
                            }

                            if (isPlayer && comboUI != null)
                            {
                                float bonusDamage = comboCount * charaComboMult;
                                comboUI.UpdateCombo(currentCombo, bonusDamage);
                            }

                            // 敵の攻撃がプレイヤーにヒットした時の処理の中
                            if (!isPlayer && enemyComboUI != null)
                            {
                                // 敵キャラのコンボ倍率を取得（敵のFighterデータなどから）
                                float enemyCharaComboMult = enemyFighter.charaData != null ? enemyFighter.charaData.comboMultiplier : 1.0f;
                                float enemyBonusDamage = enemyCombo * enemyCharaComboMult;

                                // 敵側のコンボUIを更新
                                enemyComboUI.UpdateCombo(enemyCombo, enemyBonusDamage);
                            }

                            if (defender.currentHp <= 0 && !isGameOver)
                            {
                                StartCoroutine(ProcessRoundEnd(false));
                                return;
                            }

                            // アーマーブレイク（怯み）時の処理
                            if (!isSAActive)
                            {
                                defender.currentSA = 0;
                                ApplyComboReset(defender, isPlayer);

                                int idxStun = Mathf.Min(hitIndex, Mathf.Max(0, move.hitStunFrames.Count - 1));
                                int stunFrames = move.hitStunFrames.Count > 0 ? move.hitStunFrames[idxStun] : 0;
                                if (stunFrames > 0) defender.AddStun(stunFrames);

                                int idxKbX = Mathf.Min(hitIndex, Mathf.Max(0, move.knockbackX.Count - 1));
                                int idxKbY = Mathf.Min(hitIndex, Mathf.Max(0, move.knockbackY.Count - 1));
                                float kbX = move.knockbackX.Count > 0 ? move.knockbackX[idxKbX] : 0f;
                                float kbY = move.knockbackY.Count > 0 ? move.knockbackY[idxKbY] : 0f;

                                float enemyWeight = (defender.charaData != null && defender.charaData.weight > 0f) ? defender.charaData.weight : 1.0f;
                                float weightMult = 1.0f / enemyWeight;

                                // ==================================================
                                // ★ ここから変更：吹き飛ばす方向（finalDir）を決定する
                                // ==================================================
                                float finalDir;

                                // ゴム風船（ID: 803）など、放射状に吹き飛ばしたい技のIDを指定
                                if (move.id == "803")
                                {
                                    // 相対座標による吹き飛ばし（相手のX - 自分のX）
                                    float relativeX = defender.transform.position.x - attacker.transform.position.x;
                                    // 相手が右にいれば1(右へ)、左にいれば-1(左へ)
                                    finalDir = (relativeX >= 0) ? 1.0f : -1.0f;
                                    //UnityEngine.Debug.Log($"<color=yellow>【ゴム風船ヒット！】</color> 技ID: {move.id}");
                                    //UnityEngine.Debug.Log($"攻撃者のX: {attacker.transform.position.x} | 防御者のX: {defender.transform.position.x}");
                                    //UnityEngine.Debug.Log($"相対距離(relativeX): {relativeX} => 決定した finalDir: {finalDir} (元の facingDir: {attacker.facingDir})");
                                }
                                else
                                {
                                    // 通常の打撃技は今まで通り「攻撃者の向いている方向」
                                    finalDir = attacker.facingDir;
                                }

                                // ★ attacker.facingDir を finalDir に変更！
                                if (kbX != 0) defender.MoveX(kbX * finalDir * weightMult, 0.2f);
                                // ==================================================

                                if (kbY != 0) defender.Knockup(kbY * 80f * weightMult);
                            }
                        }
                    }
                }
            }
        }
    }

    // ==========================================
    // ★追加：飛び道具・罠が当たった時の「超リッチなヒット処理」
    // ==========================================
    // =========================================================
    // 飛び道具ヒット時の処理（DamageInfo 対応版）
    // =========================================================
    private void ApplyProjectileHit(Fighter attacker, Fighter defender, MoveData move)
    {
        bool isPlayerAttacking = (attacker == playerFighter);

        // 1. ガードとカウンターの判定
        if (defender.isGuarding && !move.isThrow)
        {
            if (defender.isCounterStance)
            {
                defender.triggerCounter = true;
                UnityEngine.Debug.Log($"【当身成功】{defender.fighterName} が飛び道具をかき消した！");
                return; // カウンター成功時は無傷で終了
            }
            else
            {
                // ガードクラッシュと削りダメージの処理
                float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
                float baseDmg = move.damages.Count > 0 ? move.damages[0] : 10;
                int chipDamage = Mathf.Max(1, Mathf.RoundToInt((baseDmg * charaDmgMult) * 0.1f));

                // ★ 削りダメージも DamageInfo を使って渡す！
                DamageInfo chipInfo = new DamageInfo
                {
                    damage = chipDamage,
                    moveId = move.id.ToString(),
                    attacker = attacker,
                    isReflect = false,
                    isSAActive = true
                };
                defender.TakeDamage(chipInfo);

                if (defender == playerFighter && playerHpGauge != null) playerHpGauge.UpdateHP(playerFighter.currentHp);
                else if (defender == enemyFighter && enemyHpGauge != null) enemyHpGauge.UpdateHP(enemyFighter.currentHp);

                defender.currentGuard -= chipDamage;

                if (defender.currentHp <= 0 && !isGameOver)
                {
                    StartCoroutine(ProcessRoundEnd(false));
                    return; // ガードクラッシュ処理などに進ませない！
                }

                if (defender.currentGuard <= 0) // ガードクラッシュ発生
                {
                    defender.currentGuard = 0;
                    defender.isGuarding = false;
                    defender.AddStun(5);
                    ApplyComboReset(defender, isPlayerAttacking);
                    defender.MoveX(1.0f * attacker.facingDir, 0.2f);
                    UnityEngine.Debug.Log($"【ガードブレイク】飛び道具でガードクラッシュ！");
                }
                else // ガード成功
                {
                    if (defender.charaId == 7)
                    {
                        defender.MoveX(0f, 0f); // 全く動かない（ビシッと防ぐ）
                        Debug.Log($"【ガード成功】{defender.fighterName} は弾をビシッと防いだ！ノックバックなし！");

                        // ★追加：飛び道具に対してもシールダーの大ガード反射（3%）を行う場合
                        if (defender.isGuarding)
                        {
                            int reflectDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.03f));
                            DamageInfo reflectInfo = new DamageInfo
                            {
                                damage = reflectDamage,
                                moveId = "701",
                                attacker = defender,
                                isReflect = true,
                                isSAActive = false
                            };
                            attacker.TakeDamage(reflectInfo);

                            if (attacker.currentHp <= 0 && !isGameOver)
                            {
                                StartCoroutine(ProcessRoundEnd(false));
                                return; // ガードクラッシュ処理などに進ませない！
                            }

                            MoveData guardMove = DataManager.Instance.GetMove("701");
                            if (guardMove != null)
                            {
                                // 攻撃者(attacker)が isPlayer なら、防御者(defender)は !isPlayer
                                RecordMoveHit(!isPlayerAttacking, guardMove, reflectDamage);
                            }
                        }
                    }
                    else
                    {
                        defender.MoveX(-0.5f * attacker.facingDir, 0.2f);
                    }
                }

                // 弾がガードされたことを記録して終了
                RecordMoveBlock(isPlayerAttacking, move);
                return;
            }
        }

        // 2. コンボの加算
        int addCombo = (move.comboCounts != null && move.comboCounts.Count > 0) ? move.comboCounts[0] : 1;

        int currentComboCount = 0;

        if (isPlayerAttacking)
        {
            currentCombo += addCombo;
            currentComboCount = currentCombo;
        }
        else
        {
            enemyCombo += addCombo;
            currentComboCount = enemyCombo;
        }

        // 3. ダメージ計算（SA補正・コンボ補正・サドンデス補正あり）
        float charaDmgMult2 = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
        float charaComboMult = attacker.charaData != null ? attacker.charaData.comboMultiplier : 1.0f;
        float saDamageBonus = attacker.charaData != null ? (attacker.currentSA * attacker.charaData.saBonus) : 0f;
        float baseDamage = move.damages.Count > 0 ? move.damages[0] : 10;

        // サドンデスモードの倍率計算
        float suddenDeathMultiplier = 1.0f;
        if (currentTurnCount >= 30) suddenDeathMultiplier = 2.5f;
        else if (currentTurnCount >= 25) suddenDeathMultiplier = 2.0f;
        else if (currentTurnCount >= 20) suddenDeathMultiplier = 1.5f;

        // サドンデス倍率を含めた「生の最終ダメージ」を計算
        int rawDamage = Mathf.RoundToInt(((baseDamage * charaDmgMult2) + (currentComboCount * charaComboMult) + saDamageBonus) * suddenDeathMultiplier);

        // SAで耐えられているかの判定
        int attackerSABreak = move.saBreak;
        int defenderSA = defender.currentSA;
        bool isSAActive = !move.isThrow && (attackerSABreak < defenderSA);

        // ★SAによるダメージ軽減計算は、Fighter側の TakeDamage の中でやってくれるのでここでは不要です！

        // 弾がヒットしたこととダメージを記録！
        RecordMoveHit(isPlayerAttacking, move, rawDamage);

        // ==========================================
        // ★ ダメージ情報をパックにして相手に投げる！
        // ==========================================
        DamageInfo info = new DamageInfo
        {
            damage = rawDamage,
            moveId = move.id.ToString(),
            attacker = attacker,
            isReflect = false,
            isSAActive = isSAActive
        };
        defender.TakeDamage(info);

        if (defender == playerFighter && playerHpGauge != null) playerHpGauge.UpdateHP(playerFighter.currentHp);
        else if (defender == enemyFighter && enemyHpGauge != null) enemyHpGauge.UpdateHP(enemyFighter.currentHp);

        // UIの更新（プレイヤー攻撃時）
        if (isPlayerAttacking && comboUI != null)
        {
            float bonusDamage = currentComboCount * charaComboMult;
            comboUI.UpdateCombo(currentComboCount, bonusDamage);
        }

        // 敵の攻撃がプレイヤーにヒットした時の処理の中
        if (!isPlayerAttacking && enemyComboUI != null)
        {
            // 敵キャラのコンボ倍率を取得（敵のFighterデータなどから）
            float enemyCharaComboMult = enemyFighter.charaData != null ? enemyFighter.charaData.comboMultiplier : 1.0f;
            float enemyBonusDamage = enemyCombo * enemyCharaComboMult;

            // 敵側のコンボUIを更新
            enemyComboUI.UpdateCombo(enemyCombo, enemyBonusDamage);
        }

        // 4. ヒットストップとKO演出
        if (defender.currentHp <= 0 && !isGameOver)
        {
            StartCoroutine(ProcessRoundEnd(false));
            return;
        }
        else if (!isGameOver)
        {
            StartCoroutine(HitStopRoutine(Mathf.Clamp(rawDamage / 200f, 0.05f, 0.5f)));
        }

        // 5. SA破壊・ノックバック・スタンの処理
        if (!isSAActive)
        {
            defender.currentSA = 0; // アーマー破壊
            ApplyComboReset(defender, isPlayerAttacking);

            int stunFrames = move.hitStunFrames.Count > 0 ? move.hitStunFrames[0] : 0;
            if (stunFrames > 0) defender.AddStun(stunFrames);

            float kbX = move.knockbackX.Count > 0 ? move.knockbackX[0] : 0f;
            float kbY = move.knockbackY.Count > 0 ? move.knockbackY[0] : 0f;
            float enemyWeight = (defender.charaData != null && defender.charaData.weight > 0f) ? defender.charaData.weight : 1.0f;
            float weightMult = 1.0f / enemyWeight;

            if (kbX != 0) defender.MoveX(kbX * attacker.facingDir * weightMult, 0.2f);
            if (kbY != 0) defender.Knockup(kbY * 80f * weightMult);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showHitbox) return;

        foreach (var box in activeHitboxes)
        {
            Vector3 center = new Vector3((box.minX + box.maxX) / 2f, (box.minY + box.maxY) / 2f, 0);
            Vector3 size = new Vector3(box.maxX - box.minX, box.maxY - box.minY, 0.1f);

            // ★修正：isGuard が true なら「青色」、false なら「赤色」にする！
            if (box.isGuard)
            {
                Gizmos.color = new Color(0f, 0f, 1f, 0.4f); // 半透明の青
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.blue; // 濃い青の枠線
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f); // 半透明の赤
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.red; // 濃い赤の枠線
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    // ==========================================
    // ★追加：勝敗を決める処理
    // ==========================================
    private IEnumerator ProcessRoundEnd(bool isTimeUp)
    {
        isGameOver = true; // 入力やゲームの進行を止める

        // ==========================================
        // 🚨 フリーズ対策：時間が止まっていたら絶対に元に戻す！
        // ==========================================
        Time.timeScale = 1.0f;

        if (resultText != null) resultText.gameObject.SetActive(true);

        // 1. 理由の表示（KO か TIME UP か）
        if (isTimeUp)
        {
            Debug.Log("【決着】TIME UP!!");
            resultText.text = "<color=yellow>TIME UP</color>";
        }
        else
        {
            Debug.Log("【決着】K.O.!!");
            resultText.text = "<color=red>K.O.</color>";
        }

        // 文字を2秒間見せる
        yield return new WaitForSeconds(2.0f);

        // ==========================================
        // 2. 勝敗判定（HPが多い方が勝ち！）
        // ==========================================
        float playerHpPercent = (float)playerFighter.currentHp / playerFighter.maxHp;
        float enemyHpPercent = (float)enemyFighter.currentHp / enemyFighter.maxHp;
        if (playerFighter.currentHp > enemyFighter.currentHp)
        {
            p1Wins++;
            resultText.text = "<color=red>1P WIN</color>";
        }
        else if (enemyFighter.currentHp > playerFighter.currentHp)
        {
            p2Wins++;
            resultText.text = "<color=blue>2P WIN</color>";
        }
        else
        {
            // HPが全く同じ場合
            resultText.text = "<color=yellow>DRAW</color>";
        }

        // 勝者の文字を3秒間見せる
        yield return new WaitForSeconds(3.0f);
        Debug.Log($"【判定チェック】1Pの勝利数: {p1Wins} / 2Pの勝利数: {p2Wins} / 必要勝利数: {requiredWins}");
        // ==========================================
        // 3. 最終決着か、次ラウンドか？
        // ==========================================
        if (p1Wins >= requiredWins || p2Wins >= requiredWins)
        {
            if (p1Wins > p2Wins) resultText.text = "<color=red>YOU WIN!!</color>";
            else if (p2Wins > p1Wins) resultText.text = "<color=blue>YOU LOSE...</color>";
            else resultText.text = "DRAW MATCH";

            StartCoroutine(DelayedShowResult(3.0f)); // 今までのリザルトへ
        }
        else
        {
            StartCoroutine(NextRoundRoutine(0f)); // 次ラウンドへ！
        }
    }

    private IEnumerator NextRoundRoutine(float delay)
    {
        // 指定された時間（3秒）だけ「1P WIN」などの文字を出したまま待つ
        yield return new WaitForSeconds(delay);

        // 次のラウンドへ進む
        currentRound++;

        // 真ん中のテキストを一旦消す
        if (resultText != null) resultText.gameObject.SetActive(false);

        Debug.Log($"Round {currentRound} スタート準備...");

        if (activeProjectiles != null)
        {
            foreach (var proj in activeProjectiles)
            {
                if (proj.visualBlock != null)
                {
                    Destroy(proj.visualBlock); // 弾のGameObjectを破壊
                }
            }
            activeProjectiles.Clear(); // リストを空っぽにして記憶を消す
        }

        currentTurnCount = 0;
        // ==========================================
        // ★ リセット処理（HP、位置、状態）
        // ==========================================
        // 1. HPとSAゲージを全回復
        playerFighter.currentHp = playerFighter.maxHp;
        enemyFighter.currentHp = enemyFighter.maxHp;

        // ※SAゲージを持ち越さない場合は0にする（持ち越すゲームも多いです）
        playerFighter.currentSA = 0; 
        enemyFighter.currentSA = 0;

        currentCombo = 0;
        enemyCombo = 0;

        comboUI.ResetCombo();
        enemyComboUI.ResetCombo();

        // 2. UIのゲージ表示を更新
        if (playerHpGauge != null) playerHpGauge.InitHP(playerFighter.currentHp);
        if (enemyHpGauge != null) enemyHpGauge.InitHP(enemyFighter.currentHp);

        // 3. キャラクターの位置を開幕の距離に戻す（※X座標はご自身のゲームに合わせて調整してください）
        playerFighter.transform.position = new Vector3(75.0f, playerFighter.transform.position.y, 0);
        enemyFighter.transform.position = new Vector3(325.0f, enemyFighter.transform.position.y, 0);

        // 4. キャラクターの向きや状態をリセット
        playerFighter.ResetStateToNeutral();
        enemyFighter.ResetStateToNeutral();
        playerFighter.facingDir = 1;  // 右向き
        enemyFighter.facingDir = -1;  // 左向き

        // もしタイムアップ用のタイマーがあれば、ここで初期値（99秒など）に戻す処理を入れます
        // timeRemaining = 99f; 

        // ちょっとだけ待ってからバトル再開！
        yield return new WaitForSeconds(0.5f);
        Debug.Log("FIGHT!!");

        // ゲーム進行を再開
        isGameOver = false;
    }

    private IEnumerator DelayedShowResult(float delay)
    {
        // 指定された秒数（delay）だけ待機
        yield return new WaitForSeconds(delay);

        // リザルトを表示
        ShowResultUI();
    }

    // ==========================================
    // ★追加：ゲームをリセットして最初からやり直す処理
    // ==========================================
    public void ResetGame()
    {
        // 現在開いているシーンの名前を取得して、丸ごと再読み込みする（完全リセット）
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        Debug.Log("【リセット】ゲームを初期状態に戻しました！");
    }

    // --- キャラクターのステータスを初期化する専用メソッド ---
    private void ResetFighterState(Fighter f, Vector3 startPos, int startDir)
    {
        if (f == null) return;

        // HPとガード耐久値を最大に戻す
        f.currentHp = f.maxHp;
        if (f.charaData != null) f.currentGuard = f.charaData.guardEndurance;

        // 状態異常の解除
        f.stunTimer = 0;
        f.currentSA = 0;
        f.isCrouching = false;
        f.isGuarding = false;
        f.isCounterStance = false;
        f.triggerCounter = false;

        // 位置と向きを初期位置に戻す（※初期位置のX座標が違う場合は、上の -3f や 3f を調整してください）
        f.transform.position = startPos;
        f.SetFacingDirection(startDir);

        // ※もしFighter側にHPバー(スライダー)を更新するメソッドがあれば、ここで呼ぶとUIも全回復します！
        f.UpdateHPBar();
    }

    // ==========================================
    // ★追加：現実の時間を基準にして、ゲーム内の時間を一瞬だけ止める処理
    // ==========================================
    private System.Collections.IEnumerator HitStopRoutine(float stopTime)
    {
        // ==========================================
        // ★追加：オートモードなら、ヒットストップ演出をスキップして即終了！
        // ==========================================
        if (isPlayerAutoMode)
        {
            yield break; // ← これが IEnumerator 用の「return」です！
        }

        // Unityの世界の時間の進み方を「0（完全停止）」にする
        Time.timeScale = 0f;

        // 現実の時間（timeScaleの影響を受けない時計）で、指定した秒数だけ待つ
        yield return new WaitForSecondsRealtime(stopTime);

        // 時間の進み方を「1（通常スピード）」に戻す
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 敵の行動を自動で決定する（テスト用固定スクリプト）
    /// </summary>// ==========================================
    // ★修正：引数で pattern を受け取るようにしました！
    // ==========================================
    private void DecideEnemyMoves(EnemyPatternData pattern)
    {
        // ★門番1：キャラIDの不一致を検知して警告＆完全ブロック！
        if (pattern.usableCharID != 0 && pattern.usableCharID != enemyFighter.charaId)
        {
            UnityEngine.Debug.LogWarning($"【AI警告】不正な発動！ID:{enemyFighter.charaId} の敵が、専用外のパターンを使おうとしました！行動を強制キャンセルします。");
            return;
        }

        // ★門番2：距離の不一致を検知して警告！
        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);
        if (distance < pattern.minDistance || distance > pattern.maxDistance)
        {
            UnityEngine.Debug.LogWarning($"【AI警告】距離外の発動！現在距離:{distance}ですが、発動条件({pattern.minDistance}〜{pattern.maxDistance})を満たしていません！");
            return;
        }

        // ここから先は、渡された pattern を使って技をセットするだけ！
        enemySelectedMoves.Clear();
        int totalFrames = 0;

        // ★修正：chosenPattern ではなく、引数でもらった pattern をそのまま使います
        foreach (string moveId in pattern.moveIds)
        {
            MoveData move = DataManager.Instance.GetMove(moveId);
            if (move != null)
            {
                if (totalFrames + move.totalFrames > 10) break;

                enemySelectedMoves.Add(move);
                totalFrames += move.totalFrames;
            }
        }

        // 足りないフレームをニュートラルで埋める
        while (totalFrames < 10)
        {
            MoveData neutral = DataManager.Instance.GetMove("5");
            if (neutral != null)
            {
                enemySelectedMoves.Add(neutral);
                totalFrames += neutral.totalFrames;
            }
            else break;
        }

        UnityEngine.Debug.Log($"現在距離:{distance}、敵が行動パターン『{pattern.patternName}』を決定しました！");
    }

    private void DecideNextEnemyAction()
    {
        // ==========================================
        // ★追加：敵AIだけは、自分のターン（行動決定）の直前に必ずプレイヤーの方を向く！
        // ==========================================
        if (enemyFighter != null && playerFighter != null)
        {
            enemyFighter.LookAtTarget(playerFighter.transform.position.x);
        }

        // 1. プレイヤーとの距離を測る（X座標の差の絶対値）
        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);

        // 2. 「今、自分が使える技」だけを入れる空のリストを作る
        System.Collections.Generic.List<EnemyPatternData> validPatterns = new System.Collections.Generic.List<EnemyPatternData>();

        // 3. DataManagerにある全てのパターンから、条件に合うものを探す
        foreach (var pattern in DataManager.Instance.enemyPatterns)
        {
            // ==========================================
            // ★追加：CSVの「空行」や「見出し行」をここで弾く！（透明人間対策）
            // ==========================================
            if (string.IsNullOrEmpty(pattern.patternName)) continue;

            // IDが合わない（専用外の）パターンは最初から候補に入れない
            if (pattern.usableCharID != 0 && pattern.usableCharID != enemyFighter.charaId) continue;

            // 今の距離が、パターンの発動条件に収まっているかチェック
            if (distance >= pattern.minDistance && distance <= pattern.maxDistance)
            {
                // 条件をクリアしたパターンだけを候補に追加！
                validPatterns.Add(pattern);
            }
        }

        // ==========================================
        // 4. 候補の中から「適正距離の中央に近いほど高確率」で選んで実行する！
        // ==========================================
        if (validPatterns.Count > 0)
        {
            float totalWeight = 0f;
            System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();

            // 【ステップA】各パターンの「重み（逆数）」を計算する
            for (int i = 0; i < validPatterns.Count; i++)
            {
                var p = validPatterns[i];
                // 中央の距離を計算
                float center = (p.minDistance + p.maxDistance) / 2f;
                // 現在地との差（絶対値）を計算
                float diff = Mathf.Abs(distance - center);

                // ★安全装置：完全に中央だった場合の「ゼロ除算」を防ぐ（最低でも0.1の差とする）
                diff = Mathf.Max(diff, 0.1f);

                // 逆数（重み）を計算してリストに保存
                float weight = 1f / diff;
                weights.Add(weight);

                // 分母となる「重みの合計」に足していく
                totalWeight += weight;
            }

            // 【ステップB】計算した重み（確率）を使ってルーレットを回す
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < validPatterns.Count; i++)
            {
                currentSum += weights[i];
                if (randomValue <= currentSum)
                {
                    selectedIndex = i;
                    break; // 当たりを引いたらループを抜ける！
                }
            }

            // 選ばれたパターンを実行部隊に渡す！
            DecideEnemyMoves(validPatterns[selectedIndex]);
        }
        else
        {
            UnityEngine.Debug.Log($"【AI】距離 {distance} で使える技がありません！様子見します。");
        }
    }

    private void DecidePlayerAutoAction()
    {
        if (enemyFighter != null && playerFighter != null)
        {
            playerFighter.LookAtTarget(enemyFighter.transform.position.x);
        }
        // ① 現在の距離を取得する（敵の時と同じ計算でOKです）
        // （※変数名はユーザー様の環境に合わせてください）
        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);

        System.Collections.Generic.List<EnemyPatternData> validPatterns = new System.Collections.Generic.List<EnemyPatternData>();

        // ② 候補を絞り込む
        foreach (var pattern in DataManager.Instance.enemyPatterns)
        {
            // 以前直した透明人間（空行）対策！
            if (string.IsNullOrEmpty(pattern.patternName)) continue;

            // ==========================================
            // ★変更点１：敵のIDではなく「プレイヤーのキャラID」で専用パターンを判定する！
            // ==========================================
            if (pattern.usableCharID != 0 && pattern.usableCharID != playerFighter.charaId) continue;

            // 距離条件のチェック
            if (distance >= pattern.minDistance && distance <= pattern.maxDistance)
            {
                validPatterns.Add(pattern);
            }
        }

        // ③ ルーレットを回す（ここは敵の時と100%同じです！）
        if (validPatterns.Count > 0)
        {
            float totalWeight = 0f;
            System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();

            for (int i = 0; i < validPatterns.Count; i++)
            {
                var p = validPatterns[i];
                float center = (p.minDistance + p.maxDistance) / 2f;
                float diff = Mathf.Abs(distance - center);
                diff = Mathf.Max(diff, 0.1f);

                float weight = 1f / diff;
                weights.Add(weight);
                totalWeight += weight;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < validPatterns.Count; i++)
            {
                currentSum += weights[i];
                if (randomValue <= currentSum)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // ==========================================
            // ★変更点２：選ばれたパターンを敵(Enemy)ではなく「プレイヤー(Player)」に渡す！
            // ==========================================
            var selectedPattern = validPatterns[selectedIndex];
            UnityEngine.Debug.Log($"【AI(プレイヤー)】行動パターン『{selectedPattern.patternName}』を決定しました！");

            // ★変更点３：プレイヤーのコマンドリストに、選んだ技のIDをセットする！
            SetPlayerCommandsFromPattern(selectedPattern);
        }
        else
        {
            UnityEngine.Debug.Log($"【AI(プレイヤー)】距離 {distance} で使える技がありません！様子見します。");
        }
    }

    public void StartTurnInputPhase()
    {
        // まず敵の行動を決める（既存の処理）
        DecideNextEnemyAction();
        if (playerFighter.currentHp <= 0 || enemyFighter.currentHp <= 0)
        {
            Debug.Log("試合終了！オートモードを停止します。");
            return;
        }

        if (isPlayerAutoMode)
        {
            // オートモードなら、プレイヤーの行動もAIに決めさせる
            DecidePlayerAutoAction();

            // プレイヤーの入力UIを隠すか、触れなくする
            // playerUI.SetActive(false);

            // 両者の行動が決まったので、そのまま自動でターン実行（バトルアニメーション）へ！
            // 決定ボタンを押した時と同じ関数を直接呼ぶ
            StartCoroutine(ExecuteTurnRoutine());
        }
        else
        {
            // オートモードじゃないなら、通常通りプレイヤーのUI入力を待つ
            //playerUI.SetActive(true);
        }
    }

    // AIが選んだパターンをプレイヤーの入力としてセットする関数
    private void SetPlayerCommandsFromPattern(EnemyPatternData pattern)
    {
        // ① プレイヤーのコマンドリストを一度空にする
        // （※ playerCommands の部分は、ご自身の本当の変数名に直してください！）
        selectedMoves.Clear();

        int totalFrames = 0;

        // ② AIの moveIds リストに入っている技IDを、順番に取り出して追加していく
        foreach (string moveId in pattern.moveIds)
        {
            MoveData move = DataManager.Instance.GetMove(moveId);

            if (move != null)
            {
                // 10フレームを超えないようにする安全装置
                if (totalFrames + move.totalFrames > 10) break;

                // 取得した技データをプレイヤーのリストに追加！
                selectedMoves.Add(move);
                totalFrames += move.totalFrames;
            }
        }

        while (totalFrames < 10)
        {
            MoveData neutral = DataManager.Instance.GetMove("5");
            if (neutral != null)
            {
                selectedMoves.Add(neutral);
                totalFrames += neutral.totalFrames;
            }
            else break; // ニュートラルが見つからなければ無限ループ防止で抜ける
        }
    }

    // ========================================================
    // ▼▼▼ 追加：バトルログ（戦績解析）システム ▼▼▼
    // ========================================================
    public class MoveLogData
    {
        public string moveName;
        public int useCount;     // 発動した回数
        public int hitCount;     // 相手に当たった回数
        public int blockedCount; // ガードされた回数
        public int totalDamage;  // 与えた総ダメージ
    }

    // プレイヤー用と敵用のログを分ける辞書
    private Dictionary<string, MoveLogData> p1Logs = new Dictionary<string, MoveLogData>();
    private Dictionary<string, MoveLogData> p2Logs = new Dictionary<string, MoveLogData>();

    // ① 技が「発動」した時に呼ぶ
    private void RecordMoveUsage(bool isPlayer, MoveData move)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].useCount++;
    }

    // ② 技が「ヒット」してダメージを与えた時に呼ぶ
    public void RecordMoveHit(bool isPlayer, MoveData move, int damage)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].hitCount++;
        logs[move.id].totalDamage += damage;
    }

    // ③ 技が「ガード」された時に呼ぶ
    private void RecordMoveBlock(bool isPlayer, MoveData move)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].blockedCount++;
    }

    // ④ 試合終了時にCSVとして書き出す！
    private void ExportBattleLogCSV()
    {
        // 1. 保存先のフォルダのパス（Assets/BattleLog）を指定
        string folderPath = Application.dataPath + "/BattleLog";

        // 2. もし「BattleLog」フォルダがまだ無ければ、自動で作成する（エラー防止！）
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 3. 実際のファイルのフルパスを設定
        string path = folderPath + "/BattleLog_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

        // 文字化け防止のため UTF8 で書き込む
        using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
        {
            sw.WriteLine("User,MoveID,MoveName,UseCount,HitCount,BlockedCount,WhiffCount,TotalDamage,AvgDamage");

            // P1のログ書き込み
            foreach (var kvp in p1Logs)
            {
                var log = kvp.Value;
                int whiffCount = log.useCount - log.hitCount - log.blockedCount; // 空振り回数 = 発動 - ヒット - ガード
                float avgDmg = log.useCount > 0 ? (float)log.totalDamage / log.useCount : 0;
                sw.WriteLine($"1P(ID:{playerFighter.charaId}),{kvp.Key},{log.moveName},{log.useCount},{log.hitCount},{log.blockedCount},{whiffCount},{log.totalDamage},{avgDmg:F1}");
            }

            // P2のログ書き込み
            foreach (var kvp in p2Logs)
            {
                var log = kvp.Value;
                int whiffCount = log.useCount - log.hitCount - log.blockedCount;
                float avgDmg = log.useCount > 0 ? (float)log.totalDamage / log.useCount : 0;
                sw.WriteLine($"2P(ID:{enemyFighter.charaId}),{kvp.Key},{log.moveName},{log.useCount},{log.hitCount},{log.blockedCount},{whiffCount},{log.totalDamage},{avgDmg:F1}");
            }
        }
        UnityEngine.Debug.Log($"<color=cyan>【解析完了】バトルログをCSVに出力しました！\n場所: {path}</color>");
    }
}