using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class Fighter : MonoBehaviour
{
    public CharaData charaData; // ★追加：自分のキャラデータを丸ごと記憶しておく

    [Header("キャラクター設定")]
    // ==========================================
    // ★追加：このキャラクターのID（0:基本キャラ, 2:重戦車, 3:スピード型）
    // ==========================================
    public int charaId = 2;

    private Vector3 originalScale;
    private bool isCurrentlyVisualCrouching = false;

    [Header("現在のステータス")]
    public string fighterName = "Player";
    public int maxHp = 100;
    public int currentHp;
    public float moveSpeed;

    // ▼▼▼ 追加：キャラクターの体の大きさ ▼▼▼
    [Header("当たり判定（喰らい判定）")]
    public float bodyWidth = 1.0f;  // 横幅（初期値1.0）
    public float bodyHeight = 2.0f; // 高さ（初期値2.0）

    [Header("システム設定")]
    public float distanceMultiplier = 100f;

    [Header("物理演算（ジャンプ・重力）")]
    public float gravity = 300f;
    public float jumpForceMultiplier = 200f;
    public int maxJumps = 2;

    private float startY;
    private float velocityY = 0f;

    private float velocityX = 0f;
    private float moveTimer = 0f;
    private int currentJumpCount = 0;

    [Header("UI設定")]
    public TextMeshProUGUI nameText;
    public Slider hpSlider;
    public UnityEngine.UI.Slider delayedHpSlider; // 追加した赤スライダー
    private float delayTimer = 0f;                // 待機時間用のタイマー

    [Header("状態チェック")]
    public int facingDir = 1; // 1 = 右向き, -1 = 左向き
    public bool isGrounded = true; // 今、地面にいるか？
    public int stunTimer = 0; // ★追加：硬直（スタン）の残りフレーム数
    public int currentSA = 0; // ★追加：現在まとっているスーパーアーマー値

    public StatusIconController statusIcon;

    // ==========================================
    // ★追加：ステージの壁（限界座標）
    // ==========================================
    [Header("ステージ設定（壁）")]
    public float stageLeftWall = 10f;   // 左の壁のX座標（例：10）
    public float stageRightWall = 390f; // 右の壁のX座標（例：390）

    [Header("識別用設定")]
    public SpriteRenderer footRing;
    public bool isPlayer;             // 1P（自分）かどうか
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    // ▼追加：しゃがみ・ガードの状態
    public bool isCrouching = false;
    public bool isGuarding = false;
    public bool isShieldBashing = false;
    public int currentGuard = 0;     // 現在のガード耐久値

    public int extraSA = 0;

    // ★追加：ガード判定（青枠）の座標を記憶する配列 [minX, maxX, minY, maxY]
    public float[] guardBox = new float[4];

    // ==========================================
    // ★追加：カウンター（当身）用の記憶変数
    // ==========================================
    public bool isCounterStance = false; // 今、カウンター待ち状態か？
    public bool triggerCounter = false;  // 相手の攻撃を受け止めたか？
    public string derivedMoveID = "";    // 成功時に発動する派生技のID

    void Start()
    {
        if (DataManager.Instance != null)
        {
            charaData = DataManager.Instance.GetChara(charaId);

            if (charaData != null)
            {
                // CSVにデータがあった場合は上書き！
                fighterName = charaData.charaName;
                maxHp = charaData.maxHp;
                currentHp = maxHp;
                moveSpeed = charaData.moveSpeedMultiplier;
                currentGuard = charaData.guardEndurance;
            }
            else
            {
                // ★追加：CSVにデータがない（ID:0など）場合は、そのままの初期値を設定する
                UnityEngine.Debug.LogWarning($"【警告】{gameObject.name} が ID:{charaId} のデータを要求しましたが、DataManagerに存在しないため初期ステータスになります！");
                currentHp = maxHp;
            }
        }

        UpdateHPBar();
        startY = transform.position.y;
    }

    void Update()
    {
        // ==========================================
        // ★追加：時間が止まっている間は、落下計算なども完全にパスする！
        // ==========================================
        if (Time.timeScale == 0f) return;

        if (this.delayedHpSlider != null && this.hpSlider != null)
        {
            // 赤ゲージが緑ゲージより多い（＝ダメージを食らった）時だけ処理
            if (this.delayedHpSlider.value > this.hpSlider.value)
            {
                if (this.delayTimer > 0)
                {
                    // まずは待機時間を減らす
                    this.delayTimer -= Time.deltaTime;
                }
                else
                {
                    // 待機が終わったら、赤ゲージを緑ゲージの位置まで滑らかに減らす
                    // ※ 500f の部分は減るスピードです。HPの最大値に合わせて調整してください
                    this.delayedHpSlider.value = Mathf.MoveTowards(
                        this.delayedHpSlider.value,
                        this.hpSlider.value,
                        500f * Time.deltaTime
                    );
                }
            }
        }

        Vector3 pos = transform.position;

        // X方向（左右）の滑らかな移動
        if (moveTimer > 0)
        {
            pos.x += velocityX * Time.deltaTime;
            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0) velocityX = 0f;
        }

        // Y方向（ジャンプ・重力）の処理
        if (pos.y > startY || velocityY > 0)
        {
            isGrounded = false; // ★浮いている！
            velocityY -= gravity * Time.deltaTime;
            pos.y += velocityY * Time.deltaTime;

            if (pos.y <= startY)
            {
                pos.y = startY;
                velocityY = 0f;
                currentJumpCount = 0;
                isGrounded = true;  // ★着地した！
            }
        }
        else
        {
            isGrounded = true; // 動いていなくて地面と同じ高さなら地上
        }

        transform.position = pos;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // 起動時の色（1Pの青や2Pの赤など）を保存しておく
        originalColor = spriteRenderer.color;
        originalScale = transform.localScale;
    }

    public void SetVisualCrouch(bool isCrouching)
    {
        if (isCurrentlyVisualCrouching == isCrouching) return;

        isCurrentlyVisualCrouching = isCrouching;

        if (isCrouching)
        {
            // 高さを半分（0.5倍）にする
            transform.localScale = new Vector3(originalScale.x, originalScale.y * 0.5f, originalScale.z);

            // そのままだと宙に浮いてしまうため、Y座標を少し下げる
            // （元の高さの1/4だけ下げると、足元が元の位置にピタッと合います）
            transform.position = new Vector3(transform.position.x, transform.position.y - (originalScale.y * 0.25f), transform.position.z);
        }
        else
        {
            // 大きさを元に戻す
            transform.localScale = originalScale;

            // 下げたY座標を元に戻す
            transform.position = new Vector3(transform.position.x, transform.position.y + (originalScale.y * 0.25f), transform.position.z);
        }
    }

    // ==========================================
    // ★追加：すべての移動が終わった後に呼ばれるメソッド
    // ==========================================
    void LateUpdate()
    {
        // ==========================================
        // ① 既存の処理（壁での位置補正）
        // ==========================================
        // 1. 現在のキャラクターの位置を取得
        Vector3 currentPos = transform.position;

        // 2. X座標が「左の壁」と「右の壁」の間からハミ出していたら、強制的に範囲内に収める（Clamp）
        currentPos.x = Mathf.Clamp(currentPos.x, stageLeftWall, stageRightWall);

        // 3. はみ出しを修正したクリーンな位置を、キャラクターに戻す
        transform.position = currentPos;


        // ==========================================
        // ② 追加：状態アイコンのUI更新
        // ==========================================
        // 時間が止まっている時は表示の更新処理をパスする
        if (Time.timeScale == 0f) return;

        // 「最終的に確定した状態」を見て、アイコンを更新する
        if (this.statusIcon != null)
        {
            if (this.currentSA > 0)
            {
                this.statusIcon.ShowSA(this.currentSA);
            }
            else if (this.isGuarding)
            {
                this.statusIcon.ShowGuard();
            }
            else
            {
                this.statusIcon.HideIcon();
            }
        }
    }

    public void Init(CharaData data)
    {
        charaData = data;

        fighterName = data.charaName;
        maxHp = data.maxHp;
        currentHp = data.maxHp;
        moveSpeed = data.moveSpeedMultiplier;

        // ▼追加：初期状態のガード耐久値をキャラデータからセットする
        currentGuard = data.guardEndurance;

        // ★追加：ゲージの最大値をキャラのMaxHPに合わせる
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;
            hpSlider.value = currentHp;
        }

        if (this.delayedHpSlider != null)
        {
            this.delayedHpSlider.maxValue = this.maxHp;
            this.delayedHpSlider.value = this.currentHp;
        }

        UpdateUI();
    }

    // 引数を DamageInfo 型に変更！
    public void TakeDamage(DamageInfo info)
    {
        int finalDamage = info.damage;

        // ※無限ループ防止のため、反射ダメージには反射を返さない
        if (!info.isReflect)
        {
            // 攻撃がSAで耐えられる範囲だった場合のみ、反射＆軽減処理を行う
            if (info.isSAActive)
            {
                // ▼ シールドバッシュの反射処理
                // （※ isShieldBashing の部分は、ご自身の環境のフラグ変数等に置き換えてください）
                if (this.charaId == 7 && this.isShieldBashing)
                {
                    // 元のダメージの10%を反射
                    int reflectDamage = Mathf.RoundToInt(finalDamage * 0.10f);
                    reflectDamage = Mathf.Max(1, reflectDamage); // 最低1ダメージ

                    // 反射用の情報パックを作って相手に投げる
                    DamageInfo reflectInfo = new DamageInfo
                    {
                        damage = reflectDamage,
                        moveId = "702",
                        attacker = this,
                        isReflect = true,
                        isSAActive = false
                    };
                    info.attacker.TakeDamage(reflectInfo);

                    UnityEngine.Debug.Log($"【茨の盾】シールドバッシュで {reflectDamage} ダメージを反射！");

                    MoveData bashMove = DataManager.Instance.GetMove("702");
                    if (bashMove != null && CommandInputSystem.Instance != null)
                    {
                        // ダメージを受けた「自分(this)」がプレイヤー枠かどうかを判定
                        bool isThisPlayer = (this == CommandInputSystem.Instance.playerFighter);

                        // メインスクリプトのログ記録関数を呼び出す
                        CommandInputSystem.Instance.RecordMoveHit(isThisPlayer, bashMove, reflectDamage);
                    }
                }

                // ▼ SAによるダメージ軽減処理
                float saCutRate = 0.8f;
                if (this.charaId == 7) saCutRate = 0.5f;      // シールダーは大楯で50%カット
                else if (this.charaId == 2) saCutRate = 0.8f; // 重戦車は肉体で20%カット

                finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * saCutRate));
                UnityEngine.Debug.Log($"【SA軽減】アーマーでダメージを軽減！（最終ダメージ: {finalDamage}）");
            }
        }

        // 最終的なダメージをHPから引く処理（既存のコードと同じ）
        this.currentHp -= finalDamage;
        StopAllCoroutines(); // 連撃を受けた時のために、前のフラッシュを止める
        StartCoroutine(FlashWhiteRoutine());
        // HPスライダーの更新や死亡処理などが続く...
        if (this.currentHp < 0) this.currentHp = 0;
    }

    // ▼▼▼ 今回の修正点：X移動とY移動を独立させました ▼▼▼

    /// <summary>指定した時間をかけてX方向に滑らかに移動する</summary>
    public void MoveX(float moveX, float duration)
    {
        if (duration <= 0) return;
        float actualMoveX = moveX * moveSpeed * distanceMultiplier;
        velocityX = actualMoveX / duration; // 1秒あたりの速度を計算
        moveTimer = duration;               // タイマーセット
    }

    /// <summary>上方向（または下方向）にジャンプの勢いをつける</summary>
    public void Jump(float moveY)
    {
        // ▼▼▼ この1行を追加（0の時はジャンプ回数を消費しない！） ▼▼▼
        if (moveY == 0) return;
        // 勢いがマイナス（急降下など）の場合はジャンプ回数を消費しなくても良いなどの調整も可能ですが、一旦すべてカウントします
        if (currentJumpCount < maxJumps)
        {
            velocityY = moveY * jumpForceMultiplier;
            currentJumpCount++;
            Debug.Log($"【ジャンプ】{fighterName} がY方向に {moveY} の勢いで飛んだ！");
        }
    }

    // ▼▼▼ 今回新しく追加するメソッド ▼▼▼
    /// <summary>
    /// 攻撃を喰らって空中に打ち上げられる（ノックアップ）
    /// </summary>
    /// <param name="targetHeight">打ち上げたい高さ（マス数）</param>
    public void Knockup(float targetHeight)
    {
        isGrounded = false;

        if (targetHeight > 0)
        {
            // 物理学の公式：高さ(h)まで届くための初速(v) = √(2 * 重力 * 高さ)
            // これで、CSVの「1」＝「1マス分ピッタリ浮く」ようになります！
            velocityY = Mathf.Sqrt(2f * gravity * targetHeight);
        }
        else if (targetHeight < 0)
        {
            // マイナスの場合は、空中から地面に叩きつける（下向きの力）
            velocityY = targetHeight * 5f;
        }

        Debug.Log($"{fighterName} は {targetHeight} マス分、縦に吹っ飛んだ！");
    }

    /// <summary>
    /// 攻撃を喰らって硬直（スタン）する
    /// </summary>
    /// <param name="frames">硬直するフレーム数</param>
    public void AddStun(int frames)
    {
        // 連続で攻撃を喰らった場合は、長い方の硬直で上書きする
        if (frames > stunTimer)
        {
            stunTimer = frames;
        }

        // ▼▼▼ この1行を追加！ ▼▼▼
        // 攻撃を喰らって怯んだら、しゃがみ姿勢が崩れて強制的に「立ち状態（喰らい判定リセット）」になる！
        isCrouching = false;

        Debug.Log($"{fighterName} は {frames} フレームの間、身動きが取れない！");
        SetVisualCrouch(false);
    }

    /// <summary>
    /// 現在の自分の当たり判定（喰らい判定）の四角形を計算して返す
    /// </summary>
    /// <returns>最小X, 最大X, 最小Y, 最大Y の配列</returns>
    public float[] GetHurtbox()
    {
        Vector3 pos = transform.position;
        float mult = distanceMultiplier;

        bool actuallyCrouching = isCrouching && isGrounded;

        // ★修正：もし「しゃがみ」状態なら、高さを半分にする！
        float actualHeight = actuallyCrouching ? (bodyHeight * mult) * 0.5f : (bodyHeight * mult);
        float actualWidth = bodyWidth * mult;

        float minX = pos.x - (actualWidth / 2f);
        float maxX = pos.x + (actualWidth / 2f);
        float minY = pos.y - (actualHeight / 2f); // 足元の位置は変わらない
        float maxY = minY + actualHeight; // 頭の位置が下がる！

        return new float[] { minX, maxX, minY, maxY };
    }

    // ==========================================
    // ★追加：HPバーの表示を、現在のHPに合わせるメソッド
    // ==========================================
    public void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHp;    // ゲージの最大値をセット
            hpSlider.value = currentHp;   // 現在のHPに合わせてゲージの長さを変更
        }

        if (this.delayedHpSlider != null)
        {
            this.delayedHpSlider.maxValue = this.maxHp;
            this.delayedHpSlider.value = this.currentHp;
        }
    }

    /// <summary>
    /// 自分の向きを更新し、見た目も反転させる
    /// </summary>
    public void SetFacingDirection(int dir)
    {
        // 1 か -1 以外が入らないように安全対策
        if (dir > 0) facingDir = 1;
        else if (dir < 0) facingDir = -1;

        // 見た目（画像の向き）も反転させる
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDir;
        transform.localScale = scale;
    }

    private void UpdateUI()
    {
        // 数字を消して、キャラクターの名前だけを硬派に表示！
        if (nameText != null) nameText.text = fighterName;

        // ダメージを受けたら、ゲージの長さを現在のHPに合わせる！
        if (hpSlider != null) hpSlider.value = currentHp;
    }
    // ==========================================
    // ▼ 追加：デバッグ用の当たり判定可視化 ▼
    // ==========================================
    [Header("デバッグ設定")]
    public bool showHurtbox = true; // チェックを外すと見えなくなります

    private void OnDrawGizmos()
    {
        if (!showHurtbox) return;

        // 現在の喰らい判定を計算
        float actualWidth = bodyWidth * distanceMultiplier;
        float actualHeight = bodyHeight * distanceMultiplier;

        bool actuallyCrouching = isCrouching && isGrounded;

        // 1. まず、今の状態の「本当の高さ」を決定する
        float currentHeight = actuallyCrouching ? (actualHeight * 0.5f) : actualHeight;

        // 2. 「今の本当の高さ」の半分を引いて、正しい足元の位置（feetY）を出す
        float feetY = transform.position.y - (currentHeight / 2f);

        float minX = transform.position.x - (actualWidth / 2f);
        float maxX = transform.position.x + (actualWidth / 2f);
        float minY = feetY;
        float maxY = feetY + currentHeight;

        // 四角形の中心とサイズを計算
        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, transform.position.z);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.1f);

        // 半透明の緑色で塗りつぶし
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(center, size);

        // 濃い緑色で枠線を描く
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }

    // ==========================================
    // ★追加：相手のX座標を渡すと、そっちを向く（反転する）メソッド
    // ==========================================
    public void LookAtTarget(float targetX)
    {
        // 自分より相手が右にいる場合
        if (transform.position.x < targetX)
        {
            facingDir = 1;  // 右向きフラグにする
        }
        // 自分より相手が左にいる場合
        else if (transform.position.x > targetX)
        {
            facingDir = -1; // 左向きフラグにする
        }

        // ▼見た目（画像の向きや当たり判定）を反転させる魔法のコード
        Vector3 scale = transform.localScale;
        // Abs(絶対値)に facingDir を掛けることで、1ならそのまま、-1ならマイナス反転になります！
        scale.x = Mathf.Abs(scale.x) * facingDir;
        transform.localScale = scale;
    }

    // Fighter.cs 側の処理イメージ
    public void ResetStateToNeutral()
    {
        stunTimer = 0;       // 仰け反りフラグを解除
        isGuarding = false;      // ガードフラグを解除
        isCrouching = false;
        isCounterStance = false;
        triggerCounter = false;   // アニメーションを強制的に待機状態へ
        SetVisualCrouch(false);
    }

    public void SetIndicatorColor(Color color)
    {
        if (footRing != null)
        {
            // アルファ値（透明度）を少し下げると、地面が透けて綺麗に見えます
            color.a = 0.5f;
            footRing.color = color;
        }
    }

    private IEnumerator FlashWhiteRoutine()
    {
        // 1. 色を真っ白にする
        spriteRenderer.color = Color.white;

        // 2. ほんの一瞬だけ待つ（0.1秒くらいが格ゲー的に気持ちいい）
        yield return new WaitForSeconds(0.1f);

        // 3. 元の色に戻す
        spriteRenderer.color = originalColor;
    }

}