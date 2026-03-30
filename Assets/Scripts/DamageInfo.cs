using UnityEngine;

/// <summary>
/// 攻撃のヒット時に生成され、攻撃側から防御側のアクターへ渡されるダメージ情報のデータコンテナ（DTO）。
/// 単なる数値の受け渡しではなく、技の属性や反射フラグを内包することで、
/// スーパーアーマーの破壊判定やシールドバッシュ時の反射処理など、複雑な相互作用を安全に解決する。
/// </summary>
public class DamageInfo
{
    /// <summary> 算出された最終的なダメージ量 </summary>
    public int damage;

    /// <summary> ヒットした技の固有ID </summary>
    public string moveId;

    /// <summary> 攻撃を行ったアクター（反射時の対象として使用） </summary>
    public Fighter attacker;

    /// <summary> このダメージが反射によって生成されたものかを示すフラグ（無限ループ防止用） </summary>
    public bool isReflect;

    /// <summary> この攻撃が防御側のスーパーアーマー耐久値以下（アーマー有効）であるかを示すフラグ </summary>
    public bool isSAActive;
}