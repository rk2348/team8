using UnityEngine;

/// <summary>
/// 捕食者であることを示すマーカーコンポーネント。
/// トラ、オオカミなど、被食者(FleeFromPredatorsを持つ動物)から
/// 「警戒すべき対象」として認識される動物にアタッチする。
/// </summary>
public class Predator : MonoBehaviour
{
    [Tooltip("捕食者の種類名(デバッグ表示用。任意の文字列でOK)")]
    public string predatorName = "Tiger";
}