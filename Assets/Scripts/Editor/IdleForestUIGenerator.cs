using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generuje hierarchię UI w scenie, żeby każdy element był edytowalny w Unity Inspectorze.
/// Menu: Idle Forest → UI → ...
/// </summary>
public static class IdleForestUIGenerator
{
    private const string HudRootName = "GameUI_Hud";
    private const string MenuRootName = "GameFlow_Menu";
    private const string QuestHudRootName = "QuestHud_Hud";

    [MenuItem("Idle Forest/UI/Generate Gameplay HUD")]
    public static void GenerateGameplayHud()
    {
        var gameUi = UnityEngine.Object.FindAnyObjectByType<GameUI>();
        Transform parent = gameUi != null ? gameUi.transform : null;
        if (parent == null)
        {
            var go = new GameObject("GameUI");
            go.AddComponent<GameUI>();
            parent = go.transform;
        }

        DestroyExistingChild(parent, HudRootName);
        var root = CreateCanvasRoot(parent, HudRootName, 100);
        var view = root.AddComponent<GameHudView>();

        var scoreCard = CreateCard(root.transform, "ScoreCard", new Vector2(300f, 130f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f));
        CreateLabel(scoreCard, "SCORE", 16f, UISpriteFactory.TextMuted,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 24f), new Vector2(0f, -14f), FontStyles.Bold, 6f);
        var scoreValue = CreateLabel(scoreCard, "0", 42f, UISpriteFactory.ScoreValue,
            new Vector2(0f, 0.2f), new Vector2(1f, 0.58f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0f, 10f), FontStyles.Bold);
        UISpriteFactory.ApplyScoreValueStyle(scoreValue);
        var habitatCount = CreateLabel(scoreCard, "Habitats: 0", 18f, UISpriteFactory.TextPrimary,
            new Vector2(0f, 0f), new Vector2(1f, 0.35f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        var nextCard = CreateCard(root.transform, "NextTileCard", new Vector2(320f, 190f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f));
        CreateLabel(nextCard, "NEXT TILE", 14f, UISpriteFactory.TextMuted,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 22f), new Vector2(0f, -12f), FontStyles.Bold, 4f);
        var icon = CreateImage(nextCard, "Icon", new Vector2(0f, 0.15f), new Vector2(0f, 0.85f),
            new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(20f, -5f));
        icon.preserveAspect = true;
        icon.enabled = false;
        var nextName = CreateLabel(nextCard, "—", 22f, UISpriteFactory.TextPrimary,
            new Vector2(0.4f, 0.45f), new Vector2(1f, 0.85f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-10f, 0f), FontStyles.Bold);
        nextName.alignment = TextAlignmentOptions.MidlineLeft;
        var nextQueue = CreateLabel(nextCard, "0", 16f, UISpriteFactory.TextMuted,
            new Vector2(0.4f, 0.15f), new Vector2(1f, 0.45f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-10f, 0f));
        nextQueue.alignment = TextAlignmentOptions.MidlineLeft;

        var rerollBtn = CreateButton(root.transform, "RerollBtn", "REROLL  (3)",
            UISpriteFactory.AccentGold, UISpriteFactory.TextOnAccent,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(320f, 60f), new Vector2(-28f, -228f));
        var rerollLabel = rerollBtn.GetComponentInChildren<TextMeshProUGUI>();

        var pauseBtn = CreateButton(root.transform, "PauseBtn", "II",
            UISpriteFactory.PanelDark, UISpriteFactory.TextPrimary,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(72f, 72f), new Vector2(28f, 28f));

        var pauseRoot = CreateFullScreen(root.transform, "PauseMenu");
        var pauseView = pauseRoot.gameObject.AddComponent<PauseMenuView>();
        BuildPauseMenu(pauseRoot, pauseView);

        AssignHudView(view, root, scoreValue, habitatCount, icon, nextName, nextQueue,
            rerollBtn, rerollLabel, pauseBtn, pauseView);

        var gameUiComp = parent.GetComponent<GameUI>();
        if (gameUiComp != null)
        {
            var so = new SerializedObject(gameUiComp);
            so.FindProperty("hudView").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameUiComp);
        }

        root.SetActive(false);
        Selection.activeGameObject = root;
        Debug.Log("[IdleForestUIGenerator] Gameplay HUD wygenerowany pod GameUI → GameUI_Hud. Edytuj w Hierarchy.");
    }

    [MenuItem("Idle Forest/UI/Generate Menu Flow UI")]
    public static void GenerateMenuFlowUi()
    {
        var flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
        Transform parent = flow != null ? flow.transform : null;
        if (parent == null)
        {
            EditorUtility.DisplayDialog("Idle Forest UI",
                "Brak GameFlowController w scenie. Dodaj go najpierw do sceny.", "OK");
            return;
        }

        DestroyExistingChild(parent, MenuRootName);
        var root = CreateCanvasRoot(parent, MenuRootName, 200);
        var view = root.AddComponent<GameFlowMenuView>();

        var mainMenu = CreateFullScreenPanel(root.transform, "MainMenu");
        var mainBackdrop = CreateBackdrop(mainMenu);
        var mainCard = CreateCenterCard(mainBackdrop, "MainMenuCard", new Vector2(520f, 520f));
        CreateMenuTitle(mainCard, "Puzzles of the Forest", 36f, new Vector2(0f, -48f));
        CreateMenuSubtitle(mainCard, "Place tiles, build habitats,\nscore as many points as you can", new Vector2(0f, -130f));
        var playBtn = CreateMenuButton(mainCard, "PLAY", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent, new Vector2(0f, -220f));
        var howBtn = CreateMenuButton(mainCard, "HOW TO PLAY", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary, new Vector2(0f, -300f));
        var quitBtn = CreateMenuButton(mainCard, "QUIT", UISpriteFactory.AccentRed, UISpriteFactory.TextOnAccent, new Vector2(0f, -380f));

        var gameOver = CreateFullScreenPanel(root.transform, "GameOver");
        gameOver.gameObject.SetActive(false);
        var goBackdrop = CreateBackdrop(gameOver);
        var goCard = CreateCenterCard(goBackdrop, "GameOverCard", new Vector2(560f, 580f));
        CreateMenuTitle(goCard, "GAME OVER", 34f, new Vector2(0f, -40f));
        CreateMenuSubtitle(goCard, "Deck empty — session ended", new Vector2(0f, -95f));
        var goScore = CreateStatLine(goCard, "Score", "0", UISpriteFactory.ScoreValue, 40f, new Vector2(0f, -175f));
        UISpriteFactory.ApplyScoreValueStyle(goScore);
        var goHabitats = CreateStatLine(goCard, "Habitats", "—", UISpriteFactory.TextPrimary, 24f, new Vector2(0f, -250f));
        var goChain = CreateStatLine(goCard, "Largest habitat", "—", UISpriteFactory.TextPrimary, 24f, new Vector2(0f, -310f));
        var restartBtn = CreateMenuButton(goCard, "RESTART", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent, new Vector2(0f, -400f));
        var mainMenuBtn = CreateMenuButton(goCard, "MAIN MENU", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary, new Vector2(0f, -480f));

        var howTo = CreateFullScreenPanel(root.transform, "HowToPlay");
        howTo.gameObject.SetActive(false);
        var htBackdrop = CreateBackdrop(howTo);
        var htCard = CreateCenterCard(htBackdrop, "HowToPlayCard", new Vector2(640f, 520f));
        CreateMenuTitle(htCard, "HOW TO PLAY", 30f, new Vector2(0f, -36f));
        CreateBodyText(htCard,
            "• You start with 30 tiles in the deck (+ free starting tile).\n" +
            "• Each habitat adds 3 random tiles to the deck.\n" +
            "• Place tiles on empty cells next to occupied ones.\n" +
            "• Build habitats — connected regions up to 5 tiles.\n" +
            "• Each biome has a vector (meadow, forest, bush, rock, water).\n" +
            "• Fewer tiles in a habitat means more points.\n" +
            "• When the deck runs out, the game ends.\n" +
            "• Reroll (3×) replaces the current card.",
            new Vector2(0f, -120f), new Vector2(560f, 280f));
        var okBtn = CreateMenuButton(htCard, "OK", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent, new Vector2(0f, -430f));

        AssignMenuView(view, root, mainMenu, gameOver, howTo,
            playBtn, howBtn, quitBtn, goScore, goHabitats, goChain, restartBtn, mainMenuBtn, okBtn);

        var so = new SerializedObject(flow);
        so.FindProperty("menuView").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flow);

        Selection.activeGameObject = root;
        Debug.Log("[IdleForestUIGenerator] Menu Flow UI wygenerowany pod GameFlowController → GameFlow_Menu.");
    }

    [MenuItem("Idle Forest/UI/Generate Quest HUD")]
    public static void GenerateQuestHud()
    {
        var flow = UnityEngine.Object.FindAnyObjectByType<GameFlowController>();
        var gameUi = UnityEngine.Object.FindAnyObjectByType<GameUI>();
        Transform parent = flow != null ? flow.transform : gameUi != null ? gameUi.transform : null;
        if (parent == null)
        {
            EditorUtility.DisplayDialog("Idle Forest UI",
                "Brak GameFlowController / GameUI w scenie.", "OK");
            return;
        }

        var staleOnParent = parent.GetComponent<QuestHudView>();
        if (staleOnParent != null)
            UnityEngine.Object.DestroyImmediate(staleOnParent);

        DestroyExistingChild(parent, QuestHudRootName);
        var root = CreateCanvasRoot(parent, QuestHudRootName, 99);
        var view = root.AddComponent<QuestHudView>();

        var panel = CreateQuestPanel(root.transform);
        float y = -16f;

        CreateQuestHeader(panel, "MAIN QUEST", ref y);
        var mqTitle = CreateQuestLabel(panel, "mq_title", "", 16f, Color.white, FontStyles.Bold, ref y, 2f);
        var mqGoal = CreateQuestLabel(panel, "mq_goal", "", 12f, new Color(0.85f, 0.85f, 0.85f), FontStyles.Normal, ref y, 4f);
        var mqProg = CreateQuestLabel(panel, "mq_prog", "", 11f, new Color(0.6f, 0.95f, 0.6f), FontStyles.Normal, ref y, 2f);
        var mqBar = CreateQuestSlider(panel, "mq_bar", ref y, 8f);
        var mqRew = CreateQuestLabel(panel, "mq_rew", "", 11f, new Color(0.9f, 0.75f, 0.35f), FontStyles.Italic, ref y, 12f);
        CreateQuestSeparator(panel, ref y);

        CreateQuestHeader(panel, "SIDE QUEST", ref y);
        var sqTitle = CreateQuestLabel(panel, "sq_title", "", 16f, Color.white, FontStyles.Bold, ref y, 2f);
        var sqGoal = CreateQuestLabel(panel, "sq_goal", "", 12f, new Color(0.85f, 0.85f, 0.85f), FontStyles.Normal, ref y, 4f);
        var sqProg = CreateQuestLabel(panel, "sq_prog", "", 11f, new Color(0.6f, 0.95f, 0.6f), FontStyles.Normal, ref y, 2f);
        var sqBar = CreateQuestSlider(panel, "sq_bar", ref y, 8f);
        var sqRew = CreateQuestLabel(panel, "sq_rew", "", 11f, new Color(0.9f, 0.75f, 0.35f), FontStyles.Italic, ref y, 4f);
        panel.sizeDelta = new Vector2(270f, -y + 16f);

        var banner = CreateQuestBanner(root.transform, out var bannerText);

        AssignQuestHudView(view, root, mqTitle, mqGoal, mqProg, mqBar, mqRew,
            sqTitle, sqGoal, sqProg, sqBar, sqRew, banner, bannerText);

        if (flow != null)
        {
            var so = new SerializedObject(flow);
            so.FindProperty("questHudView").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        root.SetActive(false);
        Selection.activeGameObject = root;
        Debug.Log("[IdleForestUIGenerator] Quest HUD wygenerowany → QuestHud_Hud. Edytuj w Hierarchy.");
    }

    [MenuItem("Idle Forest/UI/Generate All UI")]
    public static void GenerateAllUi()
    {
        GenerateGameplayHud();
        GenerateMenuFlowUi();
        GenerateQuestHud();
    }

    // ── Pause menu build ─────────────────────────────────────────────────────

    private static void BuildPauseMenu(RectTransform backdrop, PauseMenuView view)
    {
        var img = backdrop.gameObject.GetComponent<Image>();
        if (img == null) img = backdrop.gameObject.AddComponent<Image>();
        img.color = UISpriteFactory.Backdrop;
        img.raycastTarget = true;

        var group = backdrop.gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = backdrop.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        backdrop.gameObject.SetActive(false);

        var menuCard = CreateCenterCard(backdrop, "PauseMenuCard", new Vector2(420f, 460f));
        CreateMenuTitle(menuCard, "PAUSED", 36f, new Vector2(0f, -36f));
        var resume = CreateMenuButton(menuCard, "RESUME", UISpriteFactory.AccentGreen, UISpriteFactory.TextOnAccent, new Vector2(0f, -120f));
        var settings = CreateMenuButton(menuCard, "SETTINGS", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary, new Vector2(0f, -200f));
        var quit = CreateMenuButton(menuCard, "QUIT", UISpriteFactory.AccentRed, UISpriteFactory.TextOnAccent, new Vector2(0f, -280f));

        var settingsCard = CreateCenterCard(backdrop, "SettingsCard", new Vector2(460f, 380f));
        settingsCard.gameObject.SetActive(false);
        CreateMenuTitle(settingsCard, "SETTINGS", 36f, new Vector2(0f, -36f));
        var music = CreateSlider(settingsCard, "Music", new Vector2(0f, -130f));
        var sfx = CreateSlider(settingsCard, "SFX", new Vector2(0f, -210f));
        var back = CreateMenuButton(settingsCard, "BACK", UISpriteFactory.PanelLight, UISpriteFactory.TextPrimary, new Vector2(0f, -310f));

        AssignPauseView(view, group, backdrop, menuCard, settingsCard, resume, settings, quit, back, music, sfx);
    }

    // ── Serialized assign ────────────────────────────────────────────────────

    private static void AssignHudView(GameHudView view, GameObject canvas,
        TextMeshProUGUI score, TextMeshProUGUI habitats, Image icon,
        TextMeshProUGUI nextName, TextMeshProUGUI nextQueue,
        Button reroll, TextMeshProUGUI rerollLabel, Button pause, PauseMenuView pauseView)
    {
        var so = new SerializedObject(view);
        so.FindProperty("canvas").objectReferenceValue = canvas.GetComponent<Canvas>();
        so.FindProperty("scoreValueLabel").objectReferenceValue = score;
        so.FindProperty("habitatCountLabel").objectReferenceValue = habitats;
        so.FindProperty("nextTileIcon").objectReferenceValue = icon;
        so.FindProperty("nextTileName").objectReferenceValue = nextName;
        so.FindProperty("nextTileQueue").objectReferenceValue = nextQueue;
        so.FindProperty("rerollButton").objectReferenceValue = reroll;
        so.FindProperty("rerollLabel").objectReferenceValue = rerollLabel;
        so.FindProperty("pauseButton").objectReferenceValue = pause;
        so.FindProperty("pauseMenu").objectReferenceValue = pauseView;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPauseView(PauseMenuView view, CanvasGroup group, RectTransform backdrop,
        RectTransform menuCard, RectTransform settingsCard,
        Button resume, Button settings, Button quit, Button back, Slider music, Slider sfx)
    {
        var so = new SerializedObject(view);
        so.FindProperty("backdropGroup").objectReferenceValue = group;
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("menuCard").objectReferenceValue = menuCard;
        so.FindProperty("settingsCard").objectReferenceValue = settingsCard;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("settingsBackButton").objectReferenceValue = back;
        so.FindProperty("musicSlider").objectReferenceValue = music;
        so.FindProperty("sfxSlider").objectReferenceValue = sfx;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignMenuView(GameFlowMenuView view, GameObject canvas,
        RectTransform mainMenu, RectTransform gameOver, RectTransform howToPlay,
        Button play, Button howTo, Button quit,
        TextMeshProUGUI score, TextMeshProUGUI habitats, TextMeshProUGUI chain,
        Button restart, Button mainMenuBtn, Button ok)
    {
        var so = new SerializedObject(view);
        so.FindProperty("canvas").objectReferenceValue = canvas.GetComponent<Canvas>();
        so.FindProperty("mainMenuRoot").objectReferenceValue = mainMenu;
        so.FindProperty("gameOverRoot").objectReferenceValue = gameOver;
        so.FindProperty("howToPlayRoot").objectReferenceValue = howToPlay;
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("howToPlayButton").objectReferenceValue = howTo;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("gameOverScore").objectReferenceValue = score;
        so.FindProperty("gameOverHabitats").objectReferenceValue = habitats;
        so.FindProperty("gameOverBestChain").objectReferenceValue = chain;
        so.FindProperty("restartButton").objectReferenceValue = restart;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenuBtn;
        so.FindProperty("howToPlayOkButton").objectReferenceValue = ok;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignQuestHudView(QuestHudView view, GameObject canvas,
        TextMeshProUGUI mqTitle, TextMeshProUGUI mqGoal, TextMeshProUGUI mqProg, Slider mqBar, TextMeshProUGUI mqRew,
        TextMeshProUGUI sqTitle, TextMeshProUGUI sqGoal, TextMeshProUGUI sqProg, Slider sqBar, TextMeshProUGUI sqRew,
        GameObject banner, TextMeshProUGUI bannerText)
    {
        var so = new SerializedObject(view);
        so.FindProperty("questCanvas").objectReferenceValue = canvas.GetComponent<Canvas>();
        so.FindProperty("mainQuestTitle").objectReferenceValue = mqTitle;
        so.FindProperty("mainQuestGoal").objectReferenceValue = mqGoal;
        so.FindProperty("mainQuestProgress").objectReferenceValue = mqProg;
        so.FindProperty("mainQuestBar").objectReferenceValue = mqBar;
        so.FindProperty("mainQuestReward").objectReferenceValue = mqRew;
        so.FindProperty("sideQuestTitle").objectReferenceValue = sqTitle;
        so.FindProperty("sideQuestGoal").objectReferenceValue = sqGoal;
        so.FindProperty("sideQuestProgress").objectReferenceValue = sqProg;
        so.FindProperty("sideQuestBar").objectReferenceValue = sqBar;
        so.FindProperty("sideQuestReward").objectReferenceValue = sqRew;
        so.FindProperty("completeBanner").objectReferenceValue = banner;
        so.FindProperty("completeBannerText").objectReferenceValue = bannerText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Quest HUD primitives ───────────────────────────────────────────────

    private static RectTransform CreateQuestPanel(Transform canvasRoot)
    {
        var go = new GameObject("QuestPanel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(12f, 120f);
        rt.sizeDelta = new Vector2(270f, 320f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.06f, 0.06f, 0.08f, 0.35f);
        img.raycastTarget = false;
        return rt;
    }

    private static void CreateQuestHeader(RectTransform parent, string text, ref float yOffset)
    {
        var go = new GameObject("Header_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 20f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 10f;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.color = new Color(0.9f, 0.75f, 0.35f);
        tmp.characterSpacing = 3f;
        tmp.raycastTarget = false;
        yOffset -= 22f;
    }

    private static TextMeshProUGUI CreateQuestLabel(RectTransform parent, string goName, string text,
        float size, Color color, FontStyles style, ref float yOffset, float extraPad)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, size * 1.6f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        yOffset -= size * 1.6f + extraPad;
        return tmp;
    }

    private static Slider CreateQuestSlider(RectTransform parent, string goName, ref float yOffset, float extraPad)
    {
        const float height = 6f;
        var go = new GameObject(goName, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        StretchRect(bgGo.GetComponent<RectTransform>());
        bgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
        slider.targetGraphic = bgGo.GetComponent<Image>();

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.sizeDelta = Vector2.zero;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillArea.transform, false);
        StretchRect(fillGo.GetComponent<RectTransform>());
        fillGo.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.4f);
        slider.fillRect = fillGo.GetComponent<RectTransform>();

        yOffset -= height + extraPad;
        return slider;
    }

    private static void CreateQuestSeparator(RectTransform parent, ref float yOffset)
    {
        var go = new GameObject("Sep", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(12f, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        yOffset -= 13f;
    }

    private static GameObject CreateQuestBanner(Transform canvasRoot, out TextMeshProUGUI bannerText)
    {
        var bannerGo = new GameObject("QuestCompleteBanner", typeof(RectTransform), typeof(Image));
        bannerGo.transform.SetParent(canvasRoot, false);
        var rt = bannerGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(500f, 90f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        bannerGo.GetComponent<Image>().color = new Color(0.1f, 0.55f, 0.2f, 0.92f);

        var textGo = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bannerGo.transform, false);
        StretchRect(textGo.GetComponent<RectTransform>(), 8f);
        bannerText = textGo.GetComponent<TextMeshProUGUI>();
        bannerText.text = "Quest complete!";
        bannerText.fontSize = 18f;
        bannerText.fontStyle = FontStyles.Bold;
        bannerText.color = Color.white;
        bannerText.alignment = TextAlignmentOptions.Center;
        bannerText.raycastTarget = false;

        bannerGo.SetActive(false);
        return bannerGo;
    }

    private static void StretchRect(RectTransform rt, float margin = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    // ── Primitives ─────────────────────────────────────────────────────────

    private static void DestroyExistingChild(Transform parent, string childName)
    {
        var existing = parent.Find(childName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

    private static GameObject CreateCanvasRoot(Transform parent, string name, int sortOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    private static RectTransform CreateFullScreen(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform CreateCard(Transform parent, string name, Vector2 size,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(14);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelDark;
        img.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string text, float fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPosition,
        FontStyles style = FontStyles.Normal, float charSpacing = 0f)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = style;
        label.characterSpacing = charSpacing;
        label.raycastTarget = false;
        return label;
    }

    private static Image CreateImage(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;
        return go.GetComponent<Image>();
    }

    private static Button CreateButton(Transform parent, string name, string label, Color bg, Color textColor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPosition;
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = UISpriteFactory.MakeButtonColors(bg);
        CreateLabel(rt, label, name == "PauseBtn" ? 30f : 22f, textColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontStyles.Bold, 4f);
        return btn;
    }

    private static RectTransform CreateFullScreenPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform CreateBackdrop(RectTransform parent)
    {
        var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = UISpriteFactory.Backdrop;
        img.raycastTarget = true;
        return rt;
    }

    private static RectTransform CreateCenterCard(RectTransform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(16);
        img.type = Image.Type.Sliced;
        img.color = UISpriteFactory.PanelDark;
        img.raycastTarget = true;
        return rt;
    }

    private static void CreateMenuTitle(RectTransform card, string text, float fontSize, Vector2 pos)
    {
        var label = CreateTmp(card, "Title", text, fontSize, UISpriteFactory.TextPrimary, pos,
            new Vector2(card.sizeDelta.x - 48f, 56f), TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
    }

    private static void CreateMenuSubtitle(RectTransform card, string text, Vector2 pos)
    {
        CreateTmp(card, "Subtitle", text, 20f, UISpriteFactory.TextMuted, pos,
            new Vector2(card.sizeDelta.x - 56f, 72f), TextAlignmentOptions.Center);
    }

    private static void CreateBodyText(RectTransform card, string text, Vector2 pos, Vector2 size)
    {
        var label = CreateTmp(card, "Body", text, 20f, UISpriteFactory.TextPrimary, pos, size, TextAlignmentOptions.TopLeft);
        label.enableWordWrapping = true;
        label.lineSpacing = 4f;
    }

    private static TextMeshProUGUI CreateStatLine(RectTransform card, string caption, string value,
        Color valueColor, float valueSize, Vector2 pos)
    {
        CreateTmp(card, "Caption", caption, 16f, UISpriteFactory.TextMuted, pos + new Vector2(0f, 22f),
            new Vector2(card.sizeDelta.x - 48f, 24f), TextAlignmentOptions.Center);
        return CreateTmp(card, "Value", value, valueSize, valueColor, pos,
            new Vector2(card.sizeDelta.x - 48f, 48f), TextAlignmentOptions.Center);
    }

    private static Button CreateMenuButton(RectTransform card, string label, Color bg, Color textColor, Vector2 pos)
    {
        var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(card, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(card.sizeDelta.x - 80f, 56f);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(12);
        img.type = Image.Type.Sliced;
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = UISpriteFactory.MakeButtonColors(bg);
        var tmp = CreateTmp(rt, "Label", label, 22f, textColor, Vector2.zero,
            Vector2.zero, TextAlignmentOptions.Center);
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 3f;
        return btn;
    }

    private static Slider CreateSlider(RectTransform card, string label, Vector2 pos)
    {
        var row = new GameObject($"Row_{label}", typeof(RectTransform));
        row.transform.SetParent(card, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = new Vector2(0.5f, 1f);
        rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(card.sizeDelta.x - 64f, 56f);
        rrt.anchoredPosition = pos;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(rrt, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.35f, 1f);
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var lt = labelGo.AddComponent<TextMeshProUGUI>();
        lt.text = label;
        lt.fontSize = 22f;
        lt.color = UISpriteFactory.TextMuted;
        lt.alignment = TextAlignmentOptions.MidlineLeft;

        var slGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        slGo.transform.SetParent(rrt, false);
        var srt = (RectTransform)slGo.transform;
        srt.anchorMin = new Vector2(0.36f, 0.25f);
        srt.anchorMax = new Vector2(1f, 0.75f);
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(srt, false);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = UISpriteFactory.RoundedRect(8);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = UISpriteFactory.PanelLight;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(srt, false);
        var farRt = (RectTransform)fillArea.transform;
        Stretch(farRt);
        farRt.offsetMin = new Vector2(2f, 2f);
        farRt.offsetMax = new Vector2(-2f, -2f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(farRt, false);
        Stretch(fill.GetComponent<RectTransform>());
        var fillImg = fill.GetComponent<Image>();
        fillImg.sprite = UISpriteFactory.RoundedRect(8);
        fillImg.type = Image.Type.Sliced;
        fillImg.color = UISpriteFactory.AccentGreen;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(srt, false);
        var haRt = (RectTransform)handleArea.transform;
        Stretch(haRt);
        haRt.offsetMin = new Vector2(12f, 0f);
        haRt.offsetMax = new Vector2(-12f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(haRt, false);
        var hRt = (RectTransform)handle.transform;
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.sizeDelta = new Vector2(28f, 28f);
        var hImg = handle.GetComponent<Image>();
        hImg.sprite = UISpriteFactory.Circle(20);
        hImg.color = UISpriteFactory.TextPrimary;

        var slider = slGo.GetComponent<Slider>();
        slider.targetGraphic = hImg;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = hRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateTmp(RectTransform parent, string name, string text,
        float fontSize, Color color, Vector2 anchoredPos, Vector2 sizeDelta,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta == Vector2.zero ? new Vector2(200f, 40f) : sizeDelta;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }
}
