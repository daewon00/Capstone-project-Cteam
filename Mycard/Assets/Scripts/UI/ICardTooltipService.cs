public interface ICardTooltipService
{
    void Show(ICardTooltipSource source);
    void Hide(ICardTooltipSource source);
    void HideAll();
}
