using System;
using System.Collections.Generic;
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
        ResolveCanvas() != null
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

    public bool HasMinimumBindings
    {
        get
        {
            ResolveCanvas();
            EnsureMainMenuRoot();
            return canvas != null && playButton != null && mainMenuRoot != null;
        }
    }

    public RectTransform GetMainMenuRoot()
    {
        EnsureMainMenuRoot();
        return mainMenuRoot;
    }

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

    public Canvas ResolveCanvas()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        return canvas;
    }

    /// <summary>
    /// Podpina referencje z Hierarchy — po nazwach generatora, tekście na przycisku lub strukturze.
    /// </summary>
    public bool TryAutoBindFromHierarchy()
    {
        ResolveCanvas();
        if (canvas == null) return false;

        mainMenuRoot ??= FindRectDeep("MainMenu");
        gameOverRoot ??= FindRectDeep("GameOver");
        howToPlayRoot ??= FindRectDeep("HowToPlay");

        playButton ??= FindButtonNamed("PLAY_Btn") ?? FindButtonByLabel("PLAY");
        howToPlayButton ??= FindButtonNamed("HOW TO PLAY_Btn") ?? FindButtonByLabel("HOW TO PLAY");
        quitButton ??= FindButtonUnder(mainMenuRoot, "QUIT_Btn")
                       ?? FindButtonByLabel("QUIT")
                       ?? FindButtonNamed("QUIT_Btn");
        restartButton ??= FindButtonNamed("RESTART_Btn") ?? FindButtonByLabel("RESTART");
        mainMenuButton ??= FindButtonNamed("MAIN MENU_Btn") ?? FindButtonByLabel("MAIN MENU");
        howToPlayOkButton ??= FindButtonNamed("OK_Btn") ?? FindButtonByLabel("OK");

        EnsureMainMenuRoot();
        BindGameOverStats();
        BindHowToPlayRoot();

        return HasMinimumBindings;
    }

    private void EnsureMainMenuRoot()
    {
        if (mainMenuRoot != null) return;

        if (playButton != null)
            mainMenuRoot = FindCanvasChildPanelContaining(playButton.transform);

        if (mainMenuRoot == null)
        {
            foreach (var rt in GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.parent != transform) continue;
                if (rt.GetComponentInChildren<Button>(true) != null)
                {
                    mainMenuRoot = rt;
                    break;
                }
            }
        }
    }

    private void BindHowToPlayRoot()
    {
        if (howToPlayRoot != null || howToPlayButton == null) return;
        howToPlayRoot = FindCanvasChildPanelContaining(howToPlayButton.transform);
    }

    private RectTransform FindCanvasChildPanelContaining(Transform leaf)
    {
        if (leaf == null) return null;
        var current = leaf;
        while (current != null && current.parent != transform)
            current = current.parent;
        return current as RectTransform;
    }

    private void BindGameOverStats()
    {
        if (gameOverRoot == null) return;

        var card = gameOverRoot.Find("Backdrop/GameOverCard") as RectTransform
                   ?? gameOverRoot.Find("GameOverCard") as RectTransform
                   ?? FindRectDeepUnder(gameOverRoot, "GameOverCard");

        if (card == null)
        {
            foreach (var rt in gameOverRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.GetComponent<Image>() != null && rt.childCount > 0)
                {
                    card = rt;
                    break;
                }
            }
        }

        if (card == null) return;

        var values = new List<TextMeshProUGUI>();
        foreach (var tmp in card.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == "Value")
                values.Add(tmp);
        }

        if (values.Count == 0)
        {
            foreach (var tmp in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var caption = tmp.transform.parent != null
                    ? tmp.transform.parent.GetComponentInChildren<TextMeshProUGUI>()
                    : null;
                if (caption != null && caption != tmp && caption.text.Contains("Score", StringComparison.OrdinalIgnoreCase))
                    gameOverScore ??= tmp;
            }

            foreach (var tmp in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.fontSize >= 28f && gameOverScore == null)
                    gameOverScore = tmp;
            }
        }

        values.Sort((a, b) =>
            b.rectTransform.anchoredPosition.y.CompareTo(a.rectTransform.anchoredPosition.y));

        if (gameOverScore == null && values.Count > 0) gameOverScore = values[0];
        if (gameOverHabitats == null && values.Count > 1) gameOverHabitats = values[1];
        if (gameOverBestChain == null && values.Count > 2) gameOverBestChain = values[2];
    }

    private RectTransform FindRectDeep(string childName)
    {
        foreach (var rt in GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.gameObject.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return rt;
        }
        return null;
    }

    private static RectTransform FindRectDeepUnder(Transform root, string childName)
    {
        foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.gameObject.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return rt;
        }
        return null;
    }

    private Button FindButtonNamed(string goName)
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name.Equals(goName, StringComparison.OrdinalIgnoreCase))
                return btn;
        }
        return null;
    }

    private Button FindButtonByLabel(string text)
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && string.Equals(tmp.text.Trim(), text, StringComparison.OrdinalIgnoreCase))
                return btn;
        }
        return null;
    }

    private static Button FindButtonUnder(Transform root, string goName)
    {
        if (root == null) return null;
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name == goName) return btn;
        }
        return null;
    }

    public string FormatMissingRefs()
    {
        var missing = new List<string>(12);
        if (ResolveCanvas() == null) missing.Add(nameof(canvas));
        if (mainMenuRoot == null) missing.Add(nameof(mainMenuRoot));
        if (gameOverRoot == null) missing.Add(nameof(gameOverRoot));
        if (howToPlayRoot == null) missing.Add(nameof(howToPlayRoot));
        if (playButton == null) missing.Add(nameof(playButton));
        if (howToPlayButton == null) missing.Add(nameof(howToPlayButton));
        if (quitButton == null) missing.Add(nameof(quitButton));
        if (gameOverScore == null) missing.Add(nameof(gameOverScore));
        if (gameOverHabitats == null) missing.Add(nameof(gameOverHabitats));
        if (gameOverBestChain == null) missing.Add(nameof(gameOverBestChain));
        if (restartButton == null) missing.Add(nameof(restartButton));
        if (mainMenuButton == null) missing.Add(nameof(mainMenuButton));
        if (howToPlayOkButton == null) missing.Add(nameof(howToPlayOkButton));
        return missing.Count == 0 ? "none" : string.Join(", ", missing);
    }
}
