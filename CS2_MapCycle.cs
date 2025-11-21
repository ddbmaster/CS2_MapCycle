using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2_MapCycle;

public class CS2_MapCycleConfig : BasePluginConfig
{
    [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 1;
    [JsonPropertyName("PluginEnabled")] public bool PluginEnabled { get; set; } = true;
    [JsonPropertyName("Language")] public string Language { get; set; } = "en";
    [JsonPropertyName("EnableRandomMaps")] public bool EnableRandomMaps { get; set; } = false;
    [JsonPropertyName("EnableNoDuplicateRandomMaps")] public bool EnableNoDuplicateRandomMaps { get; set; } = true;
    [JsonPropertyName("EnableNextMapMessage")] public bool EnableNextMapMessage { get; set; } = true;
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
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor  => "ddbmaster";
    public override string ModuleDescription => "Mapcycle-Plugin";   // ✅ hier angepasst

    public required CS2_MapCycleConfig Config { get; set; } = new();

    private string MapCycleFile = "";
    private List<MapEntry> MapCycleList = new();
    private List<MapEntry> MapCycleInUseList = new();
    private int MapCycleIdx = 0;

    private Dictionary<string, string> Lang = new();

    public override void Load(bool hotReload)
    {
        Console.WriteLine("------------------------------------------------------------------");
        Console.WriteLine($"Plugin: {ModuleName} - {ModuleDescription} - Version: {ModuleVersion} by {ModuleAuthor}");

        LoadLanguage();

        if (!Config.PluginEnabled)
        {
            Console.WriteLine(GetText("PluginDisabled"));
            return;
        }

        MapCycleFile = Path.Combine(Server.GameDirectory, "csgo", Config.MapCycleFile);
        if (!File.Exists(MapCycleFile))
        {
            Console.WriteLine($"WARNING: File not found = {MapCycleFile}\n{ModuleName} will be disabled.");
            return;
        }

        // Mapcycle einlesen
        MapCycleList = File.ReadAllLines(MapCycleFile)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList()
            .ConvertAll(d =>
            {
                d = d.ToLower();
                if (d.StartsWith("//"))
                    return new MapEntry { IdOrName = "", DisplayName = "" };

                if (d.Contains(":"))
                {
                    var parts = d.Split(':', 2);
                    return new MapEntry { IdOrName = parts[0], DisplayName = parts[1] };
                }
                else
                {
                    return new MapEntry { IdOrName = d, DisplayName = d };
                }
            });

        Console.WriteLine("---------------------------------");
        Console.WriteLine($"Language = {Config.Language}");
        Console.WriteLine($"EnableRandomMaps = {Config.EnableRandomMaps}");
        Console.WriteLine($"EnableNoDuplicateRandomMaps = {Config.EnableNoDuplicateRandomMaps}");
        Console.WriteLine($"EnableNextMapMessage = {Config.EnableNextMapMessage}");
        Console.WriteLine($"MapCycleFile = {Config.MapCycleFile}");
        Console.WriteLine("---------------------------------");
        foreach (var map in MapCycleList)
        {
            if (!string.IsNullOrEmpty(map.IdOrName))
                Console.WriteLine($"{map.IdOrName} (Display: {map.DisplayName})");
        }
        Console.WriteLine("------------------------------------------------------------------");

        MapCycleList.RemoveAll(s => string.IsNullOrEmpty(s.IdOrName));

        MapCycleInUseList = (Config.EnableRandomMaps && Config.EnableNoDuplicateRandomMaps)
            ? MapCycleList.Distinct().ToList()
            : MapCycleList.ToList();

        if (MapCycleInUseList.Count == 0)
        {
            Console.WriteLine($"WARNING: No maps defined in {MapCycleFile}\n{ModuleName} will be disabled.");
            return;
        }

        RegisterEventHandler<EventCsWinPanelMatch>(EventCsWinPanelMatchHandler);
    }

    public void OnConfigParsed(CS2_MapCycleConfig config)
    {
        try
        {
            if (config.Version < Config.Version)
            {
                int newVersion = Config.Version;
                Config = config;
                Config.Version = newVersion;
                Config.Update();
            }
            else
            {
                Config = config;
            }
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            var line = frame?.GetFileLineNumber() ?? 0;
            Console.WriteLine($"!EXCEPTION - {ModuleName} - OnConfigParsed()\nLine {line}\n{e.Message}", true);
        }
    }

    // ===== Mehrsprach-System =====
    private void LoadLanguage()
    {
        try
        {
            string pluginFolder = Path.Combine(Server.GameDirectory,
                "csgo", "addons", "counterstrikesharp", "plugins", "CS2_MapCycle");
            string langDir = Path.Combine(pluginFolder, "lang");
            string langFile = Path.Combine(langDir, $"{Config.Language}.json");

            if (!File.Exists(langFile))
            {
                Console.WriteLine($"Language file not found: {langFile}, falling back to English.");
                langFile = Path.Combine(langDir, "en.json");
            }

            Lang = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langFile))
                   ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load language: {ex.Message}");
            Lang = new Dictionary<string, string>();
        }
    }

    private string GetText(string key, string mapName = "")
    {
        if (Lang.TryGetValue(key, out var text))
            return text.Replace("{map}", mapName);
        return key;
    }

    // ===== Event-Handler für Mapwechsel =====
    public HookResult EventCsWinPanelMatchHandler(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        MapEntry nextEntry;
        int idx;

        float winPanelDelay;
        int matchRestartDelay;
        float delay;

        var mp_win_panel_display_time = ConVar.Find("mp_win_panel_display_time");
        winPanelDelay = mp_win_panel_display_time?.GetPrimitiveValue<float>() ?? 1;

        var mp_match_restart_delay = ConVar.Find("mp_match_restart_delay");
        matchRestartDelay = mp_match_restart_delay?.GetPrimitiveValue<int>() ?? 1;

        delay = Math.Max(winPanelDelay, matchRestartDelay);
        delay = Math.Max(0, delay - 5);

        if (Config.EnableRandomMaps)
        {
            idx = new Random().Next(0, MapCycleInUseList.Count);
            nextEntry = MapCycleInUseList[idx];

            if (Config.EnableNoDuplicateRandomMaps)
                MapCycleInUseList.RemoveAt(idx);

            if (MapCycleInUseList.Count == 0)
                MapCycleInUseList = Config.EnableNoDuplicateRandomMaps
                    ? MapCycleList.Distinct().ToList()
                    : MapCycleList.ToList();
        }
        else
        {
            nextEntry = MapCycleInUseList[MapCycleIdx];
            if (++MapCycleIdx >= MapCycleInUseList.Count)
                MapCycleIdx = 0;
        }

        if (!string.IsNullOrEmpty(nextEntry.IdOrName))
        {
            if (Config.EnableNextMapMessage)
                Server.PrintToChatAll(GetText("NextMapMessage", nextEntry.DisplayName));

            AddTimer(delay, () =>
            {
                Console.WriteLine(GetText("ChangingMap", nextEntry.DisplayName));
                if (ulong.TryParse(nextEntry.IdOrName, out _))
                    Server.ExecuteCommand($"host_workshop_map {nextEntry.IdOrName}");
                else
                    Server.ExecuteCommand($"map {nextEntry.IdOrName}");
            });
        }
        else
        {
            Console.WriteLine($"ERROR: {ModuleName} - Blank map entry.");
        }

        return HookResult.Continue;
    }
}
