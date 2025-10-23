using System;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace WordFilter
{
    #nullable enable
    public class WordFilterModSystem : ModSystem
    {
        public required ICoreServerAPI sapi;

        public WordFilterConfig Config = new();

        public string LogFilePath = string.Empty;

        public const string ConfigName = "WordFilter.json";

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            LogFilePath = Path.Combine(api.GetOrCreateDataPath("Logs"), "server-wordfilter.txt");

            if (!File.Exists(LogFilePath))
            {
                File.WriteAllText(LogFilePath, "=== Word Filter Log Started ===\n");
            }

            LoadOrCreateConfig();

            //// Register commands
            api.ChatCommands.GetOrCreate("wordfilter")
                .RequiresPrivilege(Privilege.gamemode)
                .WithDescription("Word filter management command")
                .BeginSubCommand("list")
                .WithDescription("Shows list of filter words. (Only)")
                .HandleWith(ShowList).EndSubCommand()
                .BeginSubCommand("add")
                .WithDescription("Add a filter word with optional endings")
                .WithExamples(["word: (shit), replacement: (poo)", "word: (arse|ass), replacement: (butt)"])
                .WithArgs([
                        api.ChatCommands.Parsers.Word("word"),          // base word (or OR pattern)
                        api.ChatCommands.Parsers.OptionalWord("replacement"),   // replacement word
                ])
                .HandleWith(AddTo)
                .EndSubCommand()
                .BeginSubCommand("remove")
                .WithDescription("Removes a filter word")
                    .WithArgs([api.ChatCommands.Parsers.Word("word")])
                    .HandleWith(RemoveFrom).EndSubCommand();


            api.Event.PlayerChat += Event_Catch_Message;
        }

        private void LoadOrCreateConfig()
        {
            try
            {
                // Try to load existing config
                Config = sapi!.LoadModConfig<WordFilterConfig>(ConfigName);

                if (Config?.WordFilters != null && Config.WordFilters.Count > 0)
                {
                    foreach (var filter in Config.WordFilters)
                    {
                        if (Config.Filters.Contains(filter)) continue;

                        Config.Filters.Add(filter);
                    }

                    Config.WordFilters.Clear();
                }

                // If missing or empty, load default
                if (Config == null || Config.Filters.Count == 0)
                {
                    Config = WordFilterConfig.LoadDefault(sapi);
                }

                Config.WordFilters = null;

                // Save it to disk to ensure file exists
                sapi.StoreModConfig(Config, ConfigName);
            }
            catch (Exception ex)
            {
                sapi!.Logger.Error("Failed to load or create config: " + ex);

                // Fallback to default
                Config = WordFilterConfig.LoadDefault(sapi);
                sapi.StoreModConfig(Config, ConfigName);
            }
        }

        public void Event_Catch_Message(IServerPlayer player, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (string.IsNullOrWhiteSpace(message) || sapi == null) return;

            if (message.StartsWith('/') || message.StartsWith('.')) return;

            if (ProfanityHelper.ContainsProfanity(message, Config))
            {
                consumed.value = true;

                LogFilteredWord(player, channelId, message);

                message = ProfanityHelper.FilterMessage(message, Config);

                _ = message;

                consumed.value = false;

                //sapi.SendMessageToGroup(channelId, filtered, EnumChatType.AllGroups, data);
            }
        }

        private TextCommandResult ShowList(TextCommandCallingArgs args)
        {
            if (Config.Filters.Count == 0)
                return TextCommandResult.Error("Filter list is empty.");

            string list = string.Join("\n", Config.Filters.Select(wr =>
                $"{wr.Word} / {(string.IsNullOrEmpty(wr.Replacement) ? "emtpy" : wr.Replacement)}"));

            return TextCommandResult.Success($"Current filters:\n{list}");
        }

        private TextCommandResult AddTo(TextCommandCallingArgs args)
        {
            var filter = new WordFilters
            {
                Word = (string)args.Parsers[0].GetValue() ?? string.Empty,
                Replacement = (string)args.Parsers[1].GetValue() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(filter.Word))
                return TextCommandResult.Error($"Error: {filter}");


            Config.Filters.RemoveWhere(f =>  string.Equals(f.Word, filter.Word, StringComparison.OrdinalIgnoreCase));

            Config.Filters.Add(filter);

            sapi!.StoreModConfig(Config, ConfigName);
            return TextCommandResult.Success(
                $"Added filter: {filter.Word} - {filter.Replacement}");
        }

        private TextCommandResult RemoveFrom(TextCommandCallingArgs args)
        {
            string word = (string)args.Parsers[0].GetValue() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(word))
            {
                return TextCommandResult.Error("No word provided to remove.");
            }

            // Remove filters whose Word matches the provided word (case-insensitive)
            int removedCount = Config.Filters.RemoveWhere(f => string.Equals(f.Word, word, StringComparison.OrdinalIgnoreCase));

            if (removedCount == 0)
            {
                return TextCommandResult.Error($"No filters found for: {word}");
            }

            sapi!.StoreModConfig(Config, ConfigName);

            return TextCommandResult.Success($"Removed filter for: {word}");
        }

        private void LogFilteredWord(IServerPlayer player, int channelId, string message)
        {
            if (!Config.ShouldLog) return;

            string result = ProfanityHelper.StripPlayerPrefix(player.PlayerName, message);
            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [Chat] {channelId} | {player.PlayerName}: {result}";

            using StreamWriter sw = new(LogFilePath, append: true);
            sw.WriteLine(logLine);
        }

    }
}
