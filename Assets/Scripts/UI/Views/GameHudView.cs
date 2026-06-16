using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Referencje do elementów HUD-u gry — edytuj w Hierarchy/Inspectorze.
/// Wygeneruj hierarchię: Idle Forest → UI → Generate Gameplay HUD.
/// </summary>
[DisallowMultipleComponent]
public class GameHudView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Canvas canvas;

    [Header("Score")]
    [SerializeField] private TextMeshProUGUI scoreValueLabel;
    [SerializeField] private TextMeshProUGUI habitatCountLabel;

    [Header("Next tile")]
    [SerializeField] private Image nextTileIcon;
    [SerializeField] private TextMeshProUGUI nextTileName;
    [SerializeField] private TextMeshProUGUI nextTileQueue;

    [Header("Buttons")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollLabel;
    [SerializeField] private Button pauseButton;

    [Header("Pause menu")]
    [SerializeField] private PauseMenuView pauseMenu;

    public Canvas Canvas => canvas;
    public TextMeshProUGUI ScoreValueLabel => scoreValueLabel;
    public TextMeshProUGUI HabitatCountLabel => habitatCountLabel;
    public Image NextTileIcon => nextTileIcon;
    public TextMeshProUGUI NextTileName => nextTileName;
    public TextMeshProUGUI NextTileQueue => nextTileQueue;
    public Button RerollButton => rerollButton;
    public TextMeshProUGUI RerollLabel => rerollLabel;
    public Button PauseButton => pauseButton;
    public PauseMenuView PauseMenu => pauseMenu;

    public bool IsConfigured =>
        ResolveCanvas() != null
        && scoreValueLabel != null
        && habitatCountLabel != null
        && nextTileName != null
        && nextTileQueue != null
        && rerollButton != null
        && rerollLabel != null
        && pauseButton != null
        && pauseMenu != null;

    /// <summary>Canvas z Inspectora lub na tym samym GameObject.</summary>
    public Canvas ResolveCanvas()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        return canvas;
    }

    public string FormatMissingRefs()
    {
        var missing = new List<string>(8);
        if (ResolveCanvas() == null) missing.Add(nameof(canvas));
        if (scoreValueLabel == null) missing.Add(nameof(scoreValueLabel));
        if (habitatCountLabel == null) missing.Add(nameof(habitatCountLabel));
        if (nextTileName == null) missing.Add(nameof(nextTileName));
        if (nextTileQueue == null) missing.Add(nameof(nextTileQueue));
        if (rerollButton == null) missing.Add(nameof(rerollButton));
        if (rerollLabel == null) missing.Add(nameof(rerollLabel));
        if (pauseButton == null) missing.Add(nameof(pauseButton));
        if (pauseMenu == null) missing.Add(nameof(pauseMenu));
        return missing.Count == 0 ? "none" : string.Join(", ", missing);
    }
}
