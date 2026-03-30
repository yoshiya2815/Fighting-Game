using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;

/// <summary>
/// バトルのメインループ（ターンの開始・同期実行・終了）を統括するコアマネージャークラス。
/// 1フレーム単位の厳密な同期処理、敵AIの行動決定、勝敗判定、およびバトルログの書き出しに責任を持つ。
/// </summary>
public class CommandInputSystem : MonoBehaviour
{
    public static CommandInputSystem Instance { get; private set; }

    [Header("Turn Management")]
    [SerializeField] private int maxTurnFrames = 10;
    public int currentTurnCount = 0;
    private bool isExecutingTurn = false;
    public bool isGameOver = false;

    [Header("UI References: Timeline & Commands")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform timelineArea;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private float widthPerFrame = 80f;
    [SerializeField] private float blockHeight = 100f;
    [SerializeField] private RectTransform commandButtonArea;
    [SerializeField] private GameObject commandButtonPrefab;
    [SerializeField] private int testCharaID = 2;

    [Header("UI References: Battle Status")]
    public TextMeshProUGUI actionText;
    public TextMeshProUGUI TurnText;
    public ComboUIController comboUI;
    public ComboUIController enemyComboUI;
    public StatusIconController playerStatusIcon;
    public StatusIconController enemyStatusIcon;
    public GameObject battleUIPanel;
    public GameObject koTextObject;
    public TMPro.TextMeshProUGUI debugInfoText;

    [Header("Actors")]
    [SerializeField] public Fighter playerFighter;
    [SerializeField] public Fighter enemyFighter;

    [Header("Action Queues")]
    private List<MoveData> selectedMoves = new List<MoveData>();
    private List<List<GameObject>> generatedBlocksList = new List<List<GameObject>>();
    private int currentCost = 0;
    public List<MoveData> enemySelectedMoves = new List<MoveData>();

    [Header("Battle Generation")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;
    public HPGaugeController playerHpGauge;
    public HPGaugeController enemyHpGauge;

    [Header("AI Settings")]
    public bool useRandomEnemyPattern = true;
    public int fixedPatternId = 1;
    public bool isPlayerAutoMode = false;

    [Header("Debug & Visuals")]
    public bool showHitbox = true;
    public int hitStopTimer = 0;

    [Header("Round & Match Management")]
    public GameObject resultPanel;
    public Text resultWinnerText;
    public TMPro.TextMeshProUGUI resultText;
    public int p1Wins = 0;
    public int p2Wins = 0;
    public int requiredWins = 2;
    public int currentRound = 1;

    private int currentCombo = 0;
    private int enemyCombo = 0;

    /// <summary>
    /// 当たり判定（攻撃・ガード）の矩形座標と寿命を一時管理・可視化するためのデータ構造
    /// </summary>
    private class DebugHitbox
    {
        public float minX, maxX, minY, maxY;
        public float timer;
        public bool isGuard;
        public Fighter owner;
        public MoveData sourceMove;
        public float speedX;
        public float speedY;
        public int facingDir;
        public bool hasHit;
    }

    private List<DebugHitbox> activeHitboxes = new List<DebugHitbox>();

    /// <summary>
    /// アクターから独立して画面内を移動する非同期オブジェクト（飛び道具・罠）の管理データ
    /// </summary>
    public class ProjectileData
    {
        public Fighter owner;
        public MoveData sourceMove;
        public float minX, maxX, minY, maxY;
        public float speedX, speedY;
        public int facingDir;
        public float timer;
        public bool hasHit;
        public GameObject visualBlock;
    }

    [Header("Projectiles")]
    public GameObject projectilePrefab;
    private List<ProjectileData> activeProjectiles = new List<ProjectileData>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupBattle();
        UpdateUI();
        GenerateCommandButtons();
        UpdateTurnDisplay();
        InitializeFighters();
    }

    /// <summary>
    /// マスターデータとシーン間設定に基づき、アクターの初期化と依存関係の注入を行う
    /// </summary>
    private void InitializeFighters()
    {
        int pID = GameSettings.Instance.selectedPlayerID;
        int eID = GameSettings.Instance.selectedEnemyID;

        if (playerFighter != null) playerStatusIcon = playerFighter.GetComponentInChildren<StatusIconController>();
        if (enemyFighter != null) enemyStatusIcon = enemyFighter.GetComponentInChildren<StatusIconController>();

        if (playerFighter != null) playerFighter.Init(DataManager.Instance.GetChara(pID));
        if (enemyFighter != null) enemyFighter.Init(DataManager.Instance.GetChara(eID));

        if (playerHpGauge != null && playerFighter != null) playerHpGauge.InitHP(playerFighter.maxHp);
        if (enemyHpGauge != null && enemyFighter != null) enemyHpGauge.InitHP(enemyFighter.maxHp);

        if (playerFighter != null && enemyFighter != null)
        {
            playerFighter.LookAtTarget(enemyFighter.transform.position.x);
            enemyFighter.LookAtTarget(playerFighter.transform.position.x);
        }
    }

    /// <summary>
    /// Out-Game（選択画面）からの引継ぎ情報を元に、動的にアクターを生成する
    /// </summary>
    private void SetupBattle()
    {
        GameObject p1PrefabToSpawn = playerPrefab;
        GameObject p2PrefabToSpawn = enemyPrefab;

        if (GameSettings.Instance != null)
        {
            GameObject dynamicPlayer = Resources.Load<GameObject>($"Prefabs/Fighter_{GameSettings.Instance.selectedPlayerID}");
            GameObject dynamicEnemy = Resources.Load<GameObject>($"Prefabs/Fighter_{GameSettings.Instance.selectedEnemyID}");

            if (dynamicPlayer != null) p1PrefabToSpawn = dynamicPlayer;
            if (dynamicEnemy != null) p2PrefabToSpawn = dynamicEnemy;
        }

        GameObject p1Object = Instantiate(p1PrefabToSpawn, playerSpawnPoint.position, playerSpawnPoint.rotation);
        GameObject p2Object = Instantiate(p2PrefabToSpawn, enemySpawnPoint.position, enemySpawnPoint.rotation);

        playerFighter = p1Object.GetComponent<Fighter>();
        enemyFighter = p2Object.GetComponent<Fighter>();

        if (playerFighter != null) playerFighter.SetIndicatorColor(Color.cyan);
        if (enemyFighter != null) enemyFighter.SetIndicatorColor(Color.red);

        Debug.Log("[System] アクターの動的生成とバインディングが完了しました。");
    }

    /// <summary>
    /// 選択可能な技ボタンを動的に生成し、共通技と固有技で視覚的なグループ分けを行う
    /// </summary>
    private void GenerateCommandButtons()
    {
        if (commandButtonArea == null || commandButtonPrefab == null) return;

        foreach (Transform child in commandButtonArea) Destroy(child.gameObject);

        List<MoveData> availableMoves = DataManager.Instance.GetMovesForCharacter(playerFighter.charaId);
        List<MoveData> commonMoves = new List<MoveData>();
        List<MoveData> uniqueMoves = new List<MoveData>();

        foreach (MoveData move in availableMoves)
        {
            if (int.TryParse(move.id.Trim(), out int parsedID))
            {
                if (parsedID <= 13) continue; // 移動系コマンドの除外
            }
            else continue;

            if (move.moveName.Contains("派生") || move.moveName.Contains("成功")) continue;

            if (move.usableCharID == 0) commonMoves.Add(move);
            else uniqueMoves.Add(move);
        }

        foreach (MoveData move in commonMoves) CreateSingleButton(move, new Color(0.8f, 0.8f, 0.8f));
        foreach (MoveData move in uniqueMoves) CreateSingleButton(move, new Color(1.0f, 0.7f, 0.4f));
    }

    private void CreateSingleButton(MoveData move, Color btnColor)
    {
        GameObject newBtnObj = Instantiate(commandButtonPrefab, commandButtonArea);

        TMPro.TextMeshProUGUI btnText = newBtnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (btnText != null) btnText.text = $"{move.moveName}\n({move.totalFrames}F)";

        Image btnImage = newBtnObj.GetComponent<Image>();
        if (btnImage != null) btnImage.color = btnColor;

        Button btn = newBtnObj.GetComponent<Button>();
        string moveIDForButton = move.id; // クロージャによる変数キャプチャの保護
        btn.onClick.AddListener(() => TryAddCommand(moveIDForButton));
    }

    public void TryAddCommand(string moveID)
    {
        MoveData move = DataManager.Instance.GetMove(moveID);
        if (move == null) return;

        if (isExecutingTurn || currentCost >= 10) return;

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
                nameText.text = (i == 0) ? move.moveName : "-";
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

    private void Update()
    {
        if (Time.timeScale == 0f) return; // ヒットストップ等による物理演算の完全バイパス

        if (isExecutingTurn)
        {
            ProcessProjectilesLifecycle();
            ProcessHitboxesLifecycle();
        }
        UpdateDebugInfo();
    }

    /// <summary>
    /// 非同期オブジェクト（飛び道具等）の移動、衝突判定、および寿命管理を行う
    /// </summary>
    private void ProcessProjectilesLifecycle()
    {
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = activeProjectiles[i];

            if (proj.speedX != 0f)
            {
                float moveX = proj.speedX * proj.facingDir * Time.deltaTime;
                proj.minX += moveX;
                proj.maxX += moveX;

                if (proj.visualBlock != null) proj.visualBlock.transform.position += new Vector3(moveX, 0, 0);
            }

            if (!proj.hasHit)
            {
                Fighter opponent = (proj.owner == playerFighter) ? enemyFighter : playerFighter;
                if (opponent != null)
                {
                    float[] eBox = opponent.GetHurtbox();
                    bool isHitX = (proj.maxX >= eBox[0]) && (proj.minX <= eBox[1]);
                    bool isHitY = (proj.maxY >= eBox[2]) && (proj.minY <= eBox[3]);

                    if (isHitX && isHitY)
                    {
                        proj.hasHit = true;
                        proj.timer = 0f;
                        ApplyProjectileHit(proj.owner, opponent, proj.sourceMove);
                    }
                }
            }

            proj.timer -= Time.deltaTime;
            if (proj.timer <= 0)
            {
                if (proj.visualBlock != null) Destroy(proj.visualBlock);
                activeProjectiles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// ギズモ描画用の判定枠の寿命管理を行う
    /// </summary>
    private void ProcessHitboxesLifecycle()
    {
        for (int i = activeHitboxes.Count - 1; i >= 0; i--)
        {
            var box = activeHitboxes[i];
            box.timer -= Time.deltaTime;
            if (box.timer <= 0) activeHitboxes.RemoveAt(i);
            else activeHitboxes[i] = box;
        }
    }

    private void UpdateDebugInfo()
    {
        if (debugInfoText != null && playerFighter != null && enemyFighter != null)
        {
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
        UnityEngine.Debug.Log($"[System] オートモード: {(isOn ? "ON" : "OFF")}");

        if (isOn && !isExecutingTurn && !isGameOver)
        {
            StartCoroutine(ExecuteTurnRoutine());
        }
    }

    public void ShowResultUI(string message = "")
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            if (resultWinnerText != null && message != "") resultWinnerText.text = message;
        }
    }

    public void OnClickRetry() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    public void OnClickToSelect() => SceneManager.LoadScene("CharSelect");
    public void OnClickQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ExecuteCommands()
    {
        if (isGameOver || selectedMoves.Count == 0 || isExecutingTurn) return;

        if (currentCost < 10)
        {
            if (actionText != null) actionText.text = "コマンドを10個選んでください！";
            return;
        }

        StartCoroutine(ExecuteTurnRoutine());
    }

    /// <summary>
    /// ターンの実行シーケンス。非同期バグを防ぐため、1フレームごとにPlayerとEnemyの行動を厳格に同期処理する。
    /// </summary>
    private IEnumerator ExecuteTurnRoutine()
    {
        isExecutingTurn = true;
        currentTurnCount++;
        UpdateTurnDisplay();

        if (currentTurnCount > 40)
        {
            isGameOver = true;
            Debug.Log("[System] TIME UP. 判定へ移行します。");
            StartCoroutine(ProcessRoundEnd(true));
            isExecutingTurn = false;
            playerFighter.ResetStateToNeutral();
            enemyFighter.ResetStateToNeutral();
            yield break;
        }

        ApplySuddenDeathModifiers();
        UpdateFighterExtraSA();

        DecideNextEnemyAction();
        if (isPlayerAutoMode) DecidePlayerAutoAction();

        float frameDuration = 0.5f;
        int pMoveIndex = 0; int pLocalFrame = 1;
        int eMoveIndex = 0; int eLocalFrame = 1;

        bool[] pHasHit = new bool[10];
        bool[] eHasHit = new bool[10];

        // 1Fごとのゲームループ：双方のステート更新と当たり判定を同調させる
        for (int globalFrame = 1; globalFrame <= maxTurnFrames; globalFrame++)
        {
            if (pMoveIndex < selectedMoves.Count)
            {
                MoveData pMove = selectedMoves[pMoveIndex];
                if (pLocalFrame == 1) pHasHit = new bool[Mathf.Max(1, pMove.hitFrames.Count)];
                ProcessActionFrame(playerFighter, enemyFighter, pMove, pLocalFrame, pHasHit, ref currentCombo, frameDuration, true);
                if (++pLocalFrame > pMove.totalFrames) { pMoveIndex++; pLocalFrame = 1; }
            }

            if (eMoveIndex < enemySelectedMoves.Count)
            {
                MoveData eMove = enemySelectedMoves[eMoveIndex];
                if (eLocalFrame == 1) eHasHit = new bool[Mathf.Max(1, eMove.hitFrames.Count)];
                ProcessActionFrame(enemyFighter, playerFighter, eMove, eLocalFrame, eHasHit, ref enemyCombo, frameDuration, false);
                if (++eLocalFrame > eMove.totalFrames) { eMoveIndex++; eLocalFrame = 1; }
            }

            RestoreGuardEndurance();

            if (playerFighter.triggerCounter) SwapToCounterMove(playerFighter, selectedMoves, pMoveIndex, ref pLocalFrame, ref pHasHit);
            if (enemyFighter.triggerCounter) SwapToCounterMove(enemyFighter, enemySelectedMoves, eMoveIndex, ref eLocalFrame, ref eHasHit);

            if (playerFighter.currentHp <= 0 || enemyFighter.currentHp <= 0)
            {
                isGameOver = true;
                if (!isGameOver) StartCoroutine(ProcessRoundEnd(false));
                break;
            }

            yield return new WaitForSeconds(frameDuration);
        }

        if (!isGameOver)
        {
            playerFighter.ResetStateToNeutral();
            enemyFighter.ResetStateToNeutral();
        }

        isExecutingTurn = false;
        ClearCommands();

        if (isPlayerAutoMode && !isGameOver)
        {
            yield return new WaitForSeconds(1.0f);
            StartCoroutine(ExecuteTurnRoutine());
        }
    }

    private void ApplySuddenDeathModifiers()
    {
        if (currentTurnCount == 20)
        {
            if (actionText != null) actionText.text = "【警戒】ダメージ補正 1.3倍に上昇！";
        }
        else if (currentTurnCount == 25)
        {
            if (actionText != null) actionText.text = "【危険】ダメージ補正 1.5倍に上昇！！";
        }
        else if (currentTurnCount == 30)
        {
            if (actionText != null) actionText.text = "【致死領域】ダメージ補正 2.0倍！！！";
        }
        if (actionText != null && currentTurnCount < 20) actionText.text = "バトル開始！";
    }

    private void UpdateFighterExtraSA()
    {
        int CalculateExtraSA(int turnCount)
        {
            if (turnCount >= 30) return 3;
            if (turnCount >= 20) return 2;
            if (turnCount >= 10) return 1;
            return 0;
        }

        if (playerFighter != null && playerFighter.charaId == 2)
        {
            playerFighter.extraSA = CalculateExtraSA(currentTurnCount);
            if (playerStatusIcon != null) playerStatusIcon.UpdateAuraByLevel(playerFighter.extraSA);
        }

        if (enemyFighter != null && enemyFighter.charaId == 2)
        {
            enemyFighter.extraSA = CalculateExtraSA(currentTurnCount);
            if (enemyStatusIcon != null) enemyStatusIcon.UpdateAuraByLevel(enemyFighter.extraSA);
        }
    }

    private void RestoreGuardEndurance()
    {
        if (playerFighter.charaData != null && !playerFighter.isGuarding)
            playerFighter.currentGuard = Mathf.Min(playerFighter.currentGuard + playerFighter.charaData.guardRecovery, playerFighter.charaData.guardEndurance);

        if (enemyFighter.charaData != null && !enemyFighter.isGuarding)
            enemyFighter.currentGuard = Mathf.Min(enemyFighter.currentGuard + enemyFighter.charaData.guardRecovery, enemyFighter.charaData.guardEndurance);
    }

    /// <summary>
    /// カウンター成立時に、実行中の行動キューを派生技に動的置換する
    /// </summary>
    private void SwapToCounterMove(Fighter fighter, List<MoveData> moveList, int moveIndex, ref int localFrame, ref bool[] hasHit)
    {
        fighter.triggerCounter = false;
        fighter.isCounterStance = false;

        MoveData derivedMove = DataManager.Instance.GetMove(fighter.derivedMoveID);
        if (derivedMove != null)
        {
            moveList[moveIndex] = derivedMove;
            localFrame = 1;
            hasHit = new bool[Mathf.Max(1, derivedMove.hitFrames.Count)];

            if (actionText != null && fighter == playerFighter) actionText.text = $"【反撃】{derivedMove.moveName}！";
            Debug.Log($"[System] カウンター成立: {fighter.fighterName} の行動が {derivedMove.moveName} に派生。");
        }
        else
        {
            Debug.LogError($"[System Error] 派生技データ未登録: ID {fighter.derivedMoveID}");
        }
    }

    /// <summary>
    /// 被弾時のコンボ数リセット処理と、特定のキャラ特性（スピードキャラの半減継続等）を適用する
    /// </summary>
    private void ApplyComboReset(Fighter defender, bool isAttackerPlayer)
    {
        if (defender.charaId == 3) // スピードキャラ固有のコンボ半減仕様
        {
            if (isAttackerPlayer)
            {
                enemyCombo /= 2;
                if (enemyComboUI != null)
                {
                    if (enemyCombo > 0)
                    {
                        float mult = enemyFighter.charaData != null ? enemyFighter.charaData.comboMultiplier : 1.0f;
                        enemyComboUI.UpdateCombo(enemyCombo, enemyCombo * mult, true);
                    }
                    else enemyComboUI.ResetCombo();
                }
            }
            else
            {
                currentCombo /= 2;
                if (comboUI != null)
                {
                    if (currentCombo > 0)
                    {
                        float mult = playerFighter.charaData != null ? playerFighter.charaData.comboMultiplier : 1.0f;
                        comboUI.UpdateCombo(currentCombo, currentCombo * mult, true);
                    }
                    else comboUI.ResetCombo();
                }
            }
        }
        else // 通常のリセット
        {
            if (isAttackerPlayer)
            {
                enemyCombo = 0;
                if (enemyComboUI != null) enemyComboUI.ResetCombo();
            }
            else
            {
                currentCombo = 0;
                if (comboUI != null) comboUI.ResetCombo();
            }
        }
    }

    /// <summary>
    /// アクターの1フレームごとの状態遷移、物理移動、および攻撃判定の解決を行う
    /// </summary>
    private void ProcessActionFrame(Fighter attacker, Fighter defender, MoveData move, int currentFrame, bool[] hasHitTarget, ref int comboCount, float frameDuration, bool isPlayer)
    {
        if (CheckStun(attacker, isPlayer)) return;

        bool isCounterFrame = UpdateStancesAndGetCounter(attacker, move, currentFrame);

        if (currentFrame == 1 && !ProcessFirstFrameSetup(attacker, move, isPlayer)) return;

        ProcessMovement(attacker, move, currentFrame, frameDuration);

        if (attacker.isGuarding)
        {
            ProcessGuardBox(attacker, move, isPlayer, frameDuration, isCounterFrame);
            return;
        }

        ProcessAttacks(attacker, defender, move, currentFrame, hasHitTarget, ref comboCount, frameDuration, isPlayer);
    }

    private bool CheckStun(Fighter attacker, bool isPlayer)
    {
        if (attacker.stunTimer > 0)
        {
            if (isPlayer && actionText != null) actionText.text = "【硬直中】うおぉっ！";
            attacker.stunTimer--;
            return true;
        }
        return false;
    }

    private bool UpdateStancesAndGetCounter(Fighter attacker, MoveData move, int currentFrame)
    {
        attacker.isCrouching = move.moveName.Contains("しゃがみ") || move.id == "20" || move.id == "21" || move.id == "303";
        attacker.isShieldBashing = (move.id == "702");

        bool isCounterFrame = move.moveName.Contains("カウンター") && !move.moveName.Contains("派生") && currentFrame == 1;
        attacker.isGuarding = move.moveName.Contains("ガード") || move.id == "701" || isCounterFrame;

        if (isCounterFrame)
        {
            attacker.isCounterStance = true;
            attacker.derivedMoveID = move.id.ToString().Trim() + "0";
        }
        else if (currentFrame == 1)
        {
            attacker.isCounterStance = false;
        }

        attacker.SetVisualCrouch(attacker.isCrouching && attacker.isGrounded);
        return isCounterFrame;
    }

    private bool ProcessFirstFrameSetup(Fighter attacker, MoveData move, bool isPlayer)
    {
        RecordMoveUsage(isPlayer, move);

        // 使用条件のガード節
        if (move.usableLocation == 1 && !attacker.isGrounded) return false;
        if (move.usableLocation == 2 && attacker.isGrounded) return false;

        attacker.currentSA = move.saValue + attacker.extraSA;
        if (isPlayer && actionText != null) actionText.text = $"【発動】{move.moveName}";

        if (move.moveName.Contains("右向き") || move.moveName.Contains("右移動") || move.moveName.Contains("右上"))
            attacker.SetFacingDirection(1);
        else if (move.moveName.Contains("左向き") || move.moveName.Contains("左移動") || move.moveName.Contains("左上"))
            attacker.SetFacingDirection(-1);
        else if (move.moveName.Contains("振り向き"))
            attacker.SetFacingDirection(attacker.facingDir * -1);

        return true;
    }

    private void ProcessMovement(Fighter attacker, MoveData move, int currentFrame, float frameDuration)
    {
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

        for (int i = 0; i < move.moveY.Count; i++)
        {
            if (move.moveStartY[i] == currentFrame) attacker.Jump(move.moveY[i]);
        }
    }

    private void ProcessGuardBox(Fighter attacker, MoveData move, bool isPlayer, float frameDuration, bool isCounterFrame)
    {
        Vector3 pPos = attacker.transform.position;
        float mult = attacker.distanceMultiplier;
        float pFeetY = pPos.y - ((attacker.bodyHeight * mult) / 2f);
        int dir = attacker.facingDir;

        MoveData boxData = isCounterFrame ? (DataManager.Instance.GetMove("8") ?? move) : move;

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

            float w = Mathf.Abs(maxX - minX);
            float h = Mathf.Abs(maxY - minY);
            HitboxVisualizer.Instance.ShowHitbox(new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f), new Vector2(w, h), Color.blue, frameDuration);
        }
    }

    /// <summary>
    /// 当たり判定の生成と衝突判定の解決を行う（カプセル化されたDamageInfoを使用）
    /// </summary>
    private void ProcessAttacks(Fighter attacker, Fighter defender, MoveData move, int currentFrame, bool[] hasHitTarget, ref int comboCount, float frameDuration, bool isPlayer)
    {
        for (int hitIndex = 0; hitIndex < move.hitFrames.Count; hitIndex++)
        {
            int startFrame = move.hitFrames[hitIndex];
            int duration = (hitIndex < move.activeFrames.Count && move.activeFrames[hitIndex] > 0) ? move.activeFrames[hitIndex] : 1;
            int endFrame = startFrame + duration - 1;

            if (currentFrame < startFrame || currentFrame > endFrame) continue;
            if (attacker == null || defender == null || hitIndex >= move.damages.Count) continue;

            // 1. 判定枠の算出
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
                return;
            }

            if (move.isProjectile)
            {
                float w = Mathf.Abs(maxX - minX);
                float h = Mathf.Abs(maxY - minY);
                float cX = (minX + maxX) / 2f;
                float cY = (minY + maxY) / 2f;

                GameObject blockObj = null;
                if (projectilePrefab != null)
                {
                    blockObj = Instantiate(projectilePrefab);
                    blockObj.transform.position = new Vector3(cX, cY, 0);
                    blockObj.transform.localScale = new Vector3(w, h, 1);
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

                float w = Mathf.Abs(maxX - minX);
                float h = Mathf.Abs(maxY - minY);
                HitboxVisualizer.Instance.ShowHitbox(new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f), new Vector2(w, h), Color.red, frameDuration);
            }

            // 2. 衝突判定の解決
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
                    if ((maxX >= gBox[0]) && (minX <= gBox[1]) && (maxY >= gBox[2]) && (minY <= gBox[3])) isGuarded = true;
                }

                if (isGuarded) ProcessGuardedHit(attacker, defender, move, hitIndex, isPlayer);
                else ProcessDirectHit(attacker, defender, move, hitIndex, ref comboCount, isPlayer);
            }
        }
    }

    private void ProcessGuardedHit(Fighter attacker, Fighter defender, MoveData move, int hitIndex, bool isPlayer)
    {
        if (defender.isCounterStance)
        {
            defender.triggerCounter = true;
            Debug.Log($"[System] カウンター成立: {defender.fighterName} が攻撃を受け止めた。");
            return;
        }

        float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
        int chipDamage = Mathf.Max(1, Mathf.RoundToInt((move.damages[hitIndex] * charaDmgMult) * 0.1f));

        DamageInfo chipInfo = new DamageInfo { damage = chipDamage, moveId = move.id, attacker = attacker, isReflect = false, isSAActive = true };
        defender.TakeDamage(chipInfo);
        UpdateHpGaugeUI(defender);

        defender.currentGuard -= chipDamage;

        if (defender.currentHp <= 0 && !isGameOver)
        {
            StartCoroutine(ProcessRoundEnd(false));
            return;
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
            defender.MoveX((defender.charaId == 7) ? 0f : -0.5f * attacker.facingDir, 0.2f);

            // 防御特化キャラ(ID:7)の反射処理
            if (defender.charaId == 7 && defender.isGuarding)
            {
                int reflectDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.03f));
                DamageInfo reflectInfo = new DamageInfo { damage = reflectDamage, moveId = "701", attacker = defender, isReflect = true, isSAActive = false };
                attacker.TakeDamage(reflectInfo);

                if (attacker.currentHp <= 0 && !isGameOver) StartCoroutine(ProcessRoundEnd(false));

                MoveData guardMove = DataManager.Instance.GetMove("701");
                if (guardMove != null) RecordMoveHit(!isPlayer, guardMove, reflectDamage);
            }
        }
    }

    private void ProcessDirectHit(Fighter attacker, Fighter defender, MoveData move, int hitIndex, ref int comboCount, bool isPlayer)
    {
        int addCombo = (move.comboCounts != null && move.comboCounts.Count > hitIndex) ? move.comboCounts[hitIndex] : 1;
        if (attacker.charaId == 3) addCombo *= 2; // スピードキャラ補正

        if (isPlayer) currentCombo += addCombo; else enemyCombo += addCombo;

        float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
        float charaComboMult = attacker.charaData != null ? attacker.charaData.comboMultiplier : 1.0f;
        float saDamageBonus = attacker.charaData != null ? (attacker.currentSA * attacker.charaData.saBonus) : 0f;

        int rawDamage = Mathf.RoundToInt((move.damages[hitIndex] * charaDmgMult) + (comboCount * charaComboMult) + saDamageBonus);
        bool isSAActive = !move.isThrow && (move.saBreak < defender.currentSA);

        RecordMoveHit(isPlayer, move, rawDamage);
        StartCoroutine(HitStopRoutine(Mathf.Clamp(rawDamage / 200f, 0.05f, 0.5f)));

        DamageInfo info = new DamageInfo { damage = rawDamage, moveId = move.id, attacker = attacker, isReflect = false, isSAActive = isSAActive };
        defender.TakeDamage(info);
        UpdateHpGaugeUI(defender);

        if (isPlayer && comboUI != null) comboUI.UpdateCombo(currentCombo, comboCount * charaComboMult);
        if (!isPlayer && enemyComboUI != null) enemyComboUI.UpdateCombo(enemyCombo, enemyCombo * (enemyFighter.charaData?.comboMultiplier ?? 1.0f));

        if (defender.currentHp <= 0 && !isGameOver)
        {
            StartCoroutine(ProcessRoundEnd(false));
            return;
        }

        if (!isSAActive) // アーマーブレイク
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
            float weightMult = 1.0f / ((defender.charaData != null && defender.charaData.weight > 0f) ? defender.charaData.weight : 1.0f);

            float finalDir = (move.id == "803") ? ((defender.transform.position.x - attacker.transform.position.x >= 0) ? 1.0f : -1.0f) : attacker.facingDir;

            if (kbX != 0) defender.MoveX(kbX * finalDir * weightMult, 0.2f);
            if (kbY != 0) defender.Knockup(kbY * 80f * weightMult);
        }
    }

    private void ApplyProjectileHit(Fighter attacker, Fighter defender, MoveData move)
    {
        bool isPlayerAttacking = (attacker == playerFighter);

        if (defender.isGuarding && !move.isThrow)
        {
            if (defender.isCounterStance)
            {
                defender.triggerCounter = true;
                return;
            }

            float charaDmgMult = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
            int chipDamage = Mathf.Max(1, Mathf.RoundToInt(((move.damages.Count > 0 ? move.damages[0] : 10) * charaDmgMult) * 0.1f));

            DamageInfo chipInfo = new DamageInfo { damage = chipDamage, moveId = move.id, attacker = attacker, isReflect = false, isSAActive = true };
            defender.TakeDamage(chipInfo);
            UpdateHpGaugeUI(defender);
            defender.currentGuard -= chipDamage;

            if (defender.currentHp <= 0 && !isGameOver) { StartCoroutine(ProcessRoundEnd(false)); return; }

            if (defender.currentGuard <= 0)
            {
                defender.currentGuard = 0;
                defender.isGuarding = false;
                defender.AddStun(5);
                ApplyComboReset(defender, isPlayerAttacking);
                defender.MoveX(1.0f * attacker.facingDir, 0.2f);
            }
            else
            {
                if (defender.charaId == 7)
                {
                    defender.MoveX(0f, 0f);
                    int reflectDamage = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.03f));
                    DamageInfo reflectInfo = new DamageInfo { damage = reflectDamage, moveId = "701", attacker = defender, isReflect = true, isSAActive = false };
                    attacker.TakeDamage(reflectInfo);

                    if (attacker.currentHp <= 0 && !isGameOver) StartCoroutine(ProcessRoundEnd(false));

                    MoveData guardMove = DataManager.Instance.GetMove("701");
                    if (guardMove != null) RecordMoveHit(!isPlayerAttacking, guardMove, reflectDamage);
                }
                else defender.MoveX(-0.5f * attacker.facingDir, 0.2f);
            }
            RecordMoveBlock(isPlayerAttacking, move);
            return;
        }

        int addCombo = (move.comboCounts != null && move.comboCounts.Count > 0) ? move.comboCounts[0] : 1;
        int currentComboCount = isPlayerAttacking ? (currentCombo += addCombo) : (enemyCombo += addCombo);

        float charaDmgMult2 = attacker.charaData != null ? attacker.charaData.damageMultiplier : 1.0f;
        float charaComboMult = attacker.charaData != null ? attacker.charaData.comboMultiplier : 1.0f;
        float saDamageBonus = attacker.charaData != null ? (attacker.currentSA * attacker.charaData.saBonus) : 0f;
        float baseDamage = move.damages.Count > 0 ? move.damages[0] : 10;

        float suddenDeathMultiplier = (currentTurnCount >= 30) ? 2.5f : (currentTurnCount >= 25) ? 2.0f : (currentTurnCount >= 20) ? 1.5f : 1.0f;
        int rawDamage = Mathf.RoundToInt(((baseDamage * charaDmgMult2) + (currentComboCount * charaComboMult) + saDamageBonus) * suddenDeathMultiplier);
        bool isSAActive = !move.isThrow && (move.saBreak < defender.currentSA);

        RecordMoveHit(isPlayerAttacking, move, rawDamage);

        DamageInfo info = new DamageInfo { damage = rawDamage, moveId = move.id, attacker = attacker, isReflect = false, isSAActive = isSAActive };
        defender.TakeDamage(info);
        UpdateHpGaugeUI(defender);

        if (isPlayerAttacking && comboUI != null) comboUI.UpdateCombo(currentComboCount, currentComboCount * charaComboMult);
        if (!isPlayerAttacking && enemyComboUI != null) enemyComboUI.UpdateCombo(enemyCombo, enemyCombo * (enemyFighter.charaData?.comboMultiplier ?? 1.0f));

        if (defender.currentHp <= 0 && !isGameOver) { StartCoroutine(ProcessRoundEnd(false)); return; }
        else if (!isGameOver) StartCoroutine(HitStopRoutine(Mathf.Clamp(rawDamage / 200f, 0.05f, 0.5f)));

        if (!isSAActive)
        {
            defender.currentSA = 0;
            ApplyComboReset(defender, isPlayerAttacking);

            int stunFrames = move.hitStunFrames.Count > 0 ? move.hitStunFrames[0] : 0;
            if (stunFrames > 0) defender.AddStun(stunFrames);

            float kbX = move.knockbackX.Count > 0 ? move.knockbackX[0] : 0f;
            float kbY = move.knockbackY.Count > 0 ? move.knockbackY[0] : 0f;
            float weightMult = 1.0f / ((defender.charaData != null && defender.charaData.weight > 0f) ? defender.charaData.weight : 1.0f);

            if (kbX != 0) defender.MoveX(kbX * attacker.facingDir * weightMult, 0.2f);
            if (kbY != 0) defender.Knockup(kbY * 80f * weightMult);
        }
    }

    private void UpdateHpGaugeUI(Fighter defender)
    {
        if (defender == playerFighter && playerHpGauge != null) playerHpGauge.UpdateHP(playerFighter.currentHp);
        else if (defender == enemyFighter && enemyHpGauge != null) enemyHpGauge.UpdateHP(enemyFighter.currentHp);
    }

    private void OnDrawGizmos()
    {
        if (!showHitbox) return;

        foreach (var box in activeHitboxes)
        {
            Vector3 center = new Vector3((box.minX + box.maxX) / 2f, (box.minY + box.maxY) / 2f, 0);
            Vector3 size = new Vector3(box.maxX - box.minX, box.maxY - box.minY, 0.1f);

            if (box.isGuard)
            {
                Gizmos.color = new Color(0f, 0f, 1f, 0.4f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(center, size);
            }
        }
    }

    /// <summary>
    /// ラウンド決着時の遷移処理と勝敗判定
    /// </summary>
    private IEnumerator ProcessRoundEnd(bool isTimeUp)
    {
        isGameOver = true;
        Time.timeScale = 1.0f; // フリーズ対策

        if (resultText != null)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = isTimeUp ? "<color=yellow>TIME UP</color>" : "<color=red>K.O.</color>";
        }

        yield return new WaitForSeconds(2.0f);

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
        else resultText.text = "<color=yellow>DRAW</color>";

        yield return new WaitForSeconds(3.0f);

        if (p1Wins >= requiredWins || p2Wins >= requiredWins)
        {
            if (p1Wins > p2Wins) resultText.text = "<color=red>YOU WIN!!</color>";
            else if (p2Wins > p1Wins) resultText.text = "<color=blue>YOU LOSE...</color>";
            else resultText.text = "DRAW MATCH";
            StartCoroutine(DelayedShowResult(3.0f));
        }
        else StartCoroutine(NextRoundRoutine(0f));
    }

    private IEnumerator NextRoundRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentRound++;
        if (resultText != null) resultText.gameObject.SetActive(false);

        if (activeProjectiles != null)
        {
            foreach (var proj in activeProjectiles) if (proj.visualBlock != null) Destroy(proj.visualBlock);
            activeProjectiles.Clear();
        }

        currentTurnCount = 0;
        UpdateTurnDisplay();

        playerFighter.currentHp = playerFighter.maxHp;
        enemyFighter.currentHp = enemyFighter.maxHp;
        playerFighter.currentSA = enemyFighter.currentSA = 0;
        currentCombo = enemyCombo = 0;
        comboUI.ResetCombo();
        enemyComboUI.ResetCombo();

        if (playerHpGauge != null) playerHpGauge.InitHP(playerFighter.currentHp);
        if (enemyHpGauge != null) enemyHpGauge.InitHP(enemyFighter.currentHp);

        playerFighter.transform.position = new Vector3(75.0f, playerFighter.transform.position.y, 0);
        enemyFighter.transform.position = new Vector3(325.0f, enemyFighter.transform.position.y, 0);

        playerFighter.ResetStateToNeutral();
        enemyFighter.ResetStateToNeutral();
        playerFighter.facingDir = 1;
        enemyFighter.facingDir = -1;

        yield return new WaitForSeconds(0.5f);
        isGameOver = false;
    }

    private IEnumerator DelayedShowResult(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowResultUI();
    }

    public void ResetGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    private System.Collections.IEnumerator HitStopRoutine(float stopTime)
    {
        if (isPlayerAutoMode) yield break;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(stopTime);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// AIの行動決定ロジック。マスターデータ側の設定ミスをガード節で遮断し、堅牢性を担保する。
    /// </summary>
    private void DecideEnemyMoves(EnemyPatternData pattern)
    {
        if (pattern.usableCharID != 0 && pattern.usableCharID != enemyFighter.charaId) return;

        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);
        if (distance < pattern.minDistance || distance > pattern.maxDistance) return;

        enemySelectedMoves.Clear();
        int totalFrames = 0;

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
    }

    private void DecideNextEnemyAction()
    {
        if (enemyFighter != null && playerFighter != null) enemyFighter.LookAtTarget(playerFighter.transform.position.x);

        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);
        List<EnemyPatternData> validPatterns = new List<EnemyPatternData>();

        foreach (var pattern in DataManager.Instance.enemyPatterns)
        {
            if (string.IsNullOrEmpty(pattern.patternName)) continue;
            if (pattern.usableCharID != 0 && pattern.usableCharID != enemyFighter.charaId) continue;
            if (distance >= pattern.minDistance && distance <= pattern.maxDistance) validPatterns.Add(pattern);
        }

        if (validPatterns.Count > 0)
        {
            float totalWeight = 0f;
            List<float> weights = new List<float>();

            for (int i = 0; i < validPatterns.Count; i++)
            {
                var p = validPatterns[i];
                float center = (p.minDistance + p.maxDistance) / 2f;
                float diff = Mathf.Max(Mathf.Abs(distance - center), 0.1f);
                float weight = 1f / diff;
                weights.Add(weight);
                totalWeight += weight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentSum = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < validPatterns.Count; i++)
            {
                currentSum += weights[i];
                if (randomValue <= currentSum) { selectedIndex = i; break; }
            }
            DecideEnemyMoves(validPatterns[selectedIndex]);
        }
    }

    private void DecidePlayerAutoAction()
    {
        if (enemyFighter != null && playerFighter != null) playerFighter.LookAtTarget(enemyFighter.transform.position.x);

        float distance = Mathf.Abs(playerFighter.transform.position.x - enemyFighter.transform.position.x);
        List<EnemyPatternData> validPatterns = new List<EnemyPatternData>();

        foreach (var pattern in DataManager.Instance.enemyPatterns)
        {
            if (string.IsNullOrEmpty(pattern.patternName)) continue;
            if (pattern.usableCharID != 0 && pattern.usableCharID != playerFighter.charaId) continue;
            if (distance >= pattern.minDistance && distance <= pattern.maxDistance) validPatterns.Add(pattern);
        }

        if (validPatterns.Count > 0)
        {
            float totalWeight = 0f;
            List<float> weights = new List<float>();

            for (int i = 0; i < validPatterns.Count; i++)
            {
                var p = validPatterns[i];
                float center = (p.minDistance + p.maxDistance) / 2f;
                float diff = Mathf.Max(Mathf.Abs(distance - center), 0.1f);
                float weight = 1f / diff;
                weights.Add(weight);
                totalWeight += weight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentSum = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < validPatterns.Count; i++)
            {
                currentSum += weights[i];
                if (randomValue <= currentSum) { selectedIndex = i; break; }
            }
            SetPlayerCommandsFromPattern(validPatterns[selectedIndex]);
        }
    }

    public void StartTurnInputPhase()
    {
        DecideNextEnemyAction();
        if (playerFighter.currentHp <= 0 || enemyFighter.currentHp <= 0) return;

        if (isPlayerAutoMode)
        {
            DecidePlayerAutoAction();
            StartCoroutine(ExecuteTurnRoutine());
        }
    }

    private void SetPlayerCommandsFromPattern(EnemyPatternData pattern)
    {
        selectedMoves.Clear();
        int totalFrames = 0;

        foreach (string moveId in pattern.moveIds)
        {
            MoveData move = DataManager.Instance.GetMove(moveId);
            if (move != null)
            {
                if (totalFrames + move.totalFrames > 10) break;
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
            else break;
        }
    }

    #region Battle Log System
    public class MoveLogData
    {
        public string moveName;
        public int useCount;
        public int hitCount;
        public int blockedCount;
        public int totalDamage;
    }

    private Dictionary<string, MoveLogData> p1Logs = new Dictionary<string, MoveLogData>();
    private Dictionary<string, MoveLogData> p2Logs = new Dictionary<string, MoveLogData>();

    private void RecordMoveUsage(bool isPlayer, MoveData move)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].useCount++;
    }

    public void RecordMoveHit(bool isPlayer, MoveData move, int damage)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].hitCount++;
        logs[move.id].totalDamage += damage;
    }

    private void RecordMoveBlock(bool isPlayer, MoveData move)
    {
        var logs = isPlayer ? p1Logs : p2Logs;
        if (!logs.ContainsKey(move.id)) logs[move.id] = new MoveLogData { moveName = move.moveName };
        logs[move.id].blockedCount++;
    }

    /// <summary>
    /// データ駆動でのバランス調整を行うための解析モジュール。
    /// 各技の命中率や平均ダメージを自動集計し、CSVとして出力する。
    /// </summary>
    private void ExportBattleLogCSV()
    {
        string folderPath = Application.dataPath + "/BattleLog";
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string path = folderPath + "/BattleLog_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";

        using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
        {
            sw.WriteLine("User,MoveID,MoveName,UseCount,HitCount,BlockedCount,WhiffCount,TotalDamage,AvgDamage");

            foreach (var kvp in p1Logs)
            {
                var log = kvp.Value;
                int whiffCount = log.useCount - log.hitCount - log.blockedCount;
                float avgDmg = log.useCount > 0 ? (float)log.totalDamage / log.useCount : 0;
                sw.WriteLine($"1P(ID:{playerFighter.charaId}),{kvp.Key},{log.moveName},{log.useCount},{log.hitCount},{log.blockedCount},{whiffCount},{log.totalDamage},{avgDmg:F1}");
            }

            foreach (var kvp in p2Logs)
            {
                var log = kvp.Value;
                int whiffCount = log.useCount - log.hitCount - log.blockedCount;
                float avgDmg = log.useCount > 0 ? (float)log.totalDamage / log.useCount : 0;
                sw.WriteLine($"2P(ID:{enemyFighter.charaId}),{kvp.Key},{log.moveName},{log.useCount},{log.hitCount},{log.blockedCount},{whiffCount},{log.totalDamage},{avgDmg:F1}");
            }
        }
        Debug.Log($"[System] バトルログを出力しました: {path}");
    }
    #endregion

    private void UpdateTurnDisplay()
    {
        if (TurnText != null) TurnText.text = $"TURN {currentTurnCount}";
    }
}