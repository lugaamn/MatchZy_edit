using System;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{

    public partial class MatchZy
    {
        private void CommandRtv(CCSPlayerController? player, CommandInfo commandinfo)
        {
        if (IsPlayerAdmin(player, "css_rtv", "@css/config"))
        {
            if (isMatchSetup)
            {
                PrintToChat(player, $" {ChatColors.Green}Тоглолтын үеэр санал өгөх боломжгүй!");
                return;
            }
            if (isWarmup || isPractice)
            {
                if (player == null) return;

                if (_selectedMap != null)
                {
                    PrintToChat(player, "Санал хураалт дууссан тул дахин эхлүүлэх боломжгүй.");
                    return;
                }

                if (!_isVotingActive)
                {
                    PrintToChat(player, "Санал хураалт үргэлжилж байна!");
                    return;
                }

                var countPlayers = _usersArray.Count(user => user != null);
                var countVote = (int)(countPlayers * _config.Needed) == 0 ? 1 : countPlayers * _config.Needed;
                var user = _usersArray[player.Index]!;
                if (user.VotedRtv)
                {
                    PrintToChat(player, "Та газрын зургийн өөрчлөлтийн төлөө санал өгсөн байна!");
                    return;
                }

                user.VotedRtv = true;
                _votedRtv++;
                PrintToChatAll($" {player.PlayerName} {ChatColors.Green}саналаа өгөхийг хүсч байна.");
                PrintToChatAll($" {ChatColors.Green}(саналын тоо: {ChatColors.Default}{_votedRtv}{ChatColors.Green} - шаардлагатай: {ChatColors.Default}{(int)countVote}{ChatColors.Green})");

                if (_votedRtv == (int)countVote)
                    VoteMap(true);
            }

            else if (isMatchLive || isKnifeRound) {
                PrintToChat(player, $" {ChatColors.Green}Тоглолтын үеэр санал өгөх боломжгүй.");
                return;
            }
        }
        else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        private void VoteMap(bool forced)
        {
            // Define the file path
            var rtvmapsfileName = "MatchZy/rtvmaps.cfg";
            var mapsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", rtvmapsfileName);

            _isVotingActive = false;
            var nominateMenu = new ChatMenu("RTV");
            var mapList = File.ReadAllLines(mapsPath);
            var newMapList = mapList.Except(_playedMaps).Except(_proposedMaps)
                .Select(map => map.StartsWith("ws:") ? map.Substring(3) : map)
                .ToList();

            if (mapList.Length < 7)
            {
                for (int i = 0; i < mapList.Length - 1; i++)
                {
                    if (_proposedMaps[i] == null)
                    {
                        if (newMapList.Count > 0)
                        {
                            var rand = new Random().Next(newMapList.Count);
                            _proposedMaps[i] = newMapList[rand];
                            newMapList.RemoveAt(rand);
                        }
                        else
                        {
                            var unplayedMaps = _playedMaps.Except(_proposedMaps).Where(map => map != NativeAPI.GetMapName())
                                .ToList();
                            if (unplayedMaps.Count > 0)
                            {
                                var rand = new Random().Next(unplayedMaps.Count);
                                _proposedMaps[i] = unplayedMaps[rand];
                                unplayedMaps.RemoveAt(rand);
                            }
                        }
                    }
                    nominateMenu.AddMenuOption($"{_proposedMaps[i]}", (controller, option) =>
                    {
                        if (!optionCounts.TryGetValue(option.Text, out int count))
                            optionCounts[option.Text] = 1;
                        else
                            optionCounts[option.Text] = count + 1;
                        _votedMap++;
                        PrintToChatAll($"{controller.PlayerName} сонгосон {option.Text}");
                    });
                }
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    if (_proposedMaps[i] == null)
                    {
                        if (newMapList.Count > 0)
                        {
                            var rand = new Random().Next(newMapList.Count);
                            _proposedMaps[i] = newMapList[rand];
                            newMapList.RemoveAt(rand);
                        }
                        else
                        {
                            var unplayedMaps = _playedMaps.Except(_proposedMaps).Where(map => map != NativeAPI.GetMapName())
                                .ToList();
                            if (unplayedMaps.Count > 0)
                            {
                                var rand = new Random().Next(unplayedMaps.Count);
                                _proposedMaps[i] = unplayedMaps[rand];
                                unplayedMaps.RemoveAt(rand);
                            }
                        }
                    }

                    nominateMenu.AddMenuOption($"{_proposedMaps[i]}", (controller, option) =>
                    {
                        if (!optionCounts.TryGetValue(option.Text, out int count))
                            optionCounts[option.Text] = 1;
                        else
                            optionCounts[option.Text] = count + 1;
                        _votedMap++;
                        PrintToChatAll($"{controller.PlayerName} сонгосон {option.Text}");
                    });
                }
            }

            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                ChatMenus.OpenMenu(player, nominateMenu);
            }

            AddTimer(35.0f, () => TimerVoteMap(forced));
        }

        private void TimerVoteMap(bool forced)
        {
            if (optionCounts.Count == 0 && forced)
            {
                PrintToChatAll("Шаардлагатай тооны саналд хүрч чадаагүй, одоогийн газрын зураг хэвээр байна!");
                PrintToChatAll("Шаардлагатай тооны саналд хүрч чадаагүй, одоогийн газрын зураг хэвээр байна!");
                PrintToChatAll("Шаардлагатай тооны саналд хүрч чадаагүй, одоогийн газрын зураг хэвээр байна!");
                ResetData();
                return;
            }

            if (_selectedMap != null && !forced)
            {
                if (IsTimeLimit)
                {
                    return;                
                }

                return;
            }

            _selectedMap = optionCounts.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;

            if (forced)
            {
                PrintToChatAll($"Санал хураалтын явцад газрын зургийг сонгосон {ChatColors.Green}{_selectedMap}.");
                PrintToChatAll($"Санал хураалтын явцад газрын зургийг сонгосон {ChatColors.Green}{_selectedMap}.");
                PrintToChatAll($"Санал хураалтын явцад газрын зургийг сонгосон {ChatColors.Green}{_selectedMap}.");

                AddTimer(5, ChangeMapRTV);
                return;
            }

            if (!IsTimeLimit) return;
        }
        private void ChangeMapRTV()
        {
            Server.ExecuteCommand(IsWsMaps(_selectedMap)
            ? $"ds_workshop_changelevel {_selectedMap}"
            : $"map {_selectedMap}");
        }
            private bool IsWsMaps(string selectMap)
        {
            // Define the file path
            string rtvmapsfileName = "MatchZy/rtvmaps.cfg";
            string mapsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", rtvmapsfileName);

            var mapList = File.ReadAllLines(mapsPath);

            return mapList.Any(map => map.Trim() == "ws:" + selectMap);
            ;
        }

        private void PrintToChat(CCSPlayerController controller, string msg)
        {
            controller.PrintToChat($"[{ChatColors.Green}1sT{ChatColors.Default}] {msg}");
        }

        private void PrintToChatAll(string msg)
        {
            Server.PrintToChatAll($"[{ChatColors.Green}1sT{ChatColors.Default}] {msg}");
        }

        private Config LoadConfig()
        {
            string settingsfileName = "MatchZy/settings.json";
            string configPath = Path.Join(Server.GameDirectory + "/csgo/cfg", settingsfileName);

            if (!File.Exists(configPath)) return CreateConfig(configPath);

            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

            return config;
        }

        private Config CreateConfig(string configPath)
        {
            var config = new Config
            {
                Needed = 0.6,
                VotingRoundInterval = 5,
                VotingTimeInterval = 10,
                RoundsBeforeNomination = 6,
            };

            File.WriteAllText(configPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("[MapChooser] The configuration was successfully saved to a file: " + configPath);
            Console.ResetColor();

            return config;
        }

        private void ResetData()
        {
            _isVotingActive = true;
            _votedMap = 0;
            optionCounts = new Dictionary<string, int>(0);
            _votedRtv = 0;
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                _usersArray[player.Index]!.VotedRtv = false;
                _usersArray[player.Index]!.ProposedMaps = null;
            }

            for (var i = 0; i < _proposedMaps.Length; i++)
            {
                _proposedMaps[i] = null;
            }
        }
    }
}

    public class Config
    {
        public int RoundsBeforeNomination { get; set; }
        public float VotingTimeInterval { get; set; }
        public int VotingRoundInterval { get; set; }
        public double Needed { get; set; }
    }

    public class Users
    {
        public required string ProposedMaps { get; set; }
        public bool VotedRtv { get; set; }
    }
