using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動物の種族を識別するコンポーネント。全ての動物(捕食者・被食者問わず)に付ける。
/// 同時に、種族ごとの一覧を静的に保持し、近くの動物を高速に検索できるようにする。
/// </summary>
public class AnimalIdentity : MonoBehaviour
{
    public enum AnimalSpecies
    {
        Cow, Horse, Zebra, Deer, Rabbit, Elephant, Tiger, Wolf
    }

    [Tooltip("この動物の種族")]
    public AnimalSpecies species;

    private static readonly Dictionary<AnimalSpecies, List<AnimalIdentity>> registry
        = new Dictionary<AnimalSpecies, List<AnimalIdentity>>();

    void OnEnable()
    {
        if (!registry.ContainsKey(species))
        {
            registry[species] = new List<AnimalIdentity>();
        }
        registry[species].Add(this);
    }

    void OnDisable()
    {
        if (registry.ContainsKey(species))
        {
            registry[species].Remove(this);
        }
    }

    public static AnimalIdentity FindNearest(AnimalSpecies targetSpecies, Vector3 fromPosition, AnimalIdentity exclude = null)
    {
        if (!registry.ContainsKey(targetSpecies)) return null;

        AnimalIdentity nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var animal in registry[targetSpecies])
        {
            if (animal == exclude || animal == null) continue;

            float dist = Vector3.Distance(fromPosition, animal.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = animal;
            }
        }
        return nearest;
    }
}