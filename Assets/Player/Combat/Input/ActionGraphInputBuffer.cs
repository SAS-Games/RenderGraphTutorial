using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActionGraphInputBuffer : MonoBehaviour
{
    public const string BlackboardKey = "ActionGraph.InputBuffer";

    [SerializeField] [Min(1)] private int safetyCapacity = 16;
    private readonly List<string> bufferedInputs = new();

    public int Count => bufferedInputs.Count;

    public bool Publish(string inputName)
    {
        if (string.IsNullOrWhiteSpace(inputName) || bufferedInputs.Count >= safetyCapacity)
            return false;

        bufferedInputs.Add(inputName);
        return true;
    }

    public bool TryConsume(string inputName)
    {
        for (int i = 0; i < bufferedInputs.Count; i++)
        {
            if (!string.Equals(bufferedInputs[i], inputName, System.StringComparison.Ordinal))
                continue;

            bufferedInputs.RemoveAt(i);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        bufferedInputs.Clear();
    }
}