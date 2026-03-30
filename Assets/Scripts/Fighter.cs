using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// キャラクターの内部状態、物理演算、および動的な当たり判定(Hurtbox)を管理するアクタークラス。
/// 進行管理システム(Manager)からの指示を受け取り、自身の状態更新とUIへのイベント通知のみを行う（疎結合設計）。
/// </summary>
public class Fighter : MonoBehaviour
{
    [Header("Data Profile")]
    public CharaData charaData;
    public int charaId = 2;
    public string fighterName = "Player";

    [Header("Status")]
    public int maxHp = 100;
    public int currentHp;
    public float moveSpeed;

    [Header("Hurtbox Constraints")]
    public float bodyWidth = 1.0f;
    public float bodyHeight = 2.0f;
    public float distanceMultiplier = 100f;

    [Header("Physics Parameters")]
    public float gravity = 300f;
    public float jumpForceMultiplier = 200f;
    public int maxJumps = 2;
    public float stageLeftWall = 10f;
    public float stageRightWall = 390f;

    [Header("State Flags")]
    public int facingDir = 1;
    public bool isGrounded = true;
    public int stunTimer = 0;
    public int currentSA = 0;
    public bool isCrouching = false;
    public bool isGuarding = false;
    public bool isShieldBashing = false;
    public int currentGuard = 0;
    public int extraSA = 0;
    public float[] guardBox = new float[4];

    [Header("Counter Logic")]
    public bool isCounterStance = false;
    public bool triggerCounter = false;
    public string derivedMoveID = "";

    [Header("UI & View References")]
    public TextMeshProUGUI nameText;
    public Slider hpSlider;
    public Slider delayedHpSlider;
    public StatusIconController statusIcon;
    public SpriteRenderer footRing;
    public bool isPlayer;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    private bool isCurrentlyVisualCrouching = false;
    private float startY;
    private float velocityY = 0f;
    private float velocityX = 0f;
    private float moveTimer = 0f;
    private int currentJumpCount = 0;
    private float delayTimer = 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        originalScale = transform.localScale;
    }

    private void Start()
    {
        if (DataManager.Instance != null)
        {
            charaData = DataManager.Instance.GetChara(charaId);
            if (charaData != null)
            {
                fighterName = charaData.charaName;
                maxHp = charaData.maxHp;
                currentHp = maxHp;
                moveSpeed = charaData.moveSpeedMultiplier;
                currentGuard = charaData.guardEndurance;
            }
            else
            {
                Debug.LogWarning($"[Fighter] ID:{charaId} のマスターデータが存在しないため、初期ステータスでフォールバックします。");
                currentHp = maxHp;
            }
        }
        UpdateHPBar();
        startY = transform.position.y;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return; // タイムスケール停止時は物理演算を完全にバイパス

        ProcessDelayedHpUI();
        ProcessMovementPhysics();
    }

    private void LateUpdate()
    {
        // 座標計算完了後、ステージの境界線を越えないようにクランプ（フェイルセーフ）
        Vector3 currentPos = transform.position;
        currentPos.x = Mathf.Clamp(currentPos.x, stageLeftWall, stageRightWall);
        transform.position = currentPos;

        if (Time.timeScale == 0f) return;

        UpdateStatusIconView();
    }

    public void Init(CharaData data)
    {
        charaData = data;
        fighterName = data.charaName;
        maxHp = data.maxHp;
        currentHp = data.maxHp;
        moveSpeed = data.moveSpeedMultiplier;
        currentGuard = data.guardEndurance;

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value = currentHp;
        }
        if (delayedHpSlider != null)
        {
            delayedHpSlider.maxValue = maxHp;
            delayedHpSlider.value = currentHp;
        }
        UpdateUI();
    }

    /// <summary>
    /// 被ダメージ処理。引数肥大化を防ぐためDamageInfoオブジェクトで情報を受け取り、
    /// 内部でスーパーアーマー(SA)や反射(シールドバッシュ等)の軽減ロジックを完結させる。
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        int finalDamage = info.damage;

        // 反射の無限ループ防止と、SAによる特殊軽減処理
        if (!info.isReflect && info.isSAActive)
        {
            if (charaId == 7 && isShieldBashing)
            {
                int reflectDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.10f));
                DamageInfo reflectInfo = new DamageInfo { damage = reflectDamage, moveId = "702", attacker = this, isReflect = true, isSAActive = false };
                info.attacker.TakeDamage(reflectInfo);

                MoveData bashMove = DataManager.Instance.GetMove("702");
                if (bashMove != null && CommandInputSystem.Instance != null)
                {
                    bool isThisPlayer = (this == CommandInputSystem.Instance.playerFighter);
                    CommandInputSystem.Instance.RecordMoveHit(isThisPlayer, bashMove, reflectDamage);
                }
            }

            float saCutRate = (charaId == 7) ? 0.5f : (charaId == 2) ? 0.8f : 0.8f;
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * saCutRate));
        }

        currentHp = Mathf.Max(0, currentHp - finalDamage);
        StopAllCoroutines();
        StartCoroutine(FlashWhiteRoutine());
    }

    #region Physics & Movement API
    public void MoveX(float moveX, float duration)
    {
        if (duration <= 0) return;
        float actualMoveX = moveX * moveSpeed * distanceMultiplier;
        velocityX = actualMoveX / duration;
        moveTimer = duration;
    }

    public void Jump(float moveY)
    {
        if (moveY == 0) return;
        if (currentJumpCount < maxJumps)
        {
            velocityY = moveY * jumpForceMultiplier;
            currentJumpCount++;
        }
    }

    public void Knockup(float targetHeight)
    {
        isGrounded = false;
        if (targetHeight > 0) velocityY = Mathf.Sqrt(2f * gravity * targetHeight);
        else if (targetHeight < 0) velocityY = targetHeight * 5f;
    }

    /// <summary>
    /// 被弾時の硬直付与。予期せぬ姿勢（空中のしゃがみ等）での被弾バグを防ぐため、
    /// スタン時は強制的に姿勢フラグをリセットし安全な立ち状態へ復帰させる（フェイルセーフ）。
    /// </summary>
    public void AddStun(int frames)
    {
        if (frames > stunTimer) stunTimer = frames;
        isCrouching = false;
        SetVisualCrouch(false);
    }
    #endregion

    /// <summary>
    /// 現在の喰らい判定（Hurtbox）の矩形座標を計算して返す。
    /// アニメーションの見た目と判定のズレを防ぐため、現在のステート(しゃがみ等)から1F遅れず動的に算出する。
    /// </summary>
    public float[] GetHurtbox()
    {
        Vector3 pos = transform.position;
        float mult = distanceMultiplier;
        bool actuallyCrouching = isCrouching && isGrounded;

        float actualHeight = actuallyCrouching ? (bodyHeight * mult) * 0.5f : (bodyHeight * mult);
        float actualWidth = bodyWidth * mult;

        float minX = pos.x - (actualWidth / 2f);
        float maxX = pos.x + (actualWidth / 2f);
        float minY = pos.y - (actualHeight / 2f);
        float maxY = minY + actualHeight;

        return new float[] { minX, maxX, minY, maxY };
    }

    public void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value = currentHp;
        }
        if (delayedHpSlider != null)
        {
            delayedHpSlider.maxValue = maxHp;
            delayedHpSlider.value = currentHp;
        }
    }

    public void SetFacingDirection(int dir)
    {
        if (dir > 0) facingDir = 1;
        else if (dir < 0) facingDir = -1;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDir;
        transform.localScale = scale;
    }

    public void LookAtTarget(float targetX)
    {
        if (transform.position.x < targetX) facingDir = 1;
        else if (transform.position.x > targetX) facingDir = -1;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDir;
        transform.localScale = scale;
    }

    public void ResetStateToNeutral()
    {
        stunTimer = 0;
        isGuarding = false;
        isCrouching = false;
        isCounterStance = false;
        triggerCounter = false;
        SetVisualCrouch(false);
    }

    public void SetVisualCrouch(bool isCrouching)
    {
        if (isCurrentlyVisualCrouching == isCrouching) return;
        isCurrentlyVisualCrouching = isCrouching;

        if (isCrouching)
        {
            transform.localScale = new Vector3(originalScale.x, originalScale.y * 0.5f, originalScale.z);
            transform.position = new Vector3(transform.position.x, transform.position.y - (originalScale.y * 0.25f), transform.position.z);
        }
        else
        {
            transform.localScale = originalScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + (originalScale.y * 0.25f), transform.position.z);
        }
    }

    public void SetIndicatorColor(Color color)
    {
        if (footRing != null)
        {
            color.a = 0.5f;
            footRing.color = color;
        }
    }

    #region Internal Physics & UI Helpers
    private void ProcessMovementPhysics()
    {
        Vector3 pos = transform.position;

        if (moveTimer > 0)
        {
            pos.x += velocityX * Time.deltaTime;
            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0) velocityX = 0f;
        }

        if (pos.y > startY || velocityY > 0)
        {
            isGrounded = false;
            velocityY -= gravity * Time.deltaTime;
            pos.y += velocityY * Time.deltaTime;

            if (pos.y <= startY)
            {
                pos.y = startY;
                velocityY = 0f;
                currentJumpCount = 0;
                isGrounded = true;
            }
        }
        else
        {
            isGrounded = true;
        }
        transform.position = pos;
    }

    private void ProcessDelayedHpUI()
    {
        if (delayedHpSlider != null && hpSlider != null)
        {
            if (delayedHpSlider.value > hpSlider.value)
            {
                if (delayTimer > 0) delayTimer -= Time.deltaTime;
                else delayedHpSlider.value = Mathf.MoveTowards(delayedHpSlider.value, hpSlider.value, 500f * Time.deltaTime);
            }
        }
    }

    private void UpdateStatusIconView()
    {
        if (statusIcon != null)
        {
            if (currentSA > 0) statusIcon.ShowSA(currentSA);
            else if (isGuarding) statusIcon.ShowGuard();
            else statusIcon.HideIcon();
        }
    }

    private void UpdateUI()
    {
        if (nameText != null) nameText.text = fighterName;
        if (hpSlider != null) hpSlider.value = currentHp;
    }

    private IEnumerator FlashWhiteRoutine()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalColor;
    }
    #endregion

    [Header("Debug")]
    public bool showHurtbox = true;

    private void OnDrawGizmos()
    {
        if (!showHurtbox) return;

        float actualWidth = bodyWidth * distanceMultiplier;
        float actualHeight = bodyHeight * distanceMultiplier;
        bool actuallyCrouching = isCrouching && isGrounded;

        float currentHeight = actuallyCrouching ? (actualHeight * 0.5f) : actualHeight;
        float feetY = transform.position.y - (currentHeight / 2f);

        float minX = transform.position.x - (actualWidth / 2f);
        float maxX = transform.position.x + (actualWidth / 2f);
        float minY = feetY;
        float maxY = feetY + currentHeight;

        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, transform.position.z);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.1f);

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
}