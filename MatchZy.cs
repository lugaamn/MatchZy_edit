using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy
{
    [MinimumApiVersion(107)]
    public partial class MatchZy : BasePlugin
    {

        public override string ModuleName => "MatchZy";
        public override string ModuleVersion => "0.5.1-alpha (siniii edit-0.4.4)";
        public override string ModuleAuthor => "WD- (https://github.com/shobhit-pathak/)";
        public override string ModuleDescription => "A plugin for running and managing CS2 practice/pugs/scrims/matches!";

        public string chatPrefix = $"[{ChatColors.Green}1sT{ChatColors.Default}]";
        public string adminChatPrefix = $"[{ChatColors.Green}ADMIN{ChatColors.Default}]";

        // RTV data
        private Config _config;
        private Dictionary<string, int> optionCounts = new Dictionary<string, int>();
        private Users?[] _usersArray = new Users?[65];

        private int _votedRtv;
        private int _votedMap;
        private int _countRounds;
        private float _timeLimit;

        private string? _selectedMap;
        private string[] _proposedMaps = new string[7];
        private List<string> _playedMaps = new List<string>();

        private bool _isVotingActive;
        private bool IsTimeLimit;

        // Plugin start phase data
        public bool isPractice = false;
        public bool isSleep = false;
        public bool readyAvailable = false;
        public bool matchStarted = false;
        public bool isWarmup = false;
        public bool isKnifeRound = false;
        public bool isSideSelectionPhase = false;
        public bool isMatchLive = false;
        public long liveMatchId = -1;
        public int autoStartMode = 1;

        // Pause Data
        public bool isPaused = false;
        public Dictionary<string, object> unpauseData = new Dictionary<string, object> {
            { "ct", false },
            { "t", false },
            { "pauseTeam", "" }
        };

        bool isPauseCommandForTactical = false;

        // Knife Data
        public int knifeWinner = 0;
        public string knifeWinnerName = "";

        // Players Data (including admins)
        public int connectedPlayers = 0;
        private Dictionary<int, bool> playerReadyStatus = new Dictionary<int, bool>();
        private Dictionary<int, CCSPlayerController> playerData = new Dictionary<int, CCSPlayerController>();

        // Admin Data
        private Dictionary<string, string> loadedAdmins = new Dictionary<string, string>();

        // Timers
        public CounterStrikeSharp.API.Modules.Timers.Timer? unreadyPlayerMessageTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? sideSelectionMessageTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? pausedStateTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? pracMessageTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? unreadyHintMessageTimmer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? restoreServerTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? _mapTimer = null;
        public CounterStrikeSharp.API.Modules.Timers.Timer? roundKnifeStartMessageTimer = null;

        // Each message is kept in chat display for ~13 seconds, hence setting default chat timer to 12 seconds.
        // Configurable using matchzy_chat_messages_timer_delay <seconds>
        public int chatTimerDelay = 12;
        public int pracMessageDelay = 55;
        public int unreadyHintMessageDelay = 3;
        public int restoreServerDelay = 5;
        public int roundKnifeStartMessageDelay = 11;
        public int afterReadyDelay = 3;

        // Game Config
        public bool isKnifeRequired = true;
        public int minimumReadyRequired = 2; // Number of ready players required start the match. If set to 0, all connected players have to ready-up to start the match.
        public bool isWhitelistRequired = false;
        public bool isPlayOutEnabled = false;
        public bool isSaveNadesAsGlobalEnabled = false;

        // User command - action map
        public Dictionary<string, Action<CCSPlayerController?, CommandInfo?>>? commandActions;

        // SQLite/MySQL Database 
        private Database database = new();

        public override void Load(bool hotReload) {
            // RTV data
            _config = LoadConfig();

            RegisterListener<Listeners.OnClientConnected>(slot =>
            {
                _usersArray[slot + 1] = new Users { ProposedMaps = null!, VotedRtv = false };
            });

            RegisterListener<Listeners.OnMapStart>(name =>
            {
                ResetData();
                _mapTimer = null;
                _countRounds = 0;
                _selectedMap = null;
                if (_playedMaps.Count >= _config.RoundsBeforeNomination)
                    _playedMaps.RemoveAt(0);

                if (!_playedMaps.Contains(name))
                    _playedMaps.Add(name);
            });
            RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
            {
                if (_usersArray[slot + 1]!.VotedRtv)
                    _votedRtv--;

                for (var index = 0; index < _proposedMaps.Length; index++)
                {
                    if (_usersArray[slot + 1]!.ProposedMaps == _proposedMaps[index])
                        _proposedMaps[index] = null!;
                }

                _usersArray[slot + 1] = null!;
            });
            AddCommand("css_rtv", "", CommandRtv);
            // End of RTV data

            LoadAdmins();

            database.InitializeDatabase(ModuleDirectory);

            // This sets default config ConVars
            Server.ExecuteCommand("execifexists MatchZy/config.cfg");

            teamSides[matchzyTeam1] = "CT";
            teamSides[matchzyTeam2] = "TERRORIST";
            reverseTeamSides["CT"] = matchzyTeam1;
            reverseTeamSides["TERRORIST"] = matchzyTeam2;

            if (!hotReload) {
                AutoStart();
            } else {
                // Pluign should not be reloaded while a match is live (this would messup with the match flags which were set)
                // Only hot-reload the plugin if you are testing something and don't want to restart the server time and again.
                UpdatePlayersMap();
            }

            commandActions = new Dictionary<string, Action<CCSPlayerController?, CommandInfo?>> {
                { ".load", CommandLoad },
                { ".rtv", CommandRtv },
                { ".ready", OnPlayerReady },
                { ".rdy", OnPlayerReady },
                { ".r", OnPlayerReady },
                { ".unready", OnPlayerUnReady },
                { ".ur", OnPlayerUnReady },
                { ".stay", OnTeamStay },
                { ".switch", OnTeamSwitch },
                { ".swap", OnTeamSwitch },
                { ".tech", OnTechCommand },
                { ".pause", OnPauseCommand },
                { ".unpause", OnUnpauseCommand },
                { ".forcepause", OnForcePauseCommand },
                { ".fp", OnForcePauseCommand },
                { ".forceunpause", OnForceUnpauseCommand },
                { ".fup", OnForceUnpauseCommand },
                { ".tac", OnTacCommand },
                { ".roundknife", OnKifeCommand },
                { ".rk", OnKifeCommand },
                { ".playout", OnPlayoutCommand },
                { ".start", OnStartCommand },
                { ".restart", OnRestartMatchCommand },
                { ".reloadmap", OnMapReloadCommand },
                { ".settings", OnMatchSettingsCommand },
                { ".whitelist", OnWLCommand },
                { ".globalnades", OnSaveNadesAsGlobalCommand },
                { ".reload_admins", OnReloadAdmins },
                { ".prac", OnPracCommand },
                { ".bot", OnBotCommand },
                { ".crouchbot", OnCrouchBotCommand },
                { ".boost", OnBoostBotCommand },
                { ".crouchboost", OnCrouchBoostBotCommand },
                { ".nobots", OnNoBotsCommand },
                { ".god", OnGodCommand },
                { ".ff", OnFastForwardCommand },
                { ".fastforward", OnFastForwardCommand },
                { ".clear", OnClearCommand },
                { ".match", OnMatchCommand },
                { ".uncoach", OnUnCoachCommand },
                { ".exitprac", OnExitPracCommand },
                { ".stop", OnStopCommand },
                { ".help", OnHelpCommand },
                { ".t", OnTCommand },
                { ".ct", OnCTCommand },
                { ".spec", OnSpecCommand },
                { ".fas", OnFASCommand },
                { ".watchme", OnFASCommand }
            };

            RegisterEventHandler<EventPlayerConnectFull>((@event, info) => {
                try
                {
                    Log($"[FULL CONNECT] Player ID: {@event.Userid.UserId}, Name: {@event.Userid.PlayerName} has connected!");
                    CCSPlayerController player = @event.Userid;

                    // Handling whitelisted players
                    if (!player.IsBot || !player.IsHLTV)
                    {
                        var steamId = player.SteamID;

                        string whitelistfileName = "MatchZy/whitelist.cfg";
                        string whitelistPath = Path.Join(Server.GameDirectory + "/csgo/cfg", whitelistfileName);
                        string? directoryPath = Path.GetDirectoryName(whitelistPath);
                        if (directoryPath != null)
                        {
                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }
                        }
                        if (!File.Exists(whitelistPath)) File.WriteAllLines(whitelistPath, new[] { "Steamid1", "Steamid2" });

                        var whiteList = File.ReadAllLines(whitelistPath);

                        if (isWhitelistRequired == true)
                        {
                            if (!whiteList.Contains(steamId.ToString()))
                            {
                                Log($"[EventPlayerConnectFull] KICKING PLAYER STEAMID: {steamId}, Name: {player.PlayerName} (Not whitelisted!)");
                                Server.ExecuteCommand($"kickid {(ushort)player.UserId}");
                                return HookResult.Continue;
                            }
                        }
                        if (isMatchSetup || matchModeOnly)
                        {
                            CsTeam team = GetPlayerTeam(player);
                            Log($"[EventPlayerConnectFull] KICKING PLAYER STEAMID: {steamId}, Name: {player.PlayerName} (NOT ALLOWED!)");
                            if (team == CsTeam.None)
                            {
                                Server.ExecuteCommand($"kickid {(ushort)player.UserId}");
                                return HookResult.Continue;
                            }
                        }
                    }

                    player.PrintToChat($" {ChatColors.Default}Тавтай морил {chatPrefix} сервэрт!");
                    player.PrintToChat($"{chatPrefix}: Газрын зургийг өөрчлөх санал хураалт {ChatColors.Green}!rtv");
                    player.PrintToChat($"{chatPrefix}: Тоглолтыг эхлүүлэхийн тулд {ChatColors.Green}.ready {ChatColors.Default}эсвэл {ChatColors.Green}.rdy {ChatColors.Default}гэж бичнэ үү");
                    player.PrintToChat($"{chatPrefix}: Дасгалын горимыг эхлүүлэхийн тулд {ChatColors.Green}.prac {ChatColors.Default}гэж бичнэ үү");

                    if (player.UserId.HasValue)
                    {

                        playerData[player.UserId.Value] = player;
                        connectedPlayers++;
                        if (readyAvailable && !matchStarted)
                        {
                            playerReadyStatus[player.UserId.Value] = false;
                        }
                        else
                        {
                            playerReadyStatus[player.UserId.Value] = true;
                        }
                    }
                    // May not be required, but just to be on safe side so that player data is properly updated in dictionaries
                    UpdatePlayersMap();
                    HandleClanTags();
                    UnreadyHintMessageStart();

                    if (readyAvailable && !matchStarted)
                    {
                        // Start Warmup when first player connect and match is not started.
                        if (GetRealPlayersCount() == 1)
                        {
                            Log($"[FULL CONNECT] First player has connected, starting warmup!");
                            Server.ExecuteCommand($"sv_hibernate_when_empty 0");
                            ExecUnpracCommands();
                            AutoStart();
                        }
                    }
                    return HookResult.Continue;

                }
                catch (Exception e)
                {
                    Log($"[EventPlayerConnectFull FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }
            });

            RegisterEventHandler<EventPlayerDisconnect>((@event, info) => {
                try
                {
                    CCSPlayerController player = @event.Userid;
                    if (player.UserId.HasValue)
                    {
                        if (playerReadyStatus.ContainsKey(player.UserId.Value))
                        {
                            playerReadyStatus.Remove(player.UserId.Value);
                            connectedPlayers--;
                        }
                        if (playerData.ContainsKey(player.UserId.Value))
                        {
                            playerData.Remove(player.UserId.Value);
                        }

                        if (matchzyTeam1.coach == player)
                        {
                            matchzyTeam1.coach = null;
                            player.Clan = "";
                        }
                        else if (matchzyTeam2.coach == player)
                        {
                            matchzyTeam2.coach = null;
                            player.Clan = "";
                        }
                        if (GetRealPlayersCount() == 0)
                        {
                            Server.ExecuteCommand($"sv_hibernate_when_empty 0");
                            if (restoreServerTimer == null)
                            {
                                restoreServerTimer = AddTimer(restoreServerDelay, RestoreServerConfig);
                            }
                        }
                    }
                    HandleClanTags();
                    UnreadyHintMessageStart();
                    return HookResult.Continue;
                }
                catch (Exception e)
                {
                    Log($"[EventPlayerDisconnect FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }
            });

            RegisterListener<Listeners.OnClientDisconnectPost>(playerSlot => { 
               // May not be required, but just to be on safe side so that player data is properly updated in dictionaries
                UpdatePlayersMap();
            });

            RegisterEventHandler<EventCsWinPanelRound>((@event, info) => {
                Log($"[EventCsWinPanelRound PRE] finalEvent: {@event.FinalEvent}");
                if (isKnifeRound && matchStarted) {
                    HandleKnifeWinner(@event);
                }
                return HookResult.Continue;
            }, HookMode.Pre);

            RegisterEventHandler<EventCsWinPanelMatch>((@event, info) => {
                try
                {
                    Log($"[EventCsWinPanelMatch]");
                    HandleMatchEnd();
                    // ResetMatch();
                    return HookResult.Continue;
                }
                catch (Exception e)
                {
                    Log($"[EventCsWinPanelMatch FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }

            });

            RegisterEventHandler<EventRoundStart>((@event, info) => {
                try
                {
                    HandlePostRoundStartEvent(@event);
                    return HookResult.Continue;
                }
                catch (Exception e)
                {
                    Log($"[EventRoundStart FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }

            });

            RegisterEventHandler<EventRoundFreezeEnd>((@event, info) => {
                try
                {
                    HandlePostRoundFreezeEndEvent(@event);
                    return HookResult.Continue;
                }
                catch (Exception e)
                {
                    Log($"[EventRoundFreezeEnd FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }

            });

            RegisterEventHandler<EventPlayerTeam>((@event, info) => {
                CCSPlayerController player = @event.Userid;

                if (matchzyTeam1.coach == player || matchzyTeam2.coach == player)
                {
                    @event.Silent = true;
                    return HookResult.Changed;
                }
                return HookResult.Continue;
            }, HookMode.Pre);

            RegisterEventHandler<EventPlayerTeam>((@event, info) =>
            {
                CCSPlayerController player = @event.Userid;

                if (player.IsHLTV || player.IsBot || !isMatchSetup)
                {
                    return HookResult.Continue;
                }

                CsTeam playerTeam = GetPlayerTeam(player);

                Log($"[EventPlayerTeam] PLAYER TEAM DETERMINED: {(int)playerTeam}");

                if (@event.Team != (int)playerTeam)
                {
                    if (player.IsValid)
                    {

                        Server.NextFrame(() =>
                        {
                            player.SwitchTeam(playerTeam);
                            // Server.NextFrame(() =>
                            // {
                            //     player.PlayerPawn.Value.CommitSuicide(explode: true, force: true);
                            // });
                        });
                    }
                }
                return HookResult.Continue;
            });

            AddCommandListener("jointeam", (player, info) =>
            {
                if (isMatchSetup && player != null && player.IsValid)
                {
                    if (int.TryParse(info.ArgByIndex(1), out int joiningTeam))
                    {
                        int playerTeam = (int)GetPlayerTeam(player);
                        Log($"[jointeam] PLAYER TEAM DETERMINED: PlayerName: {player.PlayerName}, PlayerTeam: {playerTeam}");
                        if (joiningTeam != playerTeam)
                        {
                            return HookResult.Stop;
                        }
                    }

                }
                return HookResult.Continue;
            });

            RegisterEventHandler<EventRoundEnd>((@event, info) => {
                Log($"[EventRoundEnd PRE] Winner: {@event.Winner}, Reason: {@event.Reason}");
                if (isKnifeRound) {
                    @event.Winner = knifeWinner;
                    int finalEvent = 10;
                    if (knifeWinner == 3) {
                        finalEvent = 8;
                    } else if (knifeWinner == 2) {
                        finalEvent = 9;
                    }
                    @event.Reason = finalEvent;
                    Log($"[EventRoundEnd Updated] Winner: {@event.Winner}, Reason: {@event.Reason}");
                    isSideSelectionPhase = true;
                    isKnifeRound = false;
                    StartAfterKnifeWarmup();
                }
                return HookResult.Continue;
            }, HookMode.Pre);

            RegisterEventHandler<EventRoundEnd>((@event, info) => {
                try
                {
                    if (!isMatchLive) return HookResult.Continue;
                    Log($"[EventRoundEnd POST] Winner: {@event.Winner}, Reason: {@event.Reason}");
                    HandlePostRoundEndEvent(@event);
                    return HookResult.Continue;
                }
                catch (Exception e)
                {
                    Log($"[EventRoundEnd FATAL] An error occurred: {e.Message}");
                    return HookResult.Continue;
                }

            }, HookMode.Post);

            RegisterListener<Listeners.OnMapStart>(mapName => {
                Log($"[Listeners.OnMapStart]");
                if (isWarmup) StartWarmup();
            });

            RegisterEventHandler<EventPlayerDeath>((@event, info) => {
                // Setting money back to 16000 when a player dies in warmup
                var player = @event.Userid;
                if (isWarmup) {
                    if (player.InGameMoneyServices != null) player.InGameMoneyServices.Account = 16000;
                }
                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerHurt>((@event, info) =>
            {
                CCSPlayerController attacker = @event.Attacker;
                CCSPlayerController victim = @event.Userid;

                if (isPractice)
                {
                    if (victim.IsBot)
                    {
                        int damage = @event.DmgHealth;
                        int postDamageHealth = @event.Health;
                        @event.Attacker.PrintToChat($" ===> {ChatColors.Green}[{damage} DMG]{ChatColors.Default} to BOT {victim.PlayerName} {ChatColors.Green}({postDamageHealth} health){ChatColors.Default}");
                    }
                    return HookResult.Continue;
                }

                if (!attacker.IsValid || attacker.IsBot && !(@event.DmgHealth > 0 || @event.DmgArmor > 0))
                    return HookResult.Continue;
                if (matchStarted)
                {
                    if (@event.Userid.TeamNum != attacker.TeamNum)
                    {
                        int targetId = (int)@event.Userid.UserId!;

                        UpdatePlayerDamageInfo(@event, targetId);
                    }
                }

                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerChat>((@event, info) => {

                int currentVersion = Api.GetVersion();
                int index = @event.Userid;
                // From APIVersion 50 and above, EventPlayerChat userid property will be a "slot", rather than an entity index 
                // Player index is slot + 1
                if (currentVersion >= 50)
                {
                    index += 1;
                }
                var playerUserId = NativeAPI.GetUseridFromIndex(index);
                Log($"[EventPlayerChat] UserId(Index): {index} playerUserId: {playerUserId} Message: {@event.Text}");

                var originalMessage = @event.Text.Trim();
                var message = @event.Text.Trim().ToLower();

                CCSPlayerController? player = null;
                if (playerData.ContainsKey(playerUserId)) {
                    player = playerData[playerUserId];
                }

                if (player == null) {
                    // Somehow we did not had the player in playerData, hence updating the maps again before getting the player
                    UpdatePlayersMap();
                    player = playerData[playerUserId];
                }

                // Handling player commands
                if (commandActions.ContainsKey(message)) {
                    commandActions[message](player, null);
                }

                if (message.StartsWith(".map")) {
                    string command = ".map";
                    string mapName = message.Substring(command.Length).Trim();
                    HandleMapChangeCommand(player, mapName);
                }
                if (message.StartsWith(".readyrequired")) {
                    string command = ".readyrequired";
                    string commandArg = message.Substring(command.Length).Trim();

                    HandleReadyRequiredCommand(player, commandArg);
                }

                if (message.StartsWith(".restore")) {
                    string command = ".restore";
                    string commandArg = message.Substring(command.Length).Trim();

                    HandleRestoreCommand(player, commandArg);
                }
                if (originalMessage.StartsWith(".asay")) {
                    string command = ".asay";
                    string commandArg = originalMessage.Substring(command.Length).Trim();

                    if (IsPlayerAdmin(player, "css_asay", "@css/chat"))
                    {
                        if (commandArg != "") {
                            Server.PrintToChatAll($"{adminChatPrefix} {commandArg}");
                        } else {
                            ReplyToUserCommand(player, "Usage: .asay <message>");
                        }
                    } else {
                        SendPlayerNotAdminMessage(player);
                    }
                }
		if (message.StartsWith(".savenade")) {
                    string command = ".savenade";
                    string commandArg = message.Substring(command.Length).Trim();
		    HandleSaveNadeCommand(player, commandArg);
		    
                }
		if (message.StartsWith(".delnade")) {
                    string command = ".delnade";
                    string commandArg = message.Substring(command.Length).Trim();
		    HandleDeleteNadeCommand(player, commandArg);
		    
                }
		if (message.StartsWith(".importnade")) {
                    string command = ".importnade";
                    string commandArg = message.Substring(command.Length).Trim();
		    HandleImportNadeCommand(player, commandArg);
		    
                }
		if (message.StartsWith(".listnades")) {
                    string command = ".listnades";
                    string commandArg = message.Substring(command.Length).Trim();
		    HandleListNadesCommand(player, commandArg);
		    
                }
		if (message.StartsWith(".loadnade")) {
                    string command = ".loadnade";
                    string commandArg = message.Substring(command.Length).Trim();
		    HandleLoadNadeCommand(player, commandArg);
		    
                }
                if (message.StartsWith(".spawn")) {
                    string command = ".spawn";
                    string commandArg = message.Substring(command.Length).Trim();

                    HandleSpawnCommand(player, commandArg, player.TeamNum, "spawn");
                }
                if (message.StartsWith(".ctspawn")) {
                    string command = ".ctspawn";
                    string commandArg = message.Substring(command.Length).Trim();

                    HandleSpawnCommand(player, commandArg, (byte)CsTeam.CounterTerrorist, "ctspawn");
                }
                if (message.StartsWith(".tspawn")) {
                    string command = ".tspawn";
                    string commandArg = message.Substring(command.Length).Trim();

                    HandleSpawnCommand(player, commandArg, (byte)CsTeam.Terrorist, "tspawn");
                }
                if (originalMessage.StartsWith(".team1")) {
                    string command = ".team1";
                    string commandArg = originalMessage.Substring(command.Length).Trim();

                    HandleTeamNameChangeCommand(player, commandArg, 1);
                }
                if (originalMessage.StartsWith(".team2")) {
                    string command = ".team2";
                    string commandArg = originalMessage.Substring(command.Length).Trim();

                    HandleTeamNameChangeCommand(player, commandArg, 2);
                }
                if (originalMessage.StartsWith(".rcon")) {
                    string command = ".rcon";
                    string commandArg = originalMessage.Substring(command.Length).Trim();
                    if (IsPlayerAdmin(player, "css_rcon", "@css/rcon"))
                    {
                        Server.ExecuteCommand(commandArg);
                        ReplyToUserCommand(player, "Command sent successfully!");
                    } else {
                        SendPlayerNotAdminMessage(player);
                    }
                }
                if (message.StartsWith(".coach")) {
                    string command = ".coach";
                    string coachSide = message.Substring(command.Length).Trim();

                    HandleCoachCommand(player, coachSide);
                }

                return HookResult.Continue;
            });

            RegisterEventHandler<EventPlayerBlind>((@event, info) =>
            {
                if (isPractice && @event.Userid.SteamID != @event.Attacker.SteamID)
                {
                    double roundedBlindDuration = Math.Round(@event.BlindDuration, 2);
                    @event.Attacker.PrintToChat($" Flashed {@event.Userid.PlayerName}. Blind time: {roundedBlindDuration} seconds");
                }
                return HookResult.Continue;
            });
        }
    }
}
