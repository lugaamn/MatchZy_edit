using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy
{
    public partial class MatchZy
    {
        [ConsoleCommand("css_whitelist", "Toggles Whitelisting of players")]
        public void OnWLCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_whitelist", "@css/config"))
            {
                isWhitelistRequired = !isWhitelistRequired;
                string WLStatus = isWhitelistRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Whitelist is now {WLStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} Whitelist is now {ChatColors.Green}{WLStatus}{ChatColors.Default}!");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_save_nades_as_global", "Toggles Global Lineups for players")]
        public void OnSaveNadesAsGlobalCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_save_nades_as_global", "@css/config"))
            {
                isSaveNadesAsGlobalEnabled = !isSaveNadesAsGlobalEnabled;
                string GlobalNadesStatus = isSaveNadesAsGlobalEnabled ? "идэвхжүүлсэн" : "идэвхгүй";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Бүх тоглогчдын бүрэлдэхүүнийг хадгалах нь {GlobalNadesStatus}!");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} Бүх тоглогчдын бүрэлдэхүүнийг хадгалах нь {ChatColors.Green}{GlobalNadesStatus}{ChatColors.Default}!");
                }
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_ready", "Marks the player ready")]
        public void OnPlayerReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!ready command] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($" Та аль хэдийн бэлэн гэж тэмдэглэгдсэн байна!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = true;
                        player.PrintToChat($" Таныг бэлэн гэж тэмдэглэсэн байна!");
                    }
                    AddTimer(afterReadyDelay, CheckLiveRequired);
                    HandleClanTags();
                    UnreadyHintMessageStart();
                }
            }
        }

        [ConsoleCommand("css_unready", "Marks the player unready")]
        public void OnPlayerUnReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!unready command] {player.UserId}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (!playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($" Та аль хэдийн бэлэн биш гэж тэмдэглэгдсэн байна!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = false;
                        player.PrintToChat($" Таныг бэлэн биш гэж тэмдэглэсэн байна!");
                    }
                    HandleClanTags();
                    UnreadyHintMessageStart();
                }
            }
        }

        [ConsoleCommand("css_stay", "Stays after knife round")]
        public void OnTeamStay(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!stay command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.PrintToChatAll($" {knifeWinnerName} сонголт {ChatColors.Green}stay{ChatColors.Default}!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_switch", "Switch after knife round")]
        public void OnTeamSwitch(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!switch command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.ExecuteCommand("mp_swapteams;");
                    SwapSidesInTeamData(true);
                    Server.PrintToChatAll($" {knifeWinnerName} сонголт {ChatColors.Green}switch{ChatColors.Default}!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_tech", "Pause the match")]
        public void OnTechCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_pause", "Pause the match")]
        public void OnPauseCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (isPauseCommandForTactical)
            {
                OnTacCommand(player, command);
            }
            else
            {
                PauseMatch(player, command);
            }
        }

        [ConsoleCommand("css_fp", "Pause the matchas an admin")]
        [ConsoleCommand("css_forcepause", "Pause the match as an admin")]
        public void OnForcePauseCommand(CCSPlayerController? player, CommandInfo? command)
        {
            ForcePauseMatch(player, command);
        }

        [ConsoleCommand("css_fup", "Pause the matchas an admin")]
        [ConsoleCommand("css_forceunpause", "Pause the match as an admin")]
        public void OnForceUnpauseCommand(CCSPlayerController? player, CommandInfo? command)
        {
            ForceUnpauseMatch(player, command);
        }

        [ConsoleCommand("css_unpause", "Unpause the match")]
        public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo? command) {
            if (isMatchLive && isPaused) {
                var pauseTeamName = unpauseData["pauseTeam"];
                if ((string)pauseTeamName == "Admin") {
                    player?.PrintToChat($"Тоглолтыг админ түр зогсоосон. Зөвхөн админ завсарлагааг цуцлах боломжтой!");
                    return;
                }
                string unpauseTeamName = "Admin";
                string remainingUnpauseTeam = "Admin";
                if (player?.TeamNum == 2)
                {
                    unpauseTeamName = reverseTeamSides["TERRORIST"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["CT"].teamName;
                    if (!(bool)unpauseData["t"])
                    {
                        unpauseData["t"] = true;
                    }

                } else if (player?.TeamNum == 3) {
                    unpauseTeamName = reverseTeamSides["CT"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["TERRORIST"].teamName;
                    if (!(bool)unpauseData["ct"]) {
                        unpauseData["ct"] = true;
                    }
                } else {
                    return;
                }
                if ((bool)unpauseData["t"] && (bool)unpauseData["ct"]) {
                    Server.PrintToChatAll($" Завсарлага цуцлагдсан тул тоглолт үргэлжлэх болно!");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                }
                else if (unpauseTeamName == "Admin")
                {
                    Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} has unpaused the match, resuming the match!");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                } else {
                    Server.PrintToChatAll($" {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} тоглолтыг үргэлжлүүлэхийг хүсч байна. {ChatColors.Green}{remainingUnpauseTeam}{ChatColors.Default}Баталгаажуулахын тулд {ChatColors.Green}.unpause {ChatColors.Default}гэж бичнэ үү");
                }
                if (!isPaused && pausedStateTimer != null) {
                    pausedStateTimer.Kill();
                    pausedStateTimer = null;
                }
            }
        }

        [ConsoleCommand("css_tac", "Starts a tactical timeout for the requested team")]
        public void OnTacCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;

            if (matchStarted && isMatchLive)
            {
                Log($"[.tac command sent via chat] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
                if (isPaused)
                {
                    ReplyToUserCommand(player, "Match is already paused, cannot start a tactical timeout!");
                    return;
                }
                if (player.TeamNum == 2)
                {
                    Server.ExecuteCommand("timeout_terrorist_start");
                }
                else if (player.TeamNum == 3)
                {
                    Server.ExecuteCommand("timeout_ct_start");
                }
            }
        }

        [ConsoleCommand("css_roundknife", "Toggles knife round for the match")]
        [ConsoleCommand("css_rk", "Toggles knife round for the match")]
        public void OnKifeCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_roundknife", "@css/config"))
            {
                isKnifeRequired = !isKnifeRequired;
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Knife round is now {knifeStatus}!");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} Knife round is now {ChatColors.Green}{knifeStatus}{ChatColors.Default}!");
                }
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_readyrequired", "Sets number of ready players required to start the match")]
        public void OnReadyRequiredCommand(CCSPlayerController? player, CommandInfo command) {
            if (IsPlayerAdmin(player, "css_readyrequired", "@css/config"))
            {
                if (command.ArgCount >= 2) {
                    string commandArg = command.ArgByIndex(1);
                    HandleReadyRequiredCommand(player, commandArg);
                }
                else {
                    string minimumReadyRequiredFormatted = (player == null) ? $"{minimumReadyRequired}" : $"{ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}";
                    ReplyToUserCommand(player, $"Current Ready Required: {minimumReadyRequiredFormatted} .Usage: !readyrequired <number_of_ready_players_required>");
                }                
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_settings", "Shows the current match configuration/settings")]
        public void OnMatchSettingsCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;

            if (IsPlayerAdmin(player, "css_settings", "@css/config"))
            {
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                string playoutStatus = isPlayOutEnabled ? "Enabled" : "Disabled";
                player.PrintToChat($" Current Settings:");
                player.PrintToChat($" Knife: {ChatColors.Green}{knifeStatus}{ChatColors.Default}");
                player.PrintToChat($" Minimum Ready Required: {ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}");
                player.PrintToChat($" Playout: {ChatColors.Green}{playoutStatus}{ChatColors.Default}");
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_restart", "Restarts the match")]
        public void OnRestartMatchCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_restart", "@css/config"))
            {
                if (!isPractice) {
                    ResetMatch();
                } else {
                    ReplyToUserCommand(player, "Дасгалын горим идэвхтэй байгаа тул та тоглолтыг дахин эхлүүлэх боломжгүй.");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_map", "Changes the map using changelevel")]
        public void OnChangeMapCommand(CCSPlayerController? player, CommandInfo command) {
            if (player == null) return;
            var mapName = command.ArgByIndex(1);
            HandleMapChangeCommand(player, mapName);
        }

        [ConsoleCommand("css_rmap", "Reloads the current map")]
        private void OnMapReloadCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;
            if (!IsPlayerAdmin(player))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            string currentMapName = Server.MapName;
            if (long.TryParse(currentMapName, out _))
            { // Check if mapName is a long for workshop map ids
                Server.ExecuteCommand($"host_workshop_map \"{currentMapName}\"");
            }
            else if (Server.IsMapValid(currentMapName))
            {
                Server.ExecuteCommand($"changelevel \"{currentMapName}\"");
            }
            else
            {
                player.PrintToChat($"{chatPrefix} Invalid map name!");
            }
        }

        [ConsoleCommand("css_load", "Load match settings")]
        public void CommandLoad(CCSPlayerController? player, CommandInfo? command)
        {
            //if (player == null) return;

            if (IsPlayerAdmin(player, "css_load", "@css/config"))
            {
                if (isMatchLive)
                {
                    player.PrintToChat($" {ChatColors.Green}Одоогоор тохиргоог ачаалах боломжгүй байна!");
                    return;
                }
                else if (isMatchSetup)
                {
                    player.PrintToChat($" {ChatColors.Green}Тоглолт аль хэдийн тохируулагдсан!");
                    return;
                }
                if (isWarmup || isPractice)
                {
                    Server.ExecuteCommand($"matchzy_loadmatch match.json");
                }
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_start", "Force starts the match")]
        public void OnStartCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;

            if (IsPlayerAdmin(player, "css_start", "@css/config"))
            {
                if (isPractice) {
                    ReplyToUserCommand(player, $"Та дасгалын горимын үед тоглолт эхлүүлэх боломжгүй. {ChatColors.Green}.exitprac {ChatColors.Default}тушаалыг ашиглана уу. {ChatColors.Green}Pug {ChatColors.Default}горим уруу буцах!");
                    return;
                }
                if (matchStarted) {
                    player.PrintToChat($" Start command cannot be used if match is already started! If you want to unpause, please use .unpause");
                } else {
                    Server.PrintToChatAll($" {ChatColors.Green}АDMIN {ChatColors.Default}тоглолтыг эхлүүллээ!");
                    HandleMatchStart();
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_asay", "Say as an admin")]
        public void OnAdminSay(CCSPlayerController? player, CommandInfo? command)
        {
            if (command == null) return;
            if (player == null)
            {
                Server.PrintToChatAll($"{adminChatPrefix} {command.ArgString}");
                return;
            }
            if (!IsPlayerAdmin(player, "css_asay", "@css/chat"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            string message = "";
            for (int i = 1; i < command.ArgCount; i++)
            {
                message += command.ArgByIndex(i) + " ";
            }
            Server.PrintToChatAll($"{adminChatPrefix} {message}");
        }

        [ConsoleCommand("reload_admins", "Reload admins of MatchZy")]
        public void OnReloadAdmins(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "reload_admins", "@css/config"))
            {
                LoadAdmins();
                UpdatePlayersMap();
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_match", "Starts match mode")]
        public void OnMatchCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player, "css_match", "@css/map", "@custom/prac"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            
            if (matchStarted) {
                ReplyToUserCommand(player, "Pug горимыг аль хэдийн тохируулсан байна!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_exitprac", "Starts match mode")]
        public void OnExitPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (matchStarted) {
                ReplyToUserCommand(player, "Pug горимыг аль хэдийн тохируулсан байна!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_rcon", "Triggers provided command on the server")]
        public void OnRconCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsPlayerAdmin(player, "css_rcon", "@css/rcon"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            Server.ExecuteCommand(command.ArgString);
            ReplyToUserCommand(player, "Command sent successfully!");
        }

        [ConsoleCommand("css_help", "Triggers provided command on the server")]
        public void OnHelpCommand(CCSPlayerController? player, CommandInfo? command)
        {
            SendAvailableCommandsMessage(player);
        }

        [ConsoleCommand("css_playout", "Toggles playout (Playing of max rounds)")]
        public void OnPlayoutCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_playout", "@css/config"))
            {
                isPlayOutEnabled = !isPlayOutEnabled;
                string playoutStatus = isPlayOutEnabled ? "Enabled" : "Disabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Playout is now {playoutStatus}!");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} Playout is now {ChatColors.Green}{playoutStatus}{ChatColors.Default}!");
                }

                if (isPlayOutEnabled)
                {
                    Server.ExecuteCommand("mp_match_can_clinch false");
                }
                else
                {
                    Server.ExecuteCommand("mp_match_can_clinch true");
                }

            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

    }
}
