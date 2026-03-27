using UnityEngine;
using UnityEngine.UI; // スライダーを使うために必要

public class HPGaugeController : MonoBehaviour
{
    [Header("UI設定")]
    public Slider mainSlider;      // 今ある手前のHPスライダー（緑）
    public Slider delayedSlider;   // 後ろに置いた赤スライダー

    [Header("演出設定")]
    public float delayTime = 0.5f;     // ダメージを受けてから赤ゲージが減り始めるまでの待機時間

    // ※注意：スライダーの最大値がHP(1000など)の場合、減る速度も大きくする必要があります
    public float decreaseSpeed = 1000f; // 1秒間に減るHPの量（HP1000なら、500で2秒かけて減る）

    private float delayTimer = 0f;

    // バトル開始時などに最大HPを設定する
    public void InitHP(float maxHP)
    {
        mainSlider.maxValue = maxHP;
        mainSlider.value = maxHP;

        delayedSlider.maxValue = maxHP;
        delayedSlider.value = maxHP;
    }

    // ダメージを受けた時に呼ばれる関数
    public void UpdateHP(float currentHP)
    {
        // メインのゲージは即座に現在のHPまで減らす
        mainSlider.value = currentHP;

        // 赤ゲージを減らすためのタイマーをリセット（待機開始）
        delayTimer = delayTime;
    }

    void Update()
    {
        // 赤ゲージがメインゲージより多い（＝ダメージを受けた）場合のみ処理
        if (delayedSlider.value > mainSlider.value)
        {
            if (delayTimer > 0)
            {
                // 待機時間を減らす
                delayTimer -= Time.deltaTime;
                //Debug.Log($"【HPゲージ】赤ゲージ待機中... 残り時間: {delayTimer:F2}秒");
            }
            else
            {
                //Debug.Log("【HPゲージ】待機完了！赤ゲージ減少スタート！！");
                // 待機が終わったら、赤ゲージをメインゲージの位置まで滑らかに減らす
                float dynamicSpeed = delayedSlider.maxValue * 0.5f; // 1秒間に最大HPの50%分を減らす
                delayedSlider.value = Mathf.MoveTowards(delayedSlider.value, mainSlider.value, dynamicSpeed * Time.deltaTime);
            }
        }
    }
}