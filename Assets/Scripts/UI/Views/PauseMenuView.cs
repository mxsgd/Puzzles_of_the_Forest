using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Referencje do menu pauzy — edytuj w Hierarchy/Inspectorze.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuView : MonoBehaviour
{
    [SerializeField] private CanvasGroup backdropGroup;
    [SerializeField] private RectTransform backdrop;
    [SerializeField] private RectTransform menuCard;
    [SerializeField] private RectTransform settingsCard;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    public CanvasGroup BackdropGroup => backdropGroup;
    public RectTransform Backdrop => backdrop;
    public RectTransform MenuCard => menuCard;
    public RectTransform SettingsCard => settingsCard;
    public Button ResumeButton => resumeButton;
    public Button SettingsButton => settingsButton;
    public Button QuitButton => quitButton;
    public Button SettingsBackButton => settingsBackButton;
    public Slider MusicSlider => musicSlider;
    public Slider SfxSlider => sfxSlider;

    public bool IsConfigured =>
        backdropGroup != null
        && backdrop != null
        && menuCard != null
        && settingsCard != null
        && resumeButton != null
        && settingsButton != null
        && quitButton != null
        && settingsBackButton != null
        && musicSlider != null
        && sfxSlider != null;
}
