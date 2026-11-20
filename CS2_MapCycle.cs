using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CS2_MapCycle;

public class CS2_MapCycleConfig : BasePluginConfig
{
    [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 2;
    [JsonPropertyName("PluginEnabled")] public bool PluginEnabled { get; set; } = true;

    [JsonPropertyName("EnableRandomMaps")] public bool EnableRandomMaps { get; set; } = false;
    [JsonPropertyName("EnableNoDuplicateRandomMaps")] public bool EnableNoDuplicateRandomMaps { get; set; } = true;

    [JsonPropertyName("EnableNextMapMessage")] public bool EnableNextMapMessage { get; set; } = true;

    [JsonPropertyName("ChatPrefix")] public string ChatPrefix { get; set; } = "[MapCycle]";

    [JsonPropertyName("MapCycleFile")] public string MapCycleFile { get; set; } = "mapcyclecustom.txt";
}

public class MapEntry
{
    public string IdOrName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class CS2_MapCycle : BasePlugin, IPluginConfig<CS2_MapCycleConfig>
{
    public override string ModuleName => "CS2_MapCycle";
    public override string ModuleVersion => "1.4.3";
    public override string ModuleAuthor => "ddbmaster";
    public override string ModuleDescription => "Mapcycle (mit letzte-Runde-Ansage)";

    public required CS2_MapCycleConfig Config { get; set; } = new();

    private const string DefaultMapCycleFile = "mapcyclecustom.txt";

    private string MapCycleFilePath = "";
    private List<MapEntry> MapCycleList = new();
    private List<MapEntry> MapCycleInUse = new();
    private int MapIndex = 0;
    private readonly Random Rng = new();

    private MapEntry? NextMap = null;
    private bool NextMapAnnounced = false;

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[{ModuleName}] v{ModuleVersion} wird geladen");

        if (!Config.PluginEnabled)
        {
            Console.WriteLine($"[{ModuleName}] Plugin ist deaktiviert.");
            return;
        }

        MapCycleFilePath = Path.Combine(Server.GameDirectory, "csgo", Config.MapCycleFile);

        // ✅ Auto-Erstellung nur für Default-Dateiname
        if (!File.Exists(MapCycleFilePath))
        {
            if (Config.MapCycleFile.Equals(DefaultMapCycleFile, StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllLines(MapCycleFilePath, new[]
                {
                    "// CS2_MapCycle – automatisch erstellt",
                    "// Eine Map pro Zeile (oder WorkshopID:Name)",
                    "de_dust2",
                    "de_inferno",
                    "de_mirage",
                    "de_nuke"
                });

                Console.WriteLine($"[{ModuleName}] Standard-Mapcycle erstellt: {MapCycleFilePath}");
            }
            else
            {
                Console.WriteLine($"[{ModuleName}] FEHLER: Mapcycle-Datei fehlt: {MapCycleFilePath}");
                Console.WriteLine($"[{ModuleName}] Kein Auto-Create bei benutzerdefiniertem Dateinamen!");
                return;
            }
        }

        LoadMapcycle();

        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);

        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventCsWinPanelMatch>(OnWinPanelMatch);
    }

    public void OnConfigParsed(CS2_MapCycleConfig config) => Config = config;

    // ---------- ConVar lesen (ConVar<T>, ab CounterStrikeSharp 1.0.379) ----------
    // Liest eine Zahl aus einer Server-ConVar, egal ob sie intern int, float, bool … ist.
    private static double ReadConVarNumber(string name, double fallback)
    {
        var cv = ConVar<float>.Find(name);
        if (cv == null || !cv.IsValid)
            return fallback;

        try
        {
            return cv.Type switch
            {
                ConVarType.Bool => cv.As<bool>().Value ? 1 : 0,
                ConVarType.Int16 => cv.As<short>().Value,
                ConVarType.UInt16 => cv.As<ushort>().Value,
                ConVarType.Int32 => cv.As<int>().Value,
                ConVarType.UInt32 => cv.As<uint>().Value,
                ConVarType.Int64 => cv.As<long>().Value,
                ConVarType.UInt64 => cv.As<ulong>().Value,
                ConVarType.Float32 => cv.As<float>().Value,
                ConVarType.Float64 => cv.As<double>().Value,
                _ => double.TryParse(cv.ValueAsString, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v
                    : fallback
            };
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }

    private void LoadMapcycle()
    {
        MapCycleList = File.ReadAllLines(MapCycleFilePath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("//"))
            .Select(l =>
            {
                // Nur die Map-ID/den Map-Namen kleinschreiben,
                // der Anzeigename bleibt so, wie er in der Datei steht.
                if (l.Contains(':'))
                {
                    var p = l.Split(':', 2);
                    var id = p[0].Trim().ToLowerInvariant();
                    var display = p[1].Trim();
                    return new MapEntry
                    {
                        IdOrName = id,
                        DisplayName = string.IsNullOrWhiteSpace(display) ? id : display
                    };
                }

                var name = l.ToLowerInvariant();
                return new MapEntry { IdOrName = name, DisplayName = name };
            })
            .ToList();

        MapCycleInUse = (Config.EnableRandomMaps && Config.EnableNoDuplicateRandomMaps)
            ? DistinctById(MapCycleList)
            : MapCycleList.ToList();

        Console.WriteLine($"[{ModuleName}] Maps geladen: {MapCycleList.Count}");
        foreach (var m in MapCycleList)
            Console.WriteLine($"  - {m.IdOrName} ({m.DisplayName})");

        if (MapCycleInUse.Count == 0)
            Console.WriteLine($"[{ModuleName}] WARNUNG: Keine Maps im Mapcycle!");
    }

    private static List<MapEntry> DistinctById(List<MapEntry> input)
        => input.GroupBy(m => m.IdOrName).Select(g => g.First()).ToList();

    private void OnMapStart(string mapName)
    {
        NextMapAnnounced = false;

        // NextMap pro Map einmal festlegen
        NextMap ??= PickAndConsumeNextMap();

        Console.WriteLine($"[{ModuleName}] OnMapStart: aktuelle Map = {mapName}, nächste Map = {NextMap?.IdOrName}");
    }

    private void OnMapEnd()
    {
        // nichts erzwingen
    }

    // ✅ „letzte Runde startet“ – variabel, live
    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        if (!Config.EnableNextMapMessage || NextMapAnnounced)
            return HookResult.Continue;

        NextMap ??= PickAndConsumeNextMap();
        if (NextMap == null || string.IsNullOrWhiteSpace(NextMap.IdOrName))
            return HookResult.Continue;

        // ab CounterStrikeSharp 1.0.380 intern gecacht
        var rules = Utilities.GetGameRules();
        if (rules == null)
            return HookResult.Continue;

        // mp_maxrounds LIVE lesen (kann sich ändern)
        int maxRounds = (int)ReadConVarNumber("mp_maxrounds", 0);
        if (maxRounds <= 0)
            return HookResult.Continue;

        // RoundStart: aktuelle Runde läuft schon an, TotalRoundsPlayed zählt meist beendete Runden
        int roundsPlayedIncludingCurrent = rules.TotalRoundsPlayed + 1;
        int roundsLeftAfterThisStarts = maxRounds - roundsPlayedIncludingCurrent;

        // 0 => diese Runde ist die letzte
        if (roundsLeftAfterThisStarts == 0)
        {
            PrintChat($"Nach dieser Runde kommt: {NextMap.DisplayName}");
            NextMapAnnounced = true;
        }

        return HookResult.Continue;
    }

    private HookResult OnWinPanelMatch(EventCsWinPanelMatch ev, GameEventInfo info)
    {
        NextMap ??= PickAndConsumeNextMap();

        if (NextMap == null || string.IsNullOrWhiteSpace(NextMap.IdOrName))
        {
            Console.WriteLine($"[{ModuleName}] FEHLER: NextMap ist leer – kein Mapwechsel.");
            return HookResult.Continue;
        }

        float winPanel = (float)ReadConVarNumber("mp_win_panel_display_time", 1);
        int restart = (int)ReadConVarNumber("mp_match_restart_delay", 1);
        float delay = Math.Max(0, Math.Max(winPanel, restart) - 5);

        if (Config.EnableNextMapMessage)
            PrintChat($"Nächste Map: {NextMap.DisplayName}");

        var mapToGo = NextMap;
        AddTimer(delay, () =>
        {
            Console.WriteLine($"[{ModuleName}] Wechsel zu {mapToGo.IdOrName}");

            if (ulong.TryParse(mapToGo.IdOrName, out _))
                Server.ExecuteCommand($"host_workshop_map {mapToGo.IdOrName}");
            else
                Server.ExecuteCommand($"map {mapToGo.IdOrName}");
        });

        // für die nächste Map neu setzen
        NextMap = null;
        NextMapAnnounced = false;

        return HookResult.Continue;
    }

    // Chat-Ausgabe mit optionalem Präfix aus der Config
    private void PrintChat(string message)
    {
        var prefix = Config.ChatPrefix?.Trim();
        Server.PrintToChatAll(string.IsNullOrEmpty(prefix) ? message : $"{prefix} {message}");
    }

    // Konsumiert State (keine Doppelmaps)
    private MapEntry? PickAndConsumeNextMap()
    {
        if (MapCycleList.Count == 0)
            return null;

        if (MapCycleInUse.Count == 0)
        {
            MapCycleInUse = (Config.EnableRandomMaps && Config.EnableNoDuplicateRandomMaps)
                ? DistinctById(MapCycleList)
                : MapCycleList.ToList();
        }

        if (MapCycleInUse.Count == 0)
            return null;

        MapEntry next;

        if (Config.EnableRandomMaps)
        {
            int idx = Rng.Next(MapCycleInUse.Count);
            next = MapCycleInUse[idx];

            if (Config.EnableNoDuplicateRandomMaps)
                MapCycleInUse.RemoveAt(idx);

            if (MapCycleInUse.Count == 0)
            {
                MapCycleInUse = Config.EnableNoDuplicateRandomMaps
                    ? DistinctById(MapCycleList)
                    : MapCycleList.ToList();
            }
        }
        else
        {
            next = MapCycleInUse[Math.Clamp(MapIndex, 0, MapCycleInUse.Count - 1)];
            MapIndex = (MapIndex + 1) % MapCycleInUse.Count;
        }

        return next;
    }
}
