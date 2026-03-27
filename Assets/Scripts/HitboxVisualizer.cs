using UnityEngine;

public class HitboxVisualizer : MonoBehaviour
{
    // どこからでも呼び出せるようにする魔法（シングルトン化）
    public static HitboxVisualizer Instance;

    [Header("ベースになる四角形画像")]
    public Sprite squareSprite; // インスペクターで「Square」をセットしてください

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 当たり判定（ブロック）を画面に出現させる
    /// </summary>
    /// <param name="position">出す場所（X, Y）</param>
    /// <param name="size">ブロックの大きさ（幅, 高さ）</param>
    /// <param name="color">色（赤＝攻撃、青＝食らい判定など）</param>
    /// <param name="duration">何秒で消えるか（デフォルト0.5秒）</param>
    public void ShowHitbox(Vector2 position, Vector2 size, Color color, float duration = 0.5f)
    {
        // 1. 空のゲームオブジェクトを新規作成
        GameObject hitboxObj = new GameObject("Debug_Hitbox");
        hitboxObj.transform.position = position;
        hitboxObj.transform.localScale = size;

        // 2. 画像（SpriteRenderer）を追加して四角形にする
        SpriteRenderer sr = hitboxObj.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;

        // 3. 半透明にする（アルファ値を0.5にする）
        color.a = 0.5f;
        sr.color = color;

        // 4. キャラより手前に表示されるようにする
        sr.sortingOrder = 100;

        // 5. 指定した時間（duration）が経過したら自動で消滅させる！
        Destroy(hitboxObj, duration);
    }
}