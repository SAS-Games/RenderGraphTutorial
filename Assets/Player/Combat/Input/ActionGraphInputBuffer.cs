using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ActionGraphInputBuffer : MonoBehaviour
{
    public const string BlackboardKey = "ActionGraph.InputBuffer";

    [FormerlySerializedAs("safetyCapacity")] [SerializeField] [Min(1)]
    private int m_SafetyCapacity = 16;

    private readonly List<string> bufferedInputs = new();

    public int Count => bufferedInputs.Count;

    public bool Publish(string inputName)
    {
        if (string.IsNullOrWhiteSpace(inputName) || bufferedInputs.Count >= m_SafetyCapacity)
            return false;

        bufferedInputs.Add(inputName);
        return true;
    }

    public bool TryConsume(string inputName)
    {
        for (int i = 0; i < bufferedInputs.Count; i++)
        {
            if (!string.Equals(bufferedInputs[i], inputName, StringComparison.Ordinal))
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