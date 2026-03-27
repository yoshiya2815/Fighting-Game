using UnityEngine;

public class StatusIconController : MonoBehaviour
{
    [Header("アイコン表示用コンポーネント")]
    public SpriteRenderer iconRenderer;

    [Header("使用する画像（Sprite）")]
    public Sprite shieldSprite;
    public Sprite machoSprite;

    // ▼▼▼ 追加：オーラ用のParticle System ▼▼▼
    [Header("重戦車専用：オーラエフェクト")]
    public ParticleSystem auraParticle;

    private Vector3 baseScale;

    void Start()
    {
        if (iconRenderer != null)
        {
            baseScale = iconRenderer.transform.localScale;
        }

        // 開始時はオーラを止めておく
        if (auraParticle != null)
        {
            auraParticle.Stop();
        }
    }

    // ★ ガード（盾）の時はオーラを消す
    public void ShowGuard()
    {
        if (iconRenderer == null || shieldSprite == null) return;

        iconRenderer.sprite = shieldSprite;
        iconRenderer.color = Color.white;
        iconRenderer.transform.localScale = baseScale;
        iconRenderer.gameObject.SetActive(true);

        // ガードアイコン時はオーラを消す
        if (auraParticle != null) auraParticle.Stop();
    }

    // ★ 改造：SA（マッチョ）時は、アイコンの色変更 ＋ オーラの色変更！
    public void ShowSA(int saLevel)
    {
        if (iconRenderer == null || machoSprite == null) return;

        iconRenderer.sprite = machoSprite;
        iconRenderer.gameObject.SetActive(true);

        // --- 以前の処理（アイコンの色とサイズ変更） ---
        Color targetColor = Color.white;
        float targetScale = 1.0f;

        if (saLevel >= 3) // レベル3以上（赤）
        {
            targetColor = Color.red;
            targetScale = 1.3f;
        }
        else if (saLevel == 2) // レベル2（黄）
        {
            targetColor = Color.yellow;
            targetScale = 1.15f;
        }

        iconRenderer.color = targetColor;
        iconRenderer.transform.localScale = baseScale * targetScale;
    }

    // ==========================================
    // ▼▼▼ 追加：ターン経過によるオーラ常時発動の制御 ▼▼▼
    // ==========================================
    public void UpdateAuraByLevel(int auraLevel)
    {
        if (auraParticle == null) return;

        if (auraLevel >= 1)
        {
            if (!auraParticle.isPlaying) auraParticle.Play();

            var main = auraParticle.main;
            if (auraLevel == 3) main.startColor = new Color(1f, 0f, 0f, 0.7f); // 赤
            else if (auraLevel == 2) main.startColor = new Color(1f, 0.5f, 0f, 0.6f); // オレンジ
            else if (auraLevel == 1) main.startColor = new Color(1f, 1f, 0f, 0.5f); // 黄色
        }
        else
        {
            auraParticle.Stop(); // レベル0なら止める
        }
    }

    public void HideIcon()
    {
        if (iconRenderer != null)
        {
            iconRenderer.gameObject.SetActive(false);
        }

        // アイコン消滅時はオーラも止める
        if (auraParticle != null) auraParticle.Stop();
    }
}