using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TileDeckUI : MonoBehaviour
{
    [Header("Źródła danych")]
    [SerializeField] private TileDeck deck;

    [Header("UI")]
    [SerializeField] private RectTransform container;
    [SerializeField] private GameObject tileEntryPrefab;
    [SerializeField, Range(1, 30)] private int previewCount = 5;

    private readonly List<GameObject> _entries = new List<GameObject>();

    private void Awake()
    {
        if (!deck)
            deck = FindAnyObjectByType<TileDeck>();
    }

    private void OnEnable()
    {
        if (deck != null)
            deck.DeckChanged += OnDeckChanged;

        RenderDeck(deck != null ? deck.GetQueuedTiles() : null);
    }

    private void OnDisable()
    {
        if (deck != null)
            deck.DeckChanged -= OnDeckChanged;

        ClearEntries();
    }

    private void OnDeckChanged(IReadOnlyList<TileDraw> deckState)
    {
        RenderDeck(deckState);
    }

    private void RenderDeck(IReadOnlyList<TileDraw> deckState)
    {
        ClearEntries();

        if (container == null || tileEntryPrefab == null)
            return;

        var state = deckState ?? deck?.GetQueuedTiles() ?? new List<TileDraw>();
        int count = Mathf.Min(previewCount, state.Count);

        for (int i = 0; i < count; i++)
        {
            var entry = Instantiate(tileEntryPrefab, container);
            ConfigureEntry(entry, state[i], i);
            _entries.Add(entry);
        }
    }

    private void ConfigureEntry(GameObject entry, TileDraw draw, int index)
    {
        if (!entry)
            return;

        var image = entry.GetComponentInChildren<Image>();
        if (image != null)
        {
            image.sprite = draw != null ? draw.icon : null;
            image.enabled = image.sprite != null;
            image.color = Color.white;
        }

        var text = entry.GetComponentInChildren<Text>();
        if (text != null)
        {
            // Typ kafla reprezentujemy ikoną, więc nie pokazujemy etykiety tekstowej biomu.
            text.text = string.Empty;
            text.enabled = false;
        }
    }

    private void ClearEntries()
    {
        foreach (var entry in _entries)
        {
            if (entry != null)
            {
                if (Application.isPlaying)
                    Destroy(entry);
                else
                    DestroyImmediate(entry);
            }
        }

        _entries.Clear();
    }
}