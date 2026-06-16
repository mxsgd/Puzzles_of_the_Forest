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
        canvas != null
        && scoreValueLabel != null
        && habitatCountLabel != null
        && nextTileName != null
        && nextTileQueue != null
        && rerollButton != null
        && rerollLabel != null
        && pauseButton != null
        && pauseMenu != null;
}
