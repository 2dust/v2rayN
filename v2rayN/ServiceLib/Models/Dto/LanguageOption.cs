namespace ServiceLib.Models.Dto;

public sealed record LanguageOption(string Value, string Display)
{
    public override string ToString() => Display ?? string.Empty;
}
