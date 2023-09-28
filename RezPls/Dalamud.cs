﻿using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace RezPls;

public class Dalamud
{
    public static void Initialize(DalamudPluginInterface pluginInterface)
        => pluginInterface.Create<Dalamud>();

        // @formatter:off
        [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ICommandManager        Commands        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ISigScanner            SigScanner      { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IDataManager           GameData        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IClientState           ClientState     { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IFramework             Framework       { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IGameGui               GameGui         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ITargetManager         Targets         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IObjectTable           Objects         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IPluginLog             Log             { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ITextureProvider       Textures        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static IGameInteropProvider   Interop         { get; private set; } = null!;
    // @formatter:on
}
