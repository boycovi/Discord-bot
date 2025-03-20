using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

class DiscordBot
{
    private readonly DiscordSocketClient _client;
    private readonly string _token;
    private Dictionary<string, int> _invitesDictionary = new();
    private SocketGuild? _selectedGuild;
    private SocketTextChannel? _selectedChannel;

    public DiscordBot(IConfiguration configuration)
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents =
                GatewayIntents.Guilds |
                GatewayIntents.GuildMessages |
                GatewayIntents.GuildMembers |
                GatewayIntents.MessageContent
        });

        _client.Log += Log;
        _client.Ready += OnReady;
        _client.SlashCommandExecuted += SlashCommandHandler;

        _token = configuration["BotToken"] ?? string.Empty;
        if (string.IsNullOrEmpty(_token)) throw new Exception("Bot token is missing or empty.");
    }
    public async Task RunBotAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        _ = Task.Run(HandleConsoleInput);
    }
    private async Task OnReady()
    {
        Console.WriteLine("Bot is online Type 'list' to see available channels.");

        var guild = _client.Guilds.FirstOrDefault();
        if (guild is not null)
        {
            var invites = await guild.GetInvitesAsync();
            _invitesDictionary = invites.ToDictionary(invite => invite.Code, invite => invite.Uses ?? 0);
        }

        var inviteCountCommand = new SlashCommandBuilder()
            .WithName("invite-count")
            .WithDescription("Shows all server invites and their usage");

        var inviteCountSelectedCommand = new SlashCommandBuilder()
            .WithName("invite-count-selected")
            .WithDescription("Shows invites for a specific user")
            .AddOption("user", ApplicationCommandOptionType.User, "Select a user", isRequired: true);

        try
        {
            await guild?.CreateApplicationCommandAsync(inviteCountCommand.Build());
            await guild?.CreateApplicationCommandAsync(inviteCountSelectedCommand.Build());
        }
        catch (ApplicationCommandException exception)
        {
            var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch (command.CommandName)
        {
            case "invite-count":
                await HandleInviteCount(command);
                break;
            case "invite-count-selected":
                await HandleInviteCountSelected(command);
                break;
        }
    }
    private async Task HandleInviteCount(SocketSlashCommand command)
    {
        var guild = _client.GetGuild(command.GuildId ?? 0);
        if (guild is null)
        {
            await command.RespondAsync("This command can only be used in a server.", ephemeral: true);
            return;
        }

        var invites = await guild.GetInvitesAsync();
        if (invites.Count == 0)
        {
            await command.RespondAsync("No invites found.", ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Server Invite List:");

        foreach (var invite in invites)
        {
            sb.AppendLine($"🔹 `{invite.Code}` - Created by **{invite.Inviter?.Username ?? "Unknown"}** - Used `{invite.Uses ?? 0}` times");
        }

        await command.RespondAsync(sb.ToString());
    }
    private async Task HandleInviteCountSelected(SocketSlashCommand command)
    {
        var userOption = command.Data.Options.FirstOrDefault(opt => opt.Name == "user")?.Value as SocketUser;
        if (userOption is null)
        {
            await command.RespondAsync("Invalid user.", ephemeral: true);
            return;
        }

        var guild = _client.GetGuild(command.GuildId ?? 0);
        if (guild is null)
        {
            await command.RespondAsync("This command can only be used in a server.", ephemeral: true);
            return;
        }

        var invites = await guild.GetInvitesAsync();
        var userInvites = invites.Where(inv => inv.Inviter?.Id == userOption.Id).ToList();

        if (userInvites.Count == 0)
        {
            await command.RespondAsync($"User {userOption.Username} has no invites.", ephemeral: true);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Invites by {userOption.Username}:");

        foreach (var invite in userInvites)
        {
            sb.AppendLine($"`{invite.Code}` - Used `{invite.Uses ?? 0}` times");
        }

        await command.RespondAsync(sb.ToString());
    }
    private async Task HandleConsoleInput()
    {
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (_selectedGuild is null)
            {
                if (int.TryParse(input, out int guildIndex))
                {
                    var guilds = _client.Guilds.ToList();
                    if (guildIndex >= 1 && guildIndex <= guilds.Count)
                    {
                        _selectedGuild = guilds[guildIndex - 1];
                        ShowChannelSelection();
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Enter a valid server number.");
                    }
                }
                continue;
            }

            if (_selectedChannel is null)
            {
                if (input == "/0")
                {
                    _selectedGuild = null;
                    ShowServerSelection();
                    continue;
                }

                if (int.TryParse(input, out int channelIndex))
                {
                    var channels = _selectedGuild.TextChannels.ToList();
                    if (channelIndex >= 1 && channelIndex <= channels.Count)
                    {
                        _selectedChannel = channels[channelIndex - 1];
                        Console.WriteLine($"Selected text channel: #{_selectedChannel.Name}");
                        Console.WriteLine("Type your message or enter `/0` to go back to server selection.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Enter a valid channel number.");
                    }
                }
                continue;
            }

            if (input == "/0")
            {
                _selectedChannel = null;
                ShowChannelSelection();
                continue;
            }

            await _selectedChannel.SendMessageAsync(input);
            Console.WriteLine($"Sent message to #{_selectedChannel.Name}");
        }
    }
    private void ShowServerSelection()
    {
        Console.WriteLine("\nAvailable Servers:");
        var guilds = _client.Guilds.ToList();
        for (var i = 0; i < guilds.Count; i++) Console.WriteLine($"  {i + 1} - {guilds[i].Name}");
        Console.WriteLine("\nEnter a number to select a server.");
    }
    private void ShowChannelSelection()
    {
        Console.WriteLine($"\nAvailable Text Channels in {_selectedGuild.Name}:");
        var channels = _selectedGuild.TextChannels.ToList();
        for (var i = 0; i < channels.Count; i++)  Console.WriteLine($"  {i + 1} - #{channels[i].Name}");
        Console.WriteLine("\nEnter a number to select a text channel or enter `/0` to go back.");
    }
    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg);
        return Task.CompletedTask;
    }
    public Dictionary<string, int> GetInviteStats() => _invitesDictionary;
}