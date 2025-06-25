using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models; // for Swagger

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<LoggerMiddleware>();

var app = builder.Build();

// Middleware for logging
app.UseMiddleware<LoggerMiddleware>();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI();

// CRUD endpoints
app.MapGet("/users", (UserStore store) => store.GetAll());

app.MapGet("/users/{id}", (int id, UserStore store) =>
{
    var user = store.Get(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/users", (User user, UserStore store) =>
{
    if (string.IsNullOrEmpty(user.Name))
        return Results.BadRequest("Name is required.");
    
    var created = store.Add(user);
    return Results.Created($"/users/{created.Id}", created);
});

app.MapPut("/users/{id}", (int id, User updatedUser, UserStore store) =>
{
    if (string.IsNullOrEmpty(updatedUser.Name))
        return Results.BadRequest("Name is required.");

    var result = store.Update(id, updatedUser);
    return result ? Results.Ok(updatedUser) : Results.NotFound();
});

app.MapDelete("/users/{id}", (int id, UserStore store) =>
{
    var result = store.Delete(id);
    return result ? Results.Ok($"User {id} deleted") : Results.NotFound();
});

app.Run();

// User model
record User(int Id, string Name);

// In-memory store
class UserStore
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _idCounter = 1;

    public IEnumerable<User> GetAll() => _users.Values;

    public User? Get(int id) => _users.TryGetValue(id, out var user) ? user : null;

    public User Add(User user)
    {
        var newUser = user with { Id = _idCounter++ };
        _users[newUser.Id] = newUser;
        return newUser;
    }

    public bool Update(int id, User updatedUser)
    {
        if (!_users.ContainsKey(id)) return false;
        _users[id] = updatedUser with { Id = id };
        return true;
    }

    public bool Delete(int id) => _users.TryRemove(id, out _);
}

// Middleware for logging
class LoggerMiddleware : IMiddleware
{
    private readonly ILogger<LoggerMiddleware> _logger;

    public LoggerMiddleware(ILogger<LoggerMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        _logger.LogInformation("Request: {method} {path}", context.Request.Method, context.Request.Path);
        await next(context);
        _logger.LogInformation("Response Status: {statusCode}", context.Response.StatusCode);
    }
}
