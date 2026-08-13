using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class PauseMenuView : ColorRect
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void RestartRequestedEventHandler();
    [Signal] public delegate void QuitRequestedEventHandler();
    [Signal] public delegate void SensitivityChangedEventHandler(float value);
    [Signal] public delegate void QualityChangedEventHandler(int index);
    [Signal] public delegate void FullscreenChangedEventHandler(bool active);
    [Signal] public delegate void LanguageChangedEventHandler(string language);

    private Label _pauseTitle = null!;
    private Label _pauseOperation = null!;
    private Label _sensitivityCaption = null!;
    private Label _sensitivityValue = null!;
    private HSlider _sensitivitySlider = null!;
    private Label _qualityCaption = null!;
    private OptionButton _qualitySelect = null!;
    private Label _languageCaption = null!;
    private OptionButton _languageSelect = null!;
    private CheckButton _fullscreenToggle = null!;
    private Button _resumeButton = null!;
    private Button _restartButton = null!;
    private Button _quitButton = null!;
    private Label _buildLabel = null!;
    private string _language = "en";

    public bool UiReady
        => IsInstanceValid(_pauseTitle)
        && IsInstanceValid(_sensitivitySlider)
        && IsInstanceValid(_qualitySelect)
        && IsInstanceValid(_languageSelect)
        && IsInstanceValid(_fullscreenToggle)
        && IsInstanceValid(_resumeButton)
        && IsInstanceValid(_restartButton)
        && IsInstanceValid(_quitButton)
        && IsInstanceValid(_buildLabel);

    public bool IntentSignalsConnected
        => HasConnections(SignalName.ResumeRequested)
        && HasConnections(SignalName.RestartRequested)
        && HasConnections(SignalName.QuitRequested)
        && HasConnections(SignalName.SensitivityChanged)
        && HasConnections(SignalName.QualityChanged)
        && HasConnections(SignalName.FullscreenChanged)
        && HasConnections(SignalName.LanguageChanged)
        && _sensitivitySlider.HasConnections(Range.SignalName.ValueChanged)
        && _qualitySelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _languageSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _fullscreenToggle.HasConnections(BaseButton.SignalName.Toggled)
        && _resumeButton.HasConnections(BaseButton.SignalName.Pressed)
        && _restartButton.HasConnections(BaseButton.SignalName.Pressed)
        && _quitButton.HasConnections(BaseButton.SignalName.Pressed);

    public override void _Ready()
    {
        BindNodes();
        PopulateOptions();
        ConnectIntentSignals();
        SetLanguage(_language);
    }

    public void SetSettings(float sensitivity, int quality, bool fullscreen, string language)
    {
        _sensitivitySlider.SetValueNoSignal(sensitivity);
        _sensitivityValue.Text = $"{sensitivity:0.00}";
        _qualitySelect.Select(Mathf.Clamp(quality, 0, 2));
        _fullscreenToggle.SetPressedNoSignal(fullscreen);
        SetLanguage(language);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _languageSelect.Select(_language == "zh" ? 1 : 0);
        _pauseTitle.Text = Text("pause_title", "TACTICAL PAUSE");
        _pauseOperation.Text = Text("operation", "OPERATION STEEL TIDE");
        _sensitivityCaption.Text = Text("look_sensitivity", "LOOK SENSITIVITY");
        _qualityCaption.Text = Text("render_quality", "RENDER QUALITY");
        _languageCaption.Text = Text("language", "LANGUAGE");
        _fullscreenToggle.Text = Text("fullscreen", "FULLSCREEN");
        _resumeButton.Text = Text("resume", "RESUME OPERATION");
        _restartButton.Text = Text("redeploy", "REDEPLOY");
        _quitButton.Text = Text("exit", "EXIT TO DESKTOP");
        _qualitySelect.SetItemText(0, Text("performance", "Performance"));
        _qualitySelect.SetItemText(1, Text("balanced", "Balanced"));
        _qualitySelect.SetItemText(2, Text("cinematic", "Cinematic"));
    }

    public bool SettingsMatch(float sensitivity, int quality, bool fullscreen, string language)
    {
        return Mathf.IsEqualApprox((float)_sensitivitySlider.Value, sensitivity)
            && _sensitivityValue.Text == $"{sensitivity:0.00}"
            && _qualitySelect.Selected == Mathf.Clamp(quality, 0, 2)
            && _fullscreenToggle.ButtonPressed == fullscreen
            && _languageSelect.Selected == (GameLocalization.IsChinese(language) ? 1 : 0);
    }

    public bool LanguageMatches(string language)
    {
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        return _language == normalized
            && _pauseTitle.Text == GameLocalization.Get("pause_title", normalized, "TACTICAL PAUSE")
            && _resumeButton.Text == GameLocalization.Get("resume", normalized, "RESUME OPERATION")
            && _qualitySelect.GetItemText(0) == GameLocalization.Get("performance", normalized, "Performance")
            && _languageSelect.Selected == (normalized == "zh" ? 1 : 0);
    }

    public void PressResumeForDiagnostics()
    {
        if (IsInstanceValid(_resumeButton))
        {
            _resumeButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    private void BindNodes()
    {
        var content = GetNode<Control>("Content");
        _pauseTitle = content.GetNode<Label>("PauseTitle");
        _pauseOperation = content.GetNode<Label>("PauseOperation");
        _sensitivityCaption = content.GetNode<Label>("SensitivityCaption");
        _sensitivityValue = content.GetNode<Label>("SensitivityValue");
        _sensitivitySlider = content.GetNode<HSlider>("SensitivitySlider");
        _qualityCaption = content.GetNode<Label>("QualityCaption");
        _qualitySelect = content.GetNode<OptionButton>("QualitySelect");
        _languageCaption = content.GetNode<Label>("LanguageCaption");
        _languageSelect = content.GetNode<OptionButton>("LanguageSelect");
        _fullscreenToggle = content.GetNode<CheckButton>("FullscreenToggle");
        _resumeButton = content.GetNode<Button>("ResumeButton");
        _restartButton = content.GetNode<Button>("RestartButton");
        _quitButton = content.GetNode<Button>("QuitButton");
        _buildLabel = content.GetNode<Label>("BuildLabel");
    }

    private void PopulateOptions()
    {
        _qualitySelect.AddItem("Performance");
        _qualitySelect.AddItem("Balanced");
        _qualitySelect.AddItem("Cinematic");
        _qualitySelect.Selected = 2;
        _languageSelect.AddItem("English");
        _languageSelect.AddItem("\u4e2d\u6587");
    }

    private void ConnectIntentSignals()
    {
        _sensitivitySlider.ValueChanged += value =>
        {
            _sensitivityValue.Text = $"{value:0.00}";
            EmitSignal(SignalName.SensitivityChanged, (float)value);
        };
        _qualitySelect.ItemSelected += index => EmitSignal(SignalName.QualityChanged, (int)index);
        _languageSelect.ItemSelected += index =>
        {
            var language = index == 1 ? "zh" : "en";
            SetLanguage(language);
            EmitSignal(SignalName.LanguageChanged, language);
        };
        _fullscreenToggle.Toggled += active => EmitSignal(SignalName.FullscreenChanged, active);
        _resumeButton.Pressed += () => EmitSignal(SignalName.ResumeRequested);
        _restartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
        _quitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);
}
