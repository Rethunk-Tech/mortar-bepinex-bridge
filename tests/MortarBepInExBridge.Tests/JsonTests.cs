using Xunit;

namespace MortarBepInExBridge.Tests;

public class JsonTests
{
    [Theory]
    [InlineData(null, "\"\"")]
    [InlineData("", "\"\"")]
    [InlineData("ok", "\"ok\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("a\nb", "\"a\\u000ab\"")]
    [InlineData("a\tb", "\"a\\u0009b\"")]
    public void QuoteEscapesControlsQuotesAndBackslashes(string? value, string json) =>
        Assert.Equal(json, Json.Quote(value));
}
