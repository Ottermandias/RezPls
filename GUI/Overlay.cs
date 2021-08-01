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
using Lumina.Data.Files;
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
        private readonly        ImGuiScene.TextureWrap? _dispelIcon;
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

            var hd = LoadIconHq(pi, raiseIconId);
            if (hd != null)
                return pi.UiBuilder.LoadImageRaw(hd.GetRgbaImageData(), hd.Header.Width, hd.Header.Height, 4);

            var texFile = pi.Data.GetIcon(raiseIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        private static TexFile? LoadIconHq(DalamudPluginInterface pi, int id)
        {
            var path = $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}_hr1.tex";
            return pi.Data.GetFile<TexFile>(path);
        }

        private static readonly Vector2 DefaultIconSize = new(48, 64);

        private static ImGuiScene.TextureWrap? BuildDispelIcon(DalamudPluginInterface pi)
        {
            const int dispelIconId = 15019;

            var hd = LoadIconHq(pi, dispelIconId);
            if (hd != null)
                return pi.UiBuilder.LoadImageRaw(hd.GetRgbaImageData(), hd.Header.Width, hd.Header.Height, 4);

            var texFile = pi.Data.GetIcon(dispelIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        public Overlay(DalamudPluginInterface pluginInterface, ActorWatcher actorWatcher, RezPlsConfig config)
        {
            _pluginInterface = pluginInterface;
            _actorWatcher    = actorWatcher;
            _hudManager      = new HudManager(_pluginInterface.TargetModuleScanner);
            _raiseIcon       = BuildRaiseIcon(_pluginInterface);
            _dispelIcon       = BuildDispelIcon(_pluginInterface);
            _config          = config;
        }

        public void Dispose()
        {
            Disable();
            _hudManager.Dispose();
            _raiseIcon?.Dispose();
            _dispelIcon?.Dispose();
        }

        private (CastType, string, bool, bool) GetText(string name, ActorState state)
        {
            if (state.Caster == 0)
            {
                if (state.HasStatus)
                    return _drawDispels ? (CastType.Dispel, "Needs Dispel", _config.ShowIcon, _config.ShowInWorldText) : (CastType.None, "", false, false);
                return _drawRaises ? (CastType.Raise, "Already Raised", _config.ShowIcon, _config.ShowInWorldText) : (CastType.None, "", false, false);
            }

            return state.Type == CastType.Raise && _drawRaises ? (CastType.Raise, $"Raise: {name}", _config.ShowIcon, _config.ShowInWorldText) : (CastType.None, "", false, false);
        }

        public void DrawInWorld(SharpDX.Vector2 pos, string name, ActorState state)
        {
            var (type, text, drawIcon, drawText) = GetText(name, state);
            if (type == CastType.None)
                return;

            if (drawIcon)
            {
                var icon = type == CastType.Dispel ? _dispelIcon : _raiseIcon;
                if (icon == null)
                    return;

                var scaledIconSize = DefaultIconSize * _config.IconScale * ImGui.GetIO().FontGlobalScale;

                ImGui.SetCursorPos(new Vector2(pos.X - scaledIconSize.X / 2f, pos.Y - scaledIconSize.Y) - ImGui.GetMainViewport().Pos);
                ImGui.Image(icon.ImGuiHandle, scaledIconSize);
            }

            if (drawText)
            {
                var color    = type == CastType.Dispel ? _config.InWorldBackgroundColorDispel : _config.InWorldBackgroundColor;
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2(pos.X, pos.Y) - new Vector2(textSize.X / 2 + ImGui.GetStyle().FramePadding.X, 0) - ImGui.GetMainViewport().Pos);
                using var imgui = new ImGuiRaii()
                    .PushColor(ImGuiCol.Button, color);
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
            if (_drawDispels)
            {
                if (state.HasStatus)
                {
                    if (state.Type == CastType.Dispel && state.Caster != 0)
                    {
                        if (PlayerRez.Item1 == corpse && state.Caster != PlayerRez.Item2.Caster)
                            return _config.DoubleRaiseColor;

                        return _config.CurrentlyDispelColor;
                    }
                    return _config.DispellableColor;
                }

                if (state.Type == CastType.Dispel)
                {
                    return corpse == PlayerRez.Item1 ? _config.DoubleRaiseColor : 0;
                }
            }

            if (_drawRaises)
            {
                if (state.Caster == 0)
                    return PlayerRez.Item1 != corpse ? _config.RaisedColor : _config.DoubleRaiseColor;
                if (corpse == PlayerRez.Item1 && state.Caster != PlayerRez.Item2.Caster)
                    return _config.DoubleRaiseColor;

                return _config.CurrentlyRaisingColor;
            }

            return 0;
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
                        if (color != 0)
                        {


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
                }

                if (corpse == state.Caster)
                    return;

                var pos = GetActorPosition(corpse);
                if (pos != null && _pluginInterface.Framework.Gui.WorldToScreen(pos.Value, out var screenPos))
                {
                    DrawInWorld(screenPos, name, state);
                }
            }

            foreach (var kvp in Resurrections)
                DrawWhichRect(kvp.Key, kvp.Value);
        }

        public void Draw()
        {
            if (Resurrections.Count == 0)
                return;

            _drawRaises  = _config.EnabledRaise;
            _drawDispels = _config.EnabledDispel;
            if (_config.RestrictedJobs || _config.RestrictedJobsDispel)
            {
                var (job, level) = _actorWatcher.CurrentPlayerJob();

                if (!job.CanRaise() || job == Job.RDM && level < 64)
                    _drawRaises &= !_config.RestrictedJobs;
                if (!job.CanDispel() || job == Job.BRD && level < 35)
                    _drawDispels &= !_config.RestrictedJobsDispel;
            }

            var drawPtrOpt = BeginRezRects();
            if (drawPtrOpt == null)
                return;

            try
            {
                var drawPtr = drawPtrOpt.Value;
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
