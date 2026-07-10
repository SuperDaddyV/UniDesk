namespace UniDesk.Services;

public interface IWindowService
{
    const double MinPanelWidth = 320;
    const double MaxPanelWidth = 520;
    const double MinPanelHeight = 560;
    const double MaxPanelHeight = 1040;
    const double CollapsedPanelWidth = 40;

    void ActivateWindow();
    void SetTopMost(bool topMost);
    void ShowWindow();
    void HideWindow();
    void ToggleWindow();
    void SetWidth(double width);
    void SetHeight(double height);
    void AnimateWidth(double width, Action? onCompleted = null);
    void SetOpacity(double opacity);
    double GetCurrentWidth();
}
