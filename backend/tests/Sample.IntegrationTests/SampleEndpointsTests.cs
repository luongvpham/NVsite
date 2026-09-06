using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Sample.Application.Dtos;
using Sample.Application.Samples;
using Sample.Domain;
using Shared.Pagination;
using Xunit;

namespace Sample.IntegrationTests;

public sealed class SampleEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListSamples_ReturnsPagedResultShape()
    {
        var response = await _client.GetAsync("/samples");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<SampleSummary>>(JsonOptions);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task GetSampleById_UnknownId_Returns404WithErrorCode()
    {
        var response = await _client.GetAsync($"/samples/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("sample_not_found", problem.GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task CreateSample_Valid_Returns201AndThenReadableById()
    {
        var command = new CreateSampleCommand(Guid.NewGuid(), "Test sample", SampleStatus.Draft);

        var createResponse = await _client.PostAsJsonAsync("/samples", command);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<SampleDetail>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Test sample", created!.Name);
        Assert.Equal(SampleStatus.Draft, created.Status);

        var getResponse = await _client.GetAsync($"/samples/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateSample_MissingName_Returns400WithValidationErrorCode()
    {
        var payload = new { shopId = Guid.NewGuid(), name = "", status = "Draft" };

        var response = await _client.PostAsJsonAsync("/samples", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("validation_error", problem.GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task GetSampleByIdForShop_NestedRoute_ScopesToShop()
    {
        var shopId = Guid.NewGuid();
        var otherShopId = Guid.NewGuid();
        var command = new CreateSampleCommand(shopId, "Scoped sample", SampleStatus.Active);
        var createResponse = await _client.PostAsJsonAsync("/samples", command);
        var created = await createResponse.Content.ReadFromJsonAsync<SampleDetail>(JsonOptions);

        var okResponse = await _client.GetAsync($"/shops/{shopId}/samples/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        var wrongShopResponse = await _client.GetAsync($"/shops/{otherShopId}/samples/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, wrongShopResponse.StatusCode);
    }
}
