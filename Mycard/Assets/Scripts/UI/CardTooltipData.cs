public readonly struct CardTooltipData
{
    public string Title { get; }
    public string Description { get; }

    public CardTooltipData(string title, string description)
    {
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
    }
}
