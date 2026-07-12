using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class MaskedEffectMaterialCache : IDisposable
{
    private readonly string _ownerName;
    private readonly string _shaderName;
    private Material _runtimeMaterial;
    private Material _sourceMaterial;
    private bool _loggedMissingShader;

    public MaskedEffectMaterialCache(string ownerName, string shaderName)
    {
        _ownerName = ownerName;
        _shaderName = shaderName;
    }

    public Material Material => _runtimeMaterial;

    public int Version { get; private set; }

    public bool Ensure(Material sourceMaterial)
    {
        if (sourceMaterial != null)
        {
            if (_runtimeMaterial == null || _sourceMaterial != sourceMaterial)
            {
                Replace(new Material(sourceMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }, sourceMaterial);
            }

            return true;
        }

        if (_runtimeMaterial != null && _sourceMaterial == null)
        {
            return true;
        }

        Replace(CoreUtils.CreateEngineMaterial(_shaderName), null);

        if (_runtimeMaterial != null || _loggedMissingShader)
        {
            return _runtimeMaterial != null;
        }

        Debug.LogError($"{_ownerName} could not find shader '{_shaderName}'.");
        _loggedMissingShader = true;
        return false;
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_runtimeMaterial);
        _runtimeMaterial = null;
        _sourceMaterial = null;
        Version++;
    }

    private void Replace(Material runtimeMaterial, Material sourceMaterial)
    {
        CoreUtils.Destroy(_runtimeMaterial);
        _runtimeMaterial = runtimeMaterial;
        _sourceMaterial = sourceMaterial;
        Version++;
    }
}

public sealed class MaskedEffectMaterialInstance : IDisposable
{
    public MaskedEffectMaterialInstance(Material source)
    {
        Material = source == null
            ? null
            : new Material(source)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
    }

    public Material Material { get; private set; }

    public bool IsValid => Material != null;

    public void Dispose()
    {
        CoreUtils.Destroy(Material);
        Material = null;
    }
}

public sealed class MaskedEffectItemPool<T> : IDisposable where T : class, IDisposable
{
    private readonly Func<Material, T> _factory;
    private readonly List<T> _items = new();
    private int _sourceVersion = -1;

    public MaskedEffectItemPool(Func<Material, T> factory)
    {
        _factory = factory;
    }

    public T this[int index] => _items[index];

    public void EnsureCount(int count, Material sourceMaterial, int sourceVersion)
    {
        if (_sourceVersion != sourceVersion)
        {
            Clear();
            _sourceVersion = sourceVersion;
        }

        while (_items.Count < count)
        {
            _items.Add(_factory(sourceMaterial));
        }

        while (_items.Count > count)
        {
            int lastIndex = _items.Count - 1;
            _items[lastIndex].Dispose();
            _items.RemoveAt(lastIndex);
        }
    }

    public void Dispose()
    {
        Clear();
        _sourceVersion = -1;
    }

    private void Clear()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].Dispose();
        }

        _items.Clear();
    }
}
