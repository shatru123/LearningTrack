using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Day05.MiddlewareApi.Tests;

public class MiddlewarePipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MiddlewarePipelineTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_AndAttachesGeneratedCorrelationId()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task CorrelationId_PreservesIncomingHeader()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/events");
        const string customId = "client-req-999888";
        request.Headers.Add("X-Correlation-ID", customId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var responseId = response.Headers.GetValues("X-Correlation-ID").First();
        Assert.Equal(customId, responseId);
    }

    [Fact]
    public async Task EventsEndpoints_ReturnExpectedData()
    {
        var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/events");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var singleResponse = await client.GetAsync("/events/1001");
        Assert.Equal(HttpStatusCode.OK, singleResponse.StatusCode);

        var missingResponse = await client.GetAsync("/events/9999");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task FailureEndpoint_ReturnsProblemDetails_WithStatus500AndTraceId()
    {
        var client = _factory.CreateClient();
        const string customId = "failure-test-trace-id";
        var request = new HttpRequestMessage(HttpMethod.Get, "/failure");
        request.Headers.Add("X-Correlation-ID", customId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Contains("Simulated catastrophic failure", root.GetProperty("detail").GetString());
        Assert.Equal(customId, root.GetProperty("traceId").GetString());
    }
}
