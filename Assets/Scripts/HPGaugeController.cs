using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バトル中のHPゲージ表示と、ダメージ時の遅延減少アニメーション（赤ゲージ）を制御するUIコントローラー。
/// 即座に減る現在HPと、遅れて減る赤ゲージを組み合わせることで、プレイヤーに「どれだけのダメージを受けたか」を視覚的に認識させるUXを提供する。
/// </summary>
public class HPGaugeController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider mainSlider;      // 即座に減少する現在HPスライダー
    [SerializeField] private Slider delayedSlider;   // 遅延して減少するダメージ量可視化スライダー

    [Header("Animation Settings")]
    [SerializeField] private float delayTime = 0.5f; // ダメージを受けてから赤ゲージが減り始めるまでの待機時間

    private float delayTimer = 0f;

    /// <summary>
    /// バトル開始時などに最大HPを設定し、ゲージを満タンに初期化する。
    /// </summary>
    /// <param name="maxHP">キャラクターの最大HP</param>
    public void InitHP(float maxHP)
    {
        mainSlider.maxValue = maxHP;
        mainSlider.value = maxHP;

        delayedSlider.maxValue = maxHP;
        delayedSlider.value = maxHP;
    }

    /// <summary>
    /// 被ダメージ時にメインゲージを即座に減らし、遅延ゲージの減少タイマーを起動する。
    /// </summary>
    /// <param name="currentHP">減少後の現在HP</param>
    public void UpdateHP(float currentHP)
    {
        mainSlider.value = currentHP;
        delayTimer = delayTime;
    }

    private void Update()
    {
        // 赤ゲージがメインゲージより多い（＝ダメージを食らった）状態でのみアニメーション処理を行う
        if (delayedSlider.value > mainSlider.value)
        {
            if (delayTimer > 0)
            {
                delayTimer -= Time.deltaTime;
            }
            else
            {
                // ゲージの最大値に依存せず、常に「1秒間に最大HPの50%分」の一定割合で滑らかに減らす動的スピード計算
                float dynamicSpeed = delayedSlider.maxValue * 0.5f;
                delayedSlider.value = Mathf.MoveTowards(delayedSlider.value, mainSlider.value, dynamicSpeed * Time.deltaTime);
            }
        }
    }
}