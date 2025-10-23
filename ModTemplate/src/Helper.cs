using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace WordFilter
{
    public static class ProfanityHelper
    {
        #nullable enable

        // Flexible pattern: dots, underscores, stars, or numbers in place of letters
        private static readonly Regex NoisePattern = new(@"[.*_\-]+", RegexOptions.Compiled);

        public static string NormalizeWord(string word, WordFilterConfig config)
        {
            if (string.IsNullOrWhiteSpace(word))
                return string.Empty;

            // --- 1. Split out ending punctuation (so it’s preserved) ---
            var match = Regex.Match(word, @"^(.+?)([!?.]+)?$", RegexOptions.IgnoreCase);
            string coreWord = match.Groups[1].Value;
            string endPunct = match.Groups[2].Value;

            var sb = new StringBuilder(coreWord.Length);

            // --- 2. Replace leets/symbols ---
            foreach (var ch in coreWord)
            {
                bool replaced = false;

                foreach (var (symbolVariants, value) in config.SymbolsToChar)
                {
                    foreach (var variant in symbolVariants.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (variant.Contains(ch))
                        {
                            sb.Append(value);
                            replaced = true;
                            break;
                        }
                    }

                    if (replaced) break;
                }

                if (!replaced)
                    sb.Append(ch);
            }

            // --- 3. Remove noise characters inside the word (not punctuation) ---
            string normalized = NoisePattern.Replace(sb.ToString(), string.Empty);

            // --- 4. Trim leading punctuation, but NOT the end ---
            normalized = Regex.Replace(normalized, @"^[^a-z0-9]+", string.Empty, RegexOptions.IgnoreCase);

            // --- 5. Reattach ending punctuation (if any) ---
            normalized += endPunct;

            return normalized;
        }

        public static bool ContainsProfanity(string message, WordFilterConfig config)
        {
            // Split message into words, ignoring punctuation
            var tokens = Regex.Matches(message, @"[a-zA-Z0-9!@#$%^&*_\-]+")
                             .Select(m => m.Value)
                             .ToList();

            foreach (var token in tokens.Skip(3))
            {
                string normalizedToken = NormalizeWord(token, config);

                string normalizedAcronym = Acronym(normalizedToken, config, out bool value);

                foreach (var filters in config.Filters)
                {
                    var variants = filters.Word.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var variant in variants)
                    {
                        if (!config.SafeWords.Contains(token))
                        {
                            if (value && Regex.IsMatch(normalizedAcronym, variant, RegexOptions.IgnoreCase))
                            {
                                return true;
                            }

                            if (Regex.IsMatch(normalizedToken, variant, RegexOptions.IgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static string FilterMessage(string message, WordFilterConfig config)
        {
            return Regex.Replace(message, @"[a-zA-Z0-9!@#$%^&*_\-]+", match =>
            {   // Collapse spaced-letter sequences (e.g. "F U C K") so they're treated as a single token

                string token = match.Value;

                string normalizedToken = NormalizeWord(token, config);

                string normalizedAcronym = Acronym(normalizedToken, config, out bool value);

                foreach (var filters in config.Filters)
                {
                    var variants = filters.Word.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var variant in variants)
                    {
                        if (!config.SafeWords.Contains(token))
                        {
                            if (value && Regex.IsMatch(normalizedAcronym, variant, RegexOptions.IgnoreCase))
                            {
                                return CensorBannedWord(normalizedAcronym, variant, filters.Replacement);
                            }

                            if (Regex.IsMatch(normalizedToken, variant, RegexOptions.IgnoreCase))
                            {
                                return CensorBannedWord(normalizedToken, variant, filters.Replacement);
                            }
                        }
                    }
                }

                return token; // leave original
            });
        }

        private static string Acronym(string normalized, WordFilterConfig config, out bool value)
        {
            string token = normalized;

            foreach (var (acronymVariants, expansion) in config.Acronyms)
            {
                var variants = acronymVariants
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var variant in variants)
                {
                    if (token.Contains(variant, StringComparison.OrdinalIgnoreCase))
                    {
                        // Option 1: Expand the acronym (replace it)
                        token = Regex.Replace(
                        normalized,
                        $@"\b{Regex.Escape(variant)}\b",
                        expansion,
                        RegexOptions.IgnoreCase
                    );
                    }
                }
            }

            value = token != normalized;
            return token;
        }

        private static string CensorBannedWord(string originalToken, string bannedWord, string? replacement = null)
        {
            if (string.IsNullOrEmpty(originalToken) || string.IsNullOrEmpty(bannedWord))
                return originalToken;

            return Regex.Replace(originalToken, bannedWord, match =>
            {

                string? censored = replacement;

                if (string.IsNullOrEmpty(replacement))
                {
                    censored = new string('*', match.Value.Length);
                    return censored;
                }

                return replacement;

            }, RegexOptions.IgnoreCase);
        }

        public static string StripPlayerPrefix(string name, string message)
        {
            int idx = message.IndexOf("</strong>");
            return (idx >= 0 && idx + 9 < message.Length)
                ? message[(idx + 9)..].TrimStart()
                : message;
        }

        // Collapse sequences like "f u c k" into "fuck" only if the joined letters match a banned word.
        // Otherwise leave the original spaced sequence alone.
        //private static string CollapseSpacedLettersIfBanned(string message, WordFilterConfig config)
        //{
        //    var rawTokens = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        //    var outTokens = new List<string>();

        //    foreach (var word in config.Filters.Select(s => s.Word)) 
        //    { 
        //        if(rawTokens.All(w => w.Length == 1))
        //        {

        //            foreach(var tokens in rawTokens)
        //            {

        //            }

        //            if (message.Contains(word))
        //            {
        //                _ = message;
        //            }
        //        }
        //    }
        //    return message;
        //}
    }
}
