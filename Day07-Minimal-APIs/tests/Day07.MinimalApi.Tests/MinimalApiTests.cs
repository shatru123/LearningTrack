using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Day07.MinimalApi.Tests;

public class MinimalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MinimalApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllEvents_Returns200_AndIncludesEndpointTimingHeader()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Endpoint-Time-Ms"));

        var events = await response.Content.ReadFromJsonAsync<List<EventResponse>>();
        Assert.NotNull(events);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/events/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Post_CreatesNewEvent_WhenValid()
    {
        var client = _factory.CreateClient();
        var req = new CreateEventRequest("Tottenham vs Arsenal", "Tottenham Stadium", 1750m, "Available");

        var response = await client.PostAsJsonAsync("/api/events", req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);
        Assert.Equal("Tottenham vs Arsenal", created.Name);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenFilterDetectsInvalidPayload()
    {
        var client = _factory.CreateClient();
        // Negative price violates validation filter!
        var req = new CreateEventRequest("Invalid Match", "Stadium", -50m, "Available");

        var response = await client.PostAsJsonAsync("/api/events", req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("Price must be greater than zero", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task FullCrudLifecycle_ExecutesSuccessfully()
    {
        var client = _factory.CreateClient();

        // 1. Create
        var createReq = new CreateEventRequest("CRUD Test", "Venue X", 100m, "Available");
        var createRes = await client.PostAsJsonAsync("/api/events", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);

        // 2. Read
        var getRes = await client.GetAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        // 3. Update
        var updateReq = new UpdateEventRequest("CRUD Updated", "Venue Y", 120m, "SoldOut");
        var updateRes = await client.PutAsJsonAsync($"/api/events/{created.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(updated);
        Assert.Equal("CRUD Updated", updated.Name);
        Assert.Equal("SoldOut", updated.Status);

        // 4. Delete
        var delRes = await client.DeleteAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 5. Verify 404
        var verifyRes = await client.GetAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, verifyRes.StatusCode);
    }
}
