using UnityEngine;

/// <summary>
/// 当たり判定（攻撃・ガード等）をリアルタイムに可視化するデバッグ用ビジュアライザ。
/// 見えない判定の調整工数を削減し、プランナーとの協業効率（DX）を高めるために使用する。
/// </summary>
public class HitboxVisualizer : MonoBehaviour
{
    public static HitboxVisualizer Instance { get; private set; }

    [Header("Visual Assets")]
    public Sprite squareSprite;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 指定された座標とサイズで当たり判定ブロックを動的に生成し、一定時間後に自動破棄する。
    /// </summary>
    /// <param name="position">描画する中心座標</param>
    /// <param name="size">判定の幅と高さ</param>
    /// <param name="color">判定の種類を示す色（赤：攻撃、青：ガード等）</param>
    /// <param name="duration">描画を持続させる時間（秒）</param>
    public void ShowHitbox(Vector2 position, Vector2 size, Color color, float duration = 0.5f)
    {
        GameObject hitboxObj = new GameObject("Debug_Hitbox");
        hitboxObj.transform.position = position;
        hitboxObj.transform.localScale = size;

        SpriteRenderer sr = hitboxObj.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;

        color.a = 0.5f; // 背景やキャラクターを視認できるよう半透明化
        sr.color = color;
        sr.sortingOrder = 100;

        Destroy(hitboxObj, duration);
    }
}