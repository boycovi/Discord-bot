var builder = WebApplication.CreateBuilder(args);

// Load configuration file
builder.Configuration.AddJsonFile("DiscCount.json", optional: false, reloadOnChange: true);

var bot = new DiscordBot(builder.Configuration);
await bot.RunBotAsync();

builder.Services.AddSingleton(bot);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/invites", (DiscordBot botService) => Task.FromResult(Results.Json(botService.GetInviteStats())));

app.Run();