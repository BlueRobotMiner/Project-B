// DecorGroup
// Put on a decor group (gravestones, pumpkins, pumpkin lights, cauldrons).
// Its children are the individual pieces. Roll() hides them all, then shows a random
// number between Min Active and Max Active, so every room looks different.
using System.Collections.Generic;
using UnityEngine;

public class DecorGroup : MonoBehaviour
{
    [SerializeField] private int minActive = 0;
    [SerializeField] private int maxActive = 3;

    public void Roll()
    {
        int count = transform.childCount;
        if (count == 0) return;

        // Shuffled list of child indexes
        var indexes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            indexes.Add(i);
            transform.GetChild(i).gameObject.SetActive(false);
        }
        for (int i = indexes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indexes[i], indexes[j]) = (indexes[j], indexes[i]);
        }

        int target = Mathf.Clamp(Random.Range(minActive, maxActive + 1), 0, count);
        for (int i = 0; i < target; i++)
            transform.GetChild(indexes[i]).gameObject.SetActive(true);
    }
}
