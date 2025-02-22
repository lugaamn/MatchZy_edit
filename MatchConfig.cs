using Newtonsoft.Json.Linq;


namespace MatchZy
{

    public class MatchConfig
    {
        public List<string> Maplist { get; set; } = new List<string>();
        public long MatchId { get; set; }
        public int NumMaps { get; set; } = 1;
        public int PlayersPerTeam { get; set; } = 5;
        public int MinPlayersToReady { get; set; } = 12;
        public int CurrentMapNumber = 0;
        public List<string> MapSides { get; set; } = new List<string>();

        public bool SeriesCanClinch { get; set; } = true;
        public bool Scrim { get; set; } = false;

        public Dictionary<string, string> ChangedCvars = new();

        public Dictionary<string, string> OriginalCvars = new();
        public JToken? Spectators;
    }
}
