using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Reflection;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Interface;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using RezPls.Enums;
using RezPls.Managers;

namespace RezPls.GUI
{
    public class Overlay : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly HudManager             _hudManager;
        private readonly RezPlsConfig           _config;
        private readonly ActorWatcher           _actorWatcher;

        private IReadOnlyDictionary<uint, ActorState> Resurrections
            => _actorWatcher.RezList;

        private IReadOnlyDictionary<uint, string> Names
            => _actorWatcher.ActorNames;

        private IReadOnlyDictionary<uint, Position3> Positions
            => _actorWatcher.ActorPositions;

        private (uint, ActorState) PlayerRez
            => _actorWatcher.PlayerRez;

        private bool _enabled = false;

        public void Enable()
        {
            if (_enabled)
                return;

            _enabled                             =  true;
            _pluginInterface.UiBuilder.OnBuildUi += Draw;
        }

        public void Disable()
        {
            if (!_enabled)
                return;

            _enabled                             =  false;
            _pluginInterface.UiBuilder.OnBuildUi -= Draw;
        }

        private readonly        ImGuiScene.TextureWrap? _raiseIcon;
        private readonly        ImGuiScene.TextureWrap? _esunaIcon;
        private static readonly Vector4                 BlackColor = new(0, 0, 0, 1);
        private static readonly Vector4                 WhiteColor = new(1, 1, 1, 1);

        private bool _drawRaises  = true;
        private bool _drawDispels = true;

        private static ImGuiScene.TextureWrap? BuildRaiseIcon(DalamudPluginInterface pi)
        {
            const int raiseIconId = 10406;

            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RezPls.RaiseIcon");
            if (resource != null)
            {
                using MemoryStream ms = new();
                resource.CopyTo(ms);
                var bytes = ms.ToArray();
                var wrap  = pi.UiBuilder.LoadImageRaw(bytes, 48, 64, 4);
                if (wrap != null)
                    return wrap;
            }

            var texFile = pi.Data.GetIcon(raiseIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        private static ImGuiScene.TextureWrap? BuildEsunaIcon(DalamudPluginInterface pi)
        {
            const int esunaIconId = 884;

            var texFile = pi.Data.GetIcon(esunaIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        public Overlay(DalamudPluginInterface pluginInterface, ActorWatcher actorWatcher, RezPlsConfig config)
        {
            _pluginInterface = pluginInterface;
            _actorWatcher    = actorWatcher;
            _hudManager      = new HudManager(_pluginInterface.TargetModuleScanner);
            _raiseIcon       = BuildRaiseIcon(_pluginInterface);
            _esunaIcon       = BuildEsunaIcon(_pluginInterface);
            _config          = config;
        }

        public void Dispose()
        {
            Disable();
            _hudManager.Dispose();
            _raiseIcon?.Dispose();
            _esunaIcon?.Dispose();
        }

        public void DrawInWorld(SharpDX.Vector2 pos, string text, ImGuiScene.TextureWrap? icon)
        {
            if (_config.ShowIcon)
            {
                if (icon == null)
                    return;

                var scaledIconSize = new Vector2(icon.Width, icon.Height) * _config.IconScale * ImGui.GetIO().FontGlobalScale;

                ImGui.SetCursorPos(new Vector2(pos.X - scaledIconSize.X / 2f, pos.Y - scaledIconSize.Y) - ImGui.GetMainViewport().Pos);
                ImGui.Image(icon.ImGuiHandle, scaledIconSize);
            }

            if (_config.ShowInWorldText)
            {
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2(pos.X, pos.Y) - textSize / 2 - ImGui.GetMainViewport().Pos);
                ImGui.Button(text);
            }
        }

        public ImDrawListPtr? BeginRezRects()
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
              | ImGuiWindowFlags.NoSavedSettings
              | ImGuiWindowFlags.NoMove
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoBackground
              | ImGuiWindowFlags.NoNav;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
            ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
            return !ImGui.Begin("##rezRects", flags) ? null : ImGui.GetWindowDrawList();
        }

        private static unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X,      par->Y);
                par =  par->ParentNode;
            }

            return pos;
        }

        private static void TextShadowed(string text, Vector4 foregroundColor, Vector4 shadowColor, byte shadowWidth = 1)
        {
            var x = ImGui.GetCursorPosX();
            var y = ImGui.GetCursorPosY();

            for (var i = -shadowWidth; i < shadowWidth; i++)
            {
                for (var j = -shadowWidth; j < shadowWidth; j++)
                {
                    if (i == 0 && j == 0)
                        continue;

                    ImGui.SetCursorPosX(x + i);
                    ImGui.SetCursorPosY(y + j);
                    ImGui.TextColored(shadowColor, text);
                }
            }

            ImGui.SetCursorPosX(x);
            ImGui.SetCursorPosY(y);
            ImGui.TextColored(foregroundColor, text);
        }

        private static readonly Vector2 GreenBoxPositionOffset = new(5, 5);
        private static readonly Vector2 GreenBoxSizeOffset     = new(8, 10);
        private const           float   GreenBoxEdgeRounding   = 10;

        private static void DrawRect(ImDrawListPtr drawPtr, Vector2 rectMin, Vector2 rectMax, uint color, float rounding, RectType type)
        {
            rectMin += ImGui.GetMainViewport().Pos;
            rectMax += ImGui.GetMainViewport().Pos;
            switch (type)
            {
                case RectType.Fill:
                    drawPtr.AddRectFilled(rectMin, rectMax, color, rounding);
                    break;
                case RectType.OnlyOutline:
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                case RectType.OnlyFullAlphaOutline:
                    color |= 0xFF000000;
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                case RectType.FillAndFullAlphaOutline:
                    drawPtr.AddRectFilled(rectMin, rectMax, color, rounding);
                    color |= 0xFF000000;
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                default: throw new InvalidEnumArgumentException();
            }
        }

        private static unsafe void DrawPartyRect(ImDrawListPtr drawPtr, AtkUnitBase* partyList, int idx, uint color, RectType type, bool names,
            string caster = "")
        {
            idx = 17 - idx;
            var nodePtr  = (AtkComponentNode*) partyList->UldManager.NodeList[idx];
            var colNode  = nodePtr->Component->UldManager.NodeList[2];
            var rectMin  = GetNodePosition(colNode) + GreenBoxPositionOffset * partyList->Scale;
            var rectSize = (new Vector2(colNode->Width, colNode->Height) - GreenBoxSizeOffset) * partyList->Scale;

            DrawRect(drawPtr, rectMin, rectMin + rectSize, color, GreenBoxEdgeRounding * partyList->Scale, type);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor, 2);
        }

        private static unsafe void DrawAllianceRect(ImDrawListPtr drawPtr, AtkUnitBase* allianceList, int idx, uint color, RectType type,
            bool names,
            string caster = "")
        {
            idx = 9 - idx;
            var nodePtr  = allianceList->UldManager.NodeList[idx];
            var comp     = ((AtkComponentNode*) nodePtr)->Component;
            var gridNode = comp->UldManager.NodeList[2]->ChildNode;
            var rectMin  = GetNodePosition(gridNode) + GreenBoxPositionOffset * allianceList->Scale;
            var rectSize = (new Vector2(gridNode->Width, gridNode->Height) - GreenBoxSizeOffset) * allianceList->Scale;

            DrawRect(drawPtr, rectMin, rectMin + rectSize, color, GreenBoxEdgeRounding * allianceList->Scale, type);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor);
        }


        private string GetActorName(CastType type, uint corpse, uint caster)
        {
            if (type == CastType.Dispel)
                return Names.TryGetValue(caster, out var name) ? name : "Unknown";
            if (corpse == caster)
                return "LIMIT BREAK";
            if (caster == 0)
                return string.Empty;

            return Names.TryGetValue(caster, out var name2) ? name2 : "Unknown";
        }

        private Position3? GetActorPosition(uint corpse)
            => Positions.TryGetValue(corpse, out var pos) ? pos : null;

        private uint GetColor(uint corpse, ActorState state)
        {
            if (state.Type == CastType.Dispel && state.Caster != 0)
                return PlayerRez.Item1 != corpse || PlayerRez.Item1 == PlayerRez.Item2.Caster
                    ? _config.CurrentlyDispelColor
                    : _config.DoubleRaiseColor;

            if (state.HasStatus)
                return _config.DispellableColor;

            if (state.Caster == 0)
                return PlayerRez.Item1 != corpse ? _config.RaisedColor : _config.DoubleRaiseColor;
            if (corpse == PlayerRez.Item1 && state.Caster != PlayerRez.Item2.Caster)
                return _config.DoubleRaiseColor;

            return _config.CurrentlyRaisingColor;
        }

        private string GetText(string name, ActorState state)
        {
            if (state.Caster == 0)
                return "Already Raised";

            return state.Type == CastType.Raise ? $"Raise: {name}" : $"Dispel: {name}";
        }

        public unsafe void DrawOnPartyFrames(ImDrawListPtr drawPtr)
        {
            if (!_drawRaises && !_drawDispels)
                return;

            var party     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_PartyList", 1);
            var drawParty = _config.ShowGroupFrame && party != null && party->IsVisible;

            var alliance1     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList1", 1);
            var drawAlliance1 = _config.ShowAllianceFrame && alliance1 != null && alliance1->IsVisible;

            var alliance2     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList2", 1);
            var drawAlliance2 = _config.ShowAllianceFrame && alliance2 != null && alliance2->IsVisible;

            var anyParty = drawParty || drawAlliance1 || drawAlliance2;

            void DrawWhichRect(uint corpse, ActorState state)
            {
                if (!_drawRaises && state.Type == CastType.Raise && !state.HasStatus)
                    return;
                if (!_drawDispels && state.Type != CastType.Raise)
                    return;

                var name = GetActorName(state.Type, corpse, state.Caster);
                if (anyParty)
                {
                    var group = _hudManager.FindGroupMemberById(corpse);
                    if (group != null)
                    {
                        var color = GetColor(corpse, state);

                        switch (group.Value.groupIdx)
                        {
                            case 0:
                                if (drawParty)
                                    DrawPartyRect(drawPtr, party, group.Value.idx, color, _config.RectType, _config.ShowCasterNames, name);
                                break;
                            case 1:
                                if (drawAlliance1)
                                    DrawAllianceRect(drawPtr, alliance1, group.Value.idx, color, _config.RectType, _config.ShowCasterNames,
                                        name);
                                break;
                            case 2:
                                if (drawAlliance2)
                                    DrawAllianceRect(drawPtr, alliance2, group.Value.idx, color, _config.RectType, _config.ShowCasterNames,
                                        name);
                                break;
                        }
                    }
                }

                if (corpse == state.Caster)
                    return;

                var pos = GetActorPosition(corpse);
                if (pos != null && _pluginInterface.Framework.Gui.WorldToScreen(pos.Value, out var screenPos))
                    DrawInWorld(screenPos, GetText(name, state), state.Type == CastType.Dispel ? _esunaIcon : _raiseIcon);
            }

            foreach (var kvp in Resurrections)
                DrawWhichRect(kvp.Key, kvp.Value);
        }

        public void Draw()
        {
            if (Resurrections.Count == 0)
                return;

            _drawRaises  = true;
            _drawDispels = true;
            if (_config.RestrictedJobs || _config.RestrictedJobsDispel)
            {
                var (job, level) = _actorWatcher.CurrentPlayerJob();

                if (!job.CanRaise() || job == Job.RDM && level < 64)
                    _drawRaises = !_config.RestrictedJobs;
                if (!job.CanDispel() || job == Job.BRD && level < 35)
                    _drawDispels = !_config.RestrictedJobsDispel;
            }

            var drawPtrOpt = BeginRezRects();
            if (drawPtrOpt == null)
                return;

            try
            {
                var drawPtr = drawPtrOpt.Value;

                using var color = new ImGuiRaii()
                    .PushColor(ImGuiCol.Button, _config.InWorldBackgroundColor);

                DrawOnPartyFrames(drawPtr);
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "");
            }
            finally
            {
                ImGui.End();
            }
        }
    }
}
