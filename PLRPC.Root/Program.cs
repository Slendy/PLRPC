﻿using DiscordRPC;
using DiscordRPC.Logging;
using System.Text.Json;

namespace LBPUnion.PLRPC.Main;

public static class Program
{
    public static readonly DiscordRpcClient DiscordClient = new("1060973475151495288");
    public static readonly Dictionary<string, Entities.User?> UserCache = new();
    public static readonly Dictionary<int, Entities.Slot?> SlotCache = new();
    public static HttpClient APIHttpClient = null!;
    public static string username = null!;
    public static string serverUrl = null!;

    public static async Task Main(string[] args)
    {
        #if !DEBUG
        Updater.Release? updateResult = await Updater.Updater.CheckUpdate();
        if (updateResult != null)
        {
            Logging.Message.New(1, $"***************************************");
            Logging.Message.New(1, $"A new version of PLRPC is available!");
            Logging.Message.New(1, $"{updateResult.TagName}: {updateResult.Url}");
            Logging.Message.New(1, $"***************************************");
        }
        else
        {
            Logging.Message.New(1, $"There are no new updates available.");
        }
        #endif

        if (args.Length > 0)
        {
            if (args[0] == "--config")
            {
                if (!File.Exists("./config.json"))
                {
                    Logging.Message.New(2, "No configuration file exists, creating a base configuration.");
                    Logging.Message.New(2, "Please populate the configuration file and restart the program.");
                    Entities.Configuration? BaseConfiguration = new()
                    {
                        ServerUrl = "https://lighthouse.lbpunion.com",
                        Username = ""
                    };
                    await File.WriteAllTextAsync("./config.json", JsonSerializer.Serialize(BaseConfiguration, new JsonSerializerOptions { WriteIndented = true }));
                    return;
                }

                string ConfigurationJson = await File.ReadAllTextAsync("./config.json");
                Entities.Configuration? Configuration = JsonSerializer.Deserialize<Entities.Configuration>(ConfigurationJson);

                serverUrl = Configuration?.ServerUrl ?? "";
                username = Configuration?.Username ?? "";
            }
            else
            {
                Logging.Message.New(3, "You have passed an invalid flag. You may use one of the following:");
                Logging.Message.New(3, "  --config (to use a configuration file)");
                return;
            }
        }
        else
        {
            Console.Write("What is the URI of the Lighthouse Instance? (e.g. https://lighthouse.lbpunion.com) ");
            serverUrl = Console.ReadLine() ?? "";

            Console.Write("What is your registered username on this server? (e.g. littlebigmolly) ");
            username = Console.ReadLine() ?? "";
        }

        APIHttpClient = new HttpClient { BaseAddress = new Uri(serverUrl + "/api/v1/"), };

        DiscordClient.Initialize();
        DiscordClient.Logger = new ConsoleLogger { Level = LogLevel.Warning };

        DiscordClient.OnReady += (_, e) => Logging.Message.New(0, $"Connected to Discord Account {e.User.Username}#{e.User.Discriminator}.");

        DiscordClient.OnPresenceUpdate += (_, e) => Logging.Message.New(0, $"{e.Presence}: Presence updated.");

        while (true)
        {
            try
            {
                UpdatePresence(username);
            }
            catch (Exception exception)
            {
                DiscordClient.Dispose();
                Logging.Message.Exception(exception.Message);
                Thread.Sleep(10000);
                Environment.Exit(1);
            }
            await Task.Delay(30000);
        }
    }

    public static class Tasks
    {
        public static async Task<Entities.User?> GetUser(string username)
        {
            if (UserCache.TryGetValue(username, out Entities.User? userObject) && userObject != null)
            {
                return userObject;
            }

            Logging.Message.New(0, $"Fetching user {username} from the server...");

            string userJson = await APIHttpClient.GetStringAsync("username/" + username);

            userObject = JsonSerializer.Deserialize<Entities.User>(userJson);

            if (userObject == null)
            {
                return null;
            }

            UserCache.Add(username, userObject);
            return userObject;
        }

        public static async Task<Entities.UserStatus?> GetStatus(Entities.User? user)
        {
            Logging.Message.New(0, $"Fetching status information for {username} from the server...");

            string userStatusJson = await APIHttpClient.GetStringAsync("user/" + user?.UserId + "/status");

            Entities.UserStatus? userStatusObject = JsonSerializer.Deserialize<Entities.UserStatus>(userStatusJson);

            return userStatusObject ?? null;
        }

        public static async Task<Entities.Slot?> GetSlot(Entities.User? user, Entities.UserStatus? userStatus)
        {
            if (SlotCache.TryGetValue(Extensions.StateTypesExtensions.StaticId(
                userStatus?.CurrentRoom?.RoomSlot?.SlotType,
                userStatus?.CurrentRoom?.RoomSlot),
                out Entities.Slot? slotObject) && slotObject != null)
            {
                Logging.Message.New(0, $"Using cached slot information for slot ID {slotObject.SlotId}");
                return slotObject;
            }

            // Handle non-existent slots, this could be done better
            if (userStatus?.CurrentRoom?.RoomSlot?.SlotType != Types.SlotType.User)
            {
                slotObject = new Entities.Slot
                {
                    SlotName = Extensions.StateTypesExtensions.SlotName(userStatus?.CurrentRoom?.RoomSlot?.SlotType, slotObject),
                    SlotId = Extensions.StateTypesExtensions.StaticId(userStatus?.CurrentRoom?.RoomSlot?.SlotType, userStatus?.CurrentRoom?.RoomSlot),
                    IconHash = user?.MehHash,
                };

                Logging.Message.New(1, $"{userStatus?.CurrentRoom?.RoomSlot?.SlotId ?? -1} is not a real slot, diverting to static.");
                Logging.Message.New(1, "This is likely because you are offline or playing a non-user level.");

                Logging.Message.New(0, $"Caching a new static slot under ID {slotObject.SlotId}");

                SlotCache.Add(slotObject.SlotId, slotObject);
                return slotObject;
            }

            Logging.Message.New(0, $"Fetching slot information for {userStatus?.CurrentRoom?.RoomSlot?.SlotId} from the server...");

            string slotJson = await APIHttpClient.GetStringAsync("slot/" + userStatus?.CurrentRoom?.RoomSlot?.SlotId);

            slotObject = JsonSerializer.Deserialize<Entities.Slot>(slotJson);

            Logging.Message.New(0, $"Caching a new dynamic slot under ID {slotObject?.SlotId}");
            SlotCache.Add(userStatus?.CurrentRoom?.RoomSlot?.SlotId ?? 0, slotObject);

            return slotObject ?? null;
        }
    }

    public static class Helpers
    {
        public static string[] StatusBuilder
        (
            Entities.Slot? slot,
            Entities.UserStatus? userStatus,
            Types.StatusType? statusType,
            Types.SlotType? slotType
        )
        {
            string Status = Extensions.StateTypesExtensions.UserStatus(statusType, userStatus);
            string Slot = Extensions.StateTypesExtensions.SlotName(slotType, slot);

            return new [] { Status, Slot };
        }
    }

    public static void UpdatePresence(string username)
    {
        Entities.User? user = Tasks.GetUser(username).Result;
        Entities.UserStatus? userStatus = Tasks.GetStatus(user).Result;
        Entities.Slot? slot = Tasks.GetSlot(user, userStatus).Result;
        Types.StatusType? statusType = userStatus?.StatusType;
        Types.SlotType? slotType = userStatus?.CurrentRoom?.RoomSlot?.SlotType;

        string[] Status = Helpers.StatusBuilder(slot, userStatus, statusType, slotType);

        DateTime lastLogin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(
            user?.LastLogin ?? 0
        );

        var presence = new RichPresence
        {
            Details = $"{Status[0]}",
            State = $"{Status[1]}",
            Assets = new Assets
            {
                LargeImageKey = serverUrl + "/gameAssets/" + slot?.IconHash,
                LargeImageText = slot?.SlotName,
                SmallImageKey = serverUrl + "/gameAssets/" + user?.YayHash,
                SmallImageText = user?.Username,
            },
            Timestamps = new Timestamps { Start = lastLogin, },
            Party = new Party
            {
                ID = $"room:{user?.UserId}:{userStatus?.CurrentRoom?.RoomId}",
                Size = userStatus?.CurrentRoom?.PlayerIds?.Length ?? 0,
                Max = 4,
            },
        };

        DiscordClient.SetPresence(presence);
        Logging.Message.New(0, $"{presence}: Sending presence update.");
    }
}
