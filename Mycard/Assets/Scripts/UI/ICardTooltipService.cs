public interface ICardTooltipService
{
    void Show(Card owner, CardTooltipData data);
    void Hide(Card owner);
    void HideAll();
}
