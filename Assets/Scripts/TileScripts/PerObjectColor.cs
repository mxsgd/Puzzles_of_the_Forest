using UnityEngine;

/// <summary>
/// Ustawia per-obiektowy _BaseColor przez MaterialPropertyBlock
/// bez tworzenia instancji materiału.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PerObjectColor : MonoBehaviour
{
    [SerializeField] private Color baseColor = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock _sharedBlock;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Apply();
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        Apply();
    }

    public void Apply()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();
        if (_renderer == null)
            return;

        if (_sharedBlock == null)
            _sharedBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_sharedBlock);
        _sharedBlock.SetColor(BaseColorId, baseColor);
        _renderer.SetPropertyBlock(_sharedBlock);
    }
}
