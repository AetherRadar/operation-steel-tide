namespace OperationSteelTide;

public partial class CombatHUD
{
    private int _alertPresentationState = int.MinValue;
    private int _alertPresentationValue = int.MinValue;
    private string _alertPresentationLanguage = string.Empty;

    private bool BeginAlertPresentationUpdate(int state, int displayedValue)
    {
        if (_alertPresentationState == state
            && _alertPresentationValue == displayedValue
            && _alertPresentationLanguage == _language)
        {
            return false;
        }

        _alertPresentationState = state;
        _alertPresentationValue = displayedValue;
        _alertPresentationLanguage = _language;
        return true;
    }
}
