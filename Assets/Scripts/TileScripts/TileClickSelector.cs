using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileClickSelector : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private TileQueryService query;
    [SerializeField] private TileSelectionModel selection;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float maxRayDistance = 500f;

    private TileGrid.Tile _selectedTile;

    public TileGrid.Tile SelectedTile => _selectedTile;

    public event Action<TileGrid.Tile> TileSelected;

    private void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!query) query = FindAnyObjectByType<TileQueryService>();
        if (!selection) selection = FindAnyObjectByType<TileSelectionModel>();
    }

    private void Update()
    {
        if (mainCamera == null || selection == null)
            return;

        if (!TryGetPointerDown(out var screenPosition, out var pointerId))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            return;

        if (RaycastToWorld(screenPosition, out var worldPoint) && query.TryGetNearestTile(worldPoint, out var tile, maxRayDistance))
        {
            SetSelection(tile);
        }
        else
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        ClearSelectionInternal(true);
    }

    private void SetSelection(TileGrid.Tile tile)
    {
        if (tile == null || tile == _selectedTile)
            return;

        _selectedTile = tile;
        if (selection != null)
            selection.SetSelectedTile(tile);

        TileSelected?.Invoke(tile);
    }

    private void ClearSelectionInternal(bool notifyGrid)
    {
        if (_selectedTile == null)
            return;

        _selectedTile = null;

        if (notifyGrid && selection != null)
            selection.ClearSelectedTile();

        TileSelected?.Invoke(null);
    }

    private bool TryGetPointerDown(out Vector2 position, out int pointerId)
    {
        if (TryGetTouchDown(out position, out pointerId))
            return true;

        if (TryGetMouseDown(out position, out pointerId))
            return true;

        if (TryGetPenDown(out position, out pointerId))
            return true;

        position = default;
        pointerId = -1;
        return false;
    }

    private bool RaycastToWorld(Vector2 screenPos, out Vector3 worldPoint)
    {
        var ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out var hitInfo, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            worldPoint = hitInfo.point;
            return true;
        }

        worldPoint = default;
        return false;
    }

    private static bool TryGetTouchDown(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = -1;

        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return false;

        foreach (var touch in touchscreen.touches)
        {
            if (!touch.press.wasPressedThisFrame)
                continue;

            position = touch.position.ReadValue();

            var touchId = touch.touchId.ReadValue();
            pointerId = -1;
            return true;
        }

        return false;
    }

    private static bool TryGetMouseDown(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = -1;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return false;

        position = mouse.position.ReadValue();
        pointerId = -1;
        return true;
    }

    private static bool TryGetPenDown(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = -1;

        var pen = Pen.current;
        if (pen == null || !pen.tip.wasPressedThisFrame)
            return false;

        position = pen.position.ReadValue();
        pointerId = -1;
        return true;
    }
}