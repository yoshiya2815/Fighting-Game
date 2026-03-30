using UnityEngine;

/// <summary>
/// アクター（Fighter）の内部ステート（ガード、スーパーアーマー等）を視覚的に表現するViewコントローラー。
/// 内部の物理演算や状態遷移のロジックからは切り離されており、UIの描画とエフェクト再生にのみ責任を持つ。
/// </summary>
public class StatusIconController : MonoBehaviour
{
    [Header("Icon Settings")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Sprite shieldSprite;
    [SerializeField] private Sprite machoSprite;

    [Header("Aura Effects")]
    [SerializeField] private ParticleSystem auraParticle;

    private Vector3 baseScale;

    private void Start()
    {
        if (iconRenderer != null)
        {
            baseScale = iconRenderer.transform.localScale;
        }

        if (auraParticle != null)
        {
            auraParticle.Stop();
        }
    }

    /// <summary>
    /// ガード状態のアイコンを表示し、オーラエフェクトを無効化する
    /// </summary>
    public void ShowGuard()
    {
        if (iconRenderer == null || shieldSprite == null) return;

        iconRenderer.sprite = shieldSprite;
        iconRenderer.color = Color.white;
        iconRenderer.transform.localScale = baseScale;
        iconRenderer.gameObject.SetActive(true);

        if (auraParticle != null) auraParticle.Stop();
    }

    /// <summary>
    /// スーパーアーマー（SA）状態のアイコンを表示し、レベルに応じてスケールと色を動的に変化させる
    /// </summary>
    public void ShowSA(int saLevel)
    {
        if (iconRenderer == null || machoSprite == null) return;

        iconRenderer.sprite = machoSprite;
        iconRenderer.gameObject.SetActive(true);

        Color targetColor = Color.white;
        float targetScale = 1.0f;

        if (saLevel >= 3)
        {
            targetColor = Color.red;
            targetScale = 1.3f;
        }
        else if (saLevel == 2)
        {
            targetColor = Color.yellow;
            targetScale = 1.15f;
        }

        iconRenderer.color = targetColor;
        iconRenderer.transform.localScale = baseScale * targetScale;
    }

    /// <summary>
    /// ターン経過やバフによって常時発動するオーラエフェクトの色と再生状態を制御する
    /// </summary>
    public void UpdateAuraByLevel(int auraLevel)
    {
        if (auraParticle == null) return;

        if (auraLevel >= 1)
        {
            if (!auraParticle.isPlaying) auraParticle.Play();

            var main = auraParticle.main;
            if (auraLevel == 3) main.startColor = new Color(1f, 0f, 0f, 0.7f);
            else if (auraLevel == 2) main.startColor = new Color(1f, 0.5f, 0f, 0.6f);
            else if (auraLevel == 1) main.startColor = new Color(1f, 1f, 0f, 0.5f);
        }
        else
        {
            auraParticle.Stop();
        }
    }

    /// <summary>
    /// ステータスアイコンおよびエフェクトを非表示・停止状態にする
    /// </summary>
    public void HideIcon()
    {
        if (iconRenderer != null) iconRenderer.gameObject.SetActive(false);
        if (auraParticle != null) auraParticle.Stop();
    }
}