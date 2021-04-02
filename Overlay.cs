﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;

namespace RezzPls
{
    public class Overlay : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly HudManager             _hudManager;
        private readonly RezzPlsConfig          _config;
        private readonly ActorWatcher           _actorWatcher;

        private IReadOnlyDictionary<uint, uint> Rezzes
            => _actorWatcher.RezzList;

        private IReadOnlyDictionary<uint, string> Names
            => _actorWatcher.ActorNames;

        private IReadOnlyDictionary<uint, Position3> Positions
            => _actorWatcher.ActorPositions;

        private (uint, uint) PlayerRezz
            => _actorWatcher.PlayerRezz;

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

        private readonly        ImGuiScene.TextureWrap? _rezzIcon;
        private static readonly Vector4                 BlackColor = new(0, 0, 0, 1);
        private static readonly Vector4                 WhiteColor = new(1, 1, 1, 1);

        private static ImGuiScene.TextureWrap? BuildRezzIcon(DalamudPluginInterface pi)
        {
            const int rezzIconId = 10406;

            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RezzPls.RezzIcon");
            if (resource != null)
            {
                using MemoryStream ms = new();
                resource.CopyTo(ms);
                var bytes = ms.ToArray();
                var wrap  = pi.UiBuilder.LoadImageRaw(bytes, 48, 64, 4);
                if (wrap != null)
                    return wrap;
            }

            var texFile = pi.Data.GetIcon(rezzIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        public Overlay(DalamudPluginInterface pluginInterface, ActorWatcher actorWatcher, RezzPlsConfig config)
        {
            _pluginInterface = pluginInterface;
            _actorWatcher    = actorWatcher;
            _hudManager      = new HudManager(_pluginInterface.TargetModuleScanner);
            _rezzIcon        = BuildRezzIcon(_pluginInterface);
            _config          = config;
        }

        public void Dispose()
        {
            Disable();
            _hudManager.Dispose();
            _rezzIcon?.Dispose();
        }

        public void DrawInWorld(SharpDX.Vector2 pos, string text)
        {
            if (_rezzIcon == null)
                return;

            if (_config.ShowIcon)
            {
                Vector2 scaledIconSize = new(_rezzIcon.Width * _config.IconScale, _rezzIcon.Height * _config.IconScale);
                ImGui.SetCursorPos(new Vector2(pos.X - scaledIconSize.X / 2f, pos.Y - scaledIconSize.Y));
                ImGui.Image(_rezzIcon.ImGuiHandle, scaledIconSize);
            }

            if (_config.ShowInWorldText)
            {
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2(pos.X, pos.Y) - textSize / 2);
                ImGui.Button(text);
            }
        }

        private static unsafe string TextNodeToString(AtkTextNode* node)
            => Marshal.PtrToStringAnsi(new IntPtr(node->NodeText.StringPtr))!;

        public ImDrawListPtr? BeginRezzRects()
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
              | ImGuiWindowFlags.NoSavedSettings
              | ImGuiWindowFlags.NoMove
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoBackground
              | ImGuiWindowFlags.NoNav;
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
            return !ImGui.Begin("##rezzRects", flags) ? null : ImGui.GetWindowDrawList();
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

        private static unsafe void DrawPartyRect(ImDrawListPtr drawPtr, AtkUnitBase* partyList, int idx, uint color, bool names,
            string caster = "")
        {
            idx = 17 - idx;
            var nodePtr  = (AtkComponentNode*) partyList->ULDData.NodeList[idx];
            var colNode  = nodePtr->Component->ULDData.NodeList[2];
            var rectMin  = GetNodePosition(colNode) + GreenBoxPositionOffset * partyList->Scale;
            var rectSize = (new Vector2(colNode->Width, colNode->Height) - GreenBoxSizeOffset) * partyList->Scale;
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, color, GreenBoxEdgeRounding * partyList->Scale);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor, 2);
        }

        private static unsafe void DrawAllianceRect(ImDrawListPtr drawPtr, AtkUnitBase* allianceList, int idx, uint color, bool names,
            string caster = "")
        {
            idx = 9 - idx;
            var nodePtr  = allianceList->ULDData.NodeList[idx];
            var comp     = ((AtkComponentNode*) nodePtr)->Component;
            var gridNode = comp->ULDData.NodeList[2]->ChildNode;
            var rectMin  = GetNodePosition(gridNode) + new Vector2(5 * allianceList->Scale, 5 * allianceList->Scale);
            var rectSize = new Vector2(gridNode->Width * allianceList->Scale, gridNode->Height * allianceList->Scale)
              - new Vector2(8 * allianceList->Scale,                          10 * allianceList->Scale);
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, color, 10 * allianceList->Scale);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor);
        }


        private string GetActorName(uint corpse, uint caster)
        {
            if (corpse == caster)
                return "LIMIT BREAK";
            if (caster == 0)
                return string.Empty;

            return Names.TryGetValue(caster, out var name) ? name : "Unknown";
        }

        private Position3? GetActorPosition(uint corpse)
            => Positions.TryGetValue(corpse, out var pos) ? pos : null;

        public unsafe void DrawOnPartyFrames(ImDrawListPtr drawPtr)
        {
            var party     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_PartyList", 1);
            var drawParty = _config.ShowGroupFrame && party != null && party->IsVisible;

            var alliance1     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList1", 1);
            var drawAlliance1 = _config.ShowAllianceFrame && alliance1 != null && alliance1->IsVisible;

            var alliance2     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList2", 1);
            var drawAlliance2 = _config.ShowAllianceFrame && alliance2 != null && alliance2->IsVisible;

            var anyParty = drawParty || drawAlliance1 || drawAlliance2;

            void DrawRect(uint corpse, uint caster)
            {
                var name = GetActorName(corpse, caster);
                if (anyParty)
                {
                    var group = _hudManager.FindGroupMemberById(corpse);
                    if (group != null)
                    {
                        uint color;
                        if (caster == 0)
                            color = PlayerRezz.Item1 != corpse ? _config.RaisedColor : _config.DoubleRaiseColor;
                        else if (corpse == PlayerRezz.Item1 && caster != PlayerRezz.Item2)
                            color = _config.DoubleRaiseColor;
                        else
                            color = _config.CurrentlyRaisingColor;

                        switch (group.Value.groupIdx)
                        {
                            case 0:
                                if (drawParty)
                                    DrawPartyRect(drawPtr, party, group.Value.idx, color, _config.ShowCasterNames, name);
                                break;
                            case 1:
                                if (drawAlliance1)
                                    DrawAllianceRect(drawPtr, alliance1, group.Value.idx, color, _config.ShowCasterNames, name);
                                break;
                            case 2:
                                if (drawAlliance2)
                                    DrawAllianceRect(drawPtr, alliance2, group.Value.idx, color, _config.ShowCasterNames, name);
                                break;
                        }
                    }
                }

                var pos = GetActorPosition(corpse);
                if (pos != null && _pluginInterface.Framework.Gui.WorldToScreen(pos.Value, out var screenPos))
                    DrawInWorld(screenPos, caster == 0 ? "Already Raised" : $"Raise: {name}");
            }

            foreach (var kvp in Rezzes)
                DrawRect(kvp.Key, kvp.Value);
        }

        public void Draw()
        {
            if (Rezzes.Count == 0)
                return;

            try
            {
                var drawPtrOpt = BeginRezzRects();
                if (drawPtrOpt == null)
                    return;

                var drawPtr = drawPtrOpt.Value;

                ImGui.PushStyleColor(ImGuiCol.Button, _config.InWorldBackgroundColor);

                DrawOnPartyFrames(drawPtr);

                ImGui.PopStyleColor();
                ImGui.End();
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "");
            }
        }
    }
}