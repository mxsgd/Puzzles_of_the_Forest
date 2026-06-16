using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Referencje do menu głównego / game over / how to play — edytuj w Hierarchy/Inspectorze.
/// Wygeneruj hierarchię: Idle Forest → UI → Generate Menu Flow UI.
/// </summary>
[DisallowMultipleComponent]
public class GameFlowMenuView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform mainMenuRoot;
    [SerializeField] private RectTransform gameOverRoot;
    [SerializeField] private RectTransform howToPlayRoot;

    [Header("Main menu")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button howToPlayButton;
    [SerializeField] private Button quitButton;

    [Header("Game over")]
    [SerializeField] private TextMeshProUGUI gameOverScore;
    [SerializeField] private TextMeshProUGUI gameOverHabitats;
    [SerializeField] private TextMeshProUGUI gameOverBestChain;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("How to play")]
    [SerializeField] private Button howToPlayOkButton;

    public Canvas Canvas => canvas;
    public RectTransform MainMenuRoot => mainMenuRoot;
    public RectTransform GameOverRoot => gameOverRoot;
    public RectTransform HowToPlayRoot => howToPlayRoot;
    public Button PlayButton => playButton;
    public Button HowToPlayButton => howToPlayButton;
    public Button QuitButton => quitButton;
    public TextMeshProUGUI GameOverScore => gameOverScore;
    public TextMeshProUGUI GameOverHabitats => gameOverHabitats;
    public TextMeshProUGUI GameOverBestChain => gameOverBestChain;
    public Button RestartButton => restartButton;
    public Button MainMenuButton => mainMenuButton;
    public Button HowToPlayOkButton => howToPlayOkButton;

    public bool IsConfigured =>
        canvas != null
        && mainMenuRoot != null
        && gameOverRoot != null
        && howToPlayRoot != null
        && playButton != null
        && howToPlayButton != null
        && quitButton != null
        && gameOverScore != null
        && gameOverHabitats != null
        && gameOverBestChain != null
        && restartButton != null
        && mainMenuButton != null
        && howToPlayOkButton != null;

    public void WireButtons(Action onPlay, Action onHowToPlay, Action onQuit,
        Action onRestart, Action onMainMenu, Action onHowToPlayOk)
    {
        Wire(playButton, onPlay);
        Wire(howToPlayButton, onHowToPlay);
        Wire(quitButton, onQuit);
        Wire(restartButton, onRestart);
        Wire(mainMenuButton, onMainMenu);
        Wire(howToPlayOkButton, onHowToPlayOk);
    }

    private static void Wire(Button button, Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action?.Invoke());
    }
}
