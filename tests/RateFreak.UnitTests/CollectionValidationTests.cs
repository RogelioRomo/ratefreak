using System.Text.Json;
using RateFreak.Application.Collections;

namespace RateFreak.UnitTests;

public sealed class CollectionValidationTests
{
    private static JsonElement J(string value) => JsonDocument.Parse(value).RootElement.Clone();
    private static FieldDefinition F(string type, string config = "{}", bool required = false) => new("value", "Value", type, required, J(config), 0);

    [Theory]
    [InlineData("text", "{}", "\"hello\"", true)]
    [InlineData("text", "{\"maxLength\":3}", "\"four\"", false)]
    [InlineData("text", "{}", "\"line\\nnext\"", true)]
    [InlineData("text", "{}", "\"bad\\u0000\"", false)]
    [InlineData("text", "{}", "\"\\uD800\"", false)]
    [InlineData("text", "{}", "3", false)]
    [InlineData("number", "{\"min\":1,\"max\":3}", "1", true)]
    [InlineData("number", "{\"min\":1,\"max\":3}", "3.1", false)]
    [InlineData("number", "{}", "1e100", false)]
    [InlineData("number", "{}", "\"2\"", false)]
    [InlineData("boolean", "{}", "false", true)]
    [InlineData("boolean", "{}", "0", false)]
    [InlineData("date", "{}", "\"2024-02-29\"", true)]
    [InlineData("date", "{}", "\"2026-02-29\"", false)]
    [InlineData("date", "{}", "\"2026-1-01\"", false)]
    [InlineData("select", "{\"options\":[\"Yes\",\"No\"]}", "\"Yes\"", true)]
    [InlineData("select", "{\"options\":[\"Yes\",\"No\"]}", "\"yes\"", false)]
    public void Values_are_typed_and_bounded(string type, string config, string value, bool valid)
    {
        var errors = CollectionValidation.ValidateData(J("{\"value\":" + value + "}"), [F(type, config)]);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData("{}", false, true)]
    [InlineData("{}", true, false)]
    [InlineData("{\"value\":null}", false, true)]
    [InlineData("{\"value\":null}", true, false)]
    [InlineData("{\"value\":\" \"}", true, false)]
    [InlineData("{\"other\":1}", false, false)]
    [InlineData("{\"value\":\"a\",\"value\":\"b\"}", false, false)]
    [InlineData("[]", false, false)]
    public void Required_and_unknown_keys_are_checked(string data, bool required, bool valid) =>
        Assert.Equal(valid, CollectionValidation.ValidateData(J(data), [F("text", required: required)]).Count == 0);

    [Theory]
    [InlineData("text", "{\"maxLength\":0}")]
    [InlineData("text", "{\"maxLength\":4001}")]
    [InlineData("text", "{\"maxLength\":2,\"maxLength\":3}")]
    [InlineData("number", "{\"min\":4,\"max\":3}")]
    [InlineData("number", "{\"min\":1e100}")]
    [InlineData("boolean", "{\"maxLength\":3}")]
    [InlineData("date", "{\"unknown\":1}")]
    [InlineData("select", "{}")]
    [InlineData("select", "{\"options\":[]}")]
    [InlineData("select", "{\"options\":[\"a\",\"a\"]}")]
    [InlineData("select", "{\"options\":[\" \" ]}")]
    [InlineData("select", "{\"options\":[null]}")]
    [InlineData("unsupported", "{}")]
    [InlineData("text", "[]")]
    public void Invalid_configuration_is_rejected(string type, string config) =>
        Assert.NotEmpty(CollectionValidation.ValidateFields([F(type, config)]));

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("media_item_id")]
    [InlineData("created_at")]
    [InlineData("updated_at")]
    [InlineData("UPPER")]
    [InlineData("1key")]
    [InlineData("bad-key")]
    [InlineData("")]
    public void Invalid_keys_are_rejected(string key) =>
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("text") with { Key = key }]));

    [Fact]
    public void Custom_json_depth_and_malformed_property_names_are_rejected_safely()
    {
        var atLimit = new string('[', 16) + "0" + new string(']', 16);
        var overLimit = new string('[', 17) + "0" + new string(']', 17);
        Assert.True(CollectionValidation.ValidJson(J(atLimit)));
        Assert.False(CollectionValidation.ValidJson(J(overLimit)));
        Assert.NotEmpty(CollectionValidation.ValidateData(J("{\"\\uD800\":\"x\"}"), [F("text")]));
    }

    [Fact]
    public void Field_and_value_boundaries_preserve_valid_inputs()
    {
        var fields = Enumerable.Range(0, 30).Select(i => F("text") with { Key = "key" + i }).ToArray();
        Assert.Empty(CollectionValidation.ValidateFields(fields));
        Assert.NotEmpty(CollectionValidation.ValidateFields([.. fields, F("text")]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("text"), F("text")]));
        Assert.Empty(CollectionValidation.ValidateFields([F("text") with { Key = new string('a', 40), Label = new string('b', 80) }]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("text") with { Key = new string('a', 41) }]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("text") with { Label = new string('a', 81) }]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("text") with { Position = -1 }]));
        Assert.Empty(CollectionValidation.ValidateData(J(JsonSerializer.Serialize(new { value = new string('a', 1000) })), [F("text")]));
        Assert.NotEmpty(CollectionValidation.ValidateData(J(JsonSerializer.Serialize(new { value = new string('a', 1001) })), [F("text")]));
        Assert.Empty(CollectionValidation.ValidateData(J(JsonSerializer.Serialize(new { value = new string('a', 4000) })), [F("text", "{\"maxLength\":4000}")]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("select", JsonSerializer.Serialize(new { options = Enumerable.Range(0, 51).Select(i => i.ToString()) }))]));
        Assert.NotEmpty(CollectionValidation.ValidateFields([F("select", JsonSerializer.Serialize(new { options = new[] { new string('a', 81) } }))]));
    }
}
