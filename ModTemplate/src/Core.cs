using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace WordFilter
{
    #nullable enable

    public class WordReplacement
    {
        public required string Word { get; set; }

        public required string Replacement { get; set; }
    }

    public class WordFilterConfig
    {
        public bool ShouldLog = true;

        public List<WordReplacement> WordFilter { get; set; } = new List<WordReplacement>();

        public void Init()
        {
            WordFilter = new List<WordReplacement>()
            {
                new WordReplacement { Word = "fuck*", Replacement = "fluffy" },
                new WordReplacement { Word = "dick*", Replacement = "sausage" }
            };
        }

        public bool Match(string word)
        {
            WordReplacement? wordFilter = WordFilter.Find(w => w.Word == word);

            if (wordFilter != null)
            {
                return true;
            }

            return false;
        }
    }

    public class WordFilterModSystem : ModSystem
    {
        public ICoreServerAPI? coreServerAPI { get; set; }

        public string LogFilePath = string.Empty;

        public WordFilterConfig Config = new WordFilterConfig();

        public List<(Regex Pattern, string Replacement)> bannedWordPatterns = new List<(Regex Pattern, string Replacement)>();

        public const string ConfigName = "WordFilter.json";

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            coreServerAPI = api;

            var logger = api.Logger;

            try
            {
                // Attempt to load the configuration file
                Config = api.LoadModConfig<WordFilterConfig>(ConfigName);

                if (Config == null)
                {
                    // Create a new config if not found
                    Config = new WordFilterConfig();
                    Config.Init();
                    logger.VerboseDebug($"Config file '{ConfigName}' not found. Creating a new one with default values...");
                    api.StoreModConfig(Config, ConfigName);
                }
                else
                {
                    // Store the loaded config back to ensure all default values are present
                    api.StoreModConfig(Config, ConfigName);
                    logger.VerboseDebug($"Config file '{ConfigName}' loaded successfully.");
                }
            }
            catch (Exception e)
            {
                // Log detailed error information and create a fallback config
                logger.Error($"Failed to load config file '{ConfigName}'. Error: {e.Message}");
                Config = new WordFilterConfig();
                Config.Init();
                api.StoreModConfig(Config, ConfigName);
                logger.Error("A new config file with default values has been created.");
            }

            LogFilePath = Path.Combine(api.GetOrCreateDataPath("Logs"), "server-wordfilter.txt");

            if (!File.Exists(LogFilePath))
            {
                File.WriteAllText(LogFilePath, "=== Word Filter Log Started ===\n");
            }

            AddOrReload();

            api.ChatCommands.GetOrCreate("wordfilter").RequiresPrivilege(Privilege.gamemode).WithDescription("Word filter management command")
                .BeginSubCommand("list").HandleWith(ShowList).WithDescription("List current filters").EndSubCommand()
                .BeginSubCommand("add").WithArgs([api.ChatCommands.Parsers.Word("word"), api.ChatCommands.Parsers.Word("replacement")]).HandleWith(AddTo).WithDescription("Add a filter").EndSubCommand()
                .BeginSubCommand("remove").WithArgs([api.ChatCommands.Parsers.Word("word")]).HandleWith(RemoveFrom).WithDescription("Remove a filter").EndSubCommand();


            api.Event.PlayerChat += Event_PlayerChat;
        }

        public void AddOrReload()
        {
            bannedWordPatterns = Config.WordFilter
                .Select(wr => (new Regex( "^" + Regex.Escape(wr.Word).Replace("\\*", "\\w*") + "$",RegexOptions.IgnoreCase | RegexOptions.Compiled),
                wr.Replacement
                )).ToList();
        }

        private void Event_PlayerChat(IServerPlayer byPlayer, int channelId, ref string message, ref string data, BoolRef consumed)
        {
            if (string.IsNullOrEmpty(message) || coreServerAPI == null) return;

            if (message.StartsWith('/') || message.StartsWith('.')) return;

            string cleaned = FilteredWords(message);

            if (cleaned != message)
            {
                // Block the original
                consumed.value = true;

                // Log the original bad message
                LogFilteredWord(byPlayer, channelId, message);

                // Replace the player's original message with the censored one
                message = cleaned;

                // Send censored one instead
                coreServerAPI.SendMessageToGroup(channelId,
                   message, EnumChatType.AllGroups, data
                );

                //rebuild it to match how it should have looked.
                string result = $"{channelId} | {byPlayer.PlayerName}: {StripPlayerPrefix(byPlayer.PlayerName, message)}";

                coreServerAPI.Server.LogChat(result);
            }

            return;
        }

        private TextCommandResult ShowList(TextCommandCallingArgs args)
        {
            if (Config.WordFilter == null || Config.WordFilter.Count == 0)
            {
                return TextCommandResult.Error("Current filter list is empty.");
            }

            string list = string.Join("\n",
                Config.WordFilter.Select(wr => $"{Safe(wr.Word)} / {Safe(wr.Replacement)}")
            );

            coreServerAPI?.Logger.Notification("WordFilter list: " + list); // debug log

            return TextCommandResult.Success($"Current filters:\n{list}");
        }

        private TextCommandResult AddTo(TextCommandCallingArgs args)
        {
            if(coreServerAPI == null) return TextCommandResult.Success();

            string? word = args[0]?.ToString();
            string? replacement = args[1]?.ToString();

            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(replacement)) return TextCommandResult.Success();

            Config.WordFilter.Add(new WordReplacement { Word = word, Replacement = replacement });

            coreServerAPI.StoreModConfig(Config, ConfigName);

            AddOrReload();

            return TextCommandResult.Success($"Added filter: {word} -> {replacement}");
        }

        private TextCommandResult RemoveFrom(TextCommandCallingArgs args)
        {
            if (coreServerAPI == null) return TextCommandResult.Success();

            string? word = args[0].ToString();

            if (string.IsNullOrEmpty(word)) return TextCommandResult.Success();
       

            if (Config.Match(word)) 
            {
                Config.WordFilter.RemoveAll(wr => wr.Word.Equals(word, StringComparison.OrdinalIgnoreCase));

                coreServerAPI.StoreModConfig(Config, ConfigName);

                AddOrReload();

                return TextCommandResult.Success($"Removed filter for: {word}");
            }

            return TextCommandResult.Error($"Can't find: {word}");

        }

        private void LogFilteredWord(IServerPlayer player, int channelId, string message)
        {
            if (!Config.ShouldLog) return;

            string result = StripPlayerPrefix(player.PlayerName, message);

            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [Chat] {channelId} | {player.PlayerName}: {result}";

            using (StreamWriter sw = new StreamWriter(LogFilePath))
            {
                sw.WriteLine(logLine);
            }
        }
        private string Safe(string text)
        {
            return text.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private string FilteredWords(string message)
        {
            string[] words = message.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                foreach (var (pattern, replacement) in bannedWordPatterns)
                {
                    if (pattern.IsMatch(words[i]))
                    {
                        words[i] = replacement;
                        break;
                    }
                }
            }
            return string.Join(" ", words);
        }

        private string StripPlayerPrefix(string name, string message)
        {
            int idx = message.IndexOf("</strong>");
            if (idx >= 0 && idx + 9 < message.Length)
            {
                return message.Substring(idx + 9).TrimStart();
            }
            return message;
        }
    }
}
