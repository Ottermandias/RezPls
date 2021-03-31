using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;

namespace RezzPls
{
    public class Interface : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly HudManager             _hudManager;

        private readonly IReadOnlyDictionary<int, (Actor? caster, Actor? target)> _rezzes;
        private readonly IReadOnlyDictionary<int, Actor>                          _rezzed;

        public bool Visible = true;

        private readonly        ImGuiScene.TextureWrap? _rezzIcon;
        private static readonly Vector4                 BlackColor = new(0, 0, 0, 1);
        private static readonly Vector4                 WhiteColor = new(1, 1, 1, 1);

        private static ImGuiScene.TextureWrap? BuildRezzIcon(DalamudPluginInterface pi)
        {
            const int rezzIconId = 10406;

            var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RezzPls.RezzIcon");
            if (resource != null)
            {
                using MemoryStream ms = new MemoryStream();
                resource.CopyTo(ms);
                var bytes = ms.ToArray();
                var wrap = pi.UiBuilder.LoadImageRaw(bytes, 48, 64, 4);
                if (wrap != null)
                    return wrap;
            }

            var texFile = pi.Data.GetIcon(rezzIconId);
            return pi.UiBuilder.LoadImageRaw(texFile.GetRgbaImageData(), texFile.Header.Width, texFile.Header.Height, 4);
        }

        public Interface(DalamudPluginInterface pluginInterface, ActorWatcher actorWatcher)
        {
            _pluginInterface                     =  pluginInterface;
            _rezzes                              =  actorWatcher.RezzList;
            _rezzed                              =  actorWatcher.RezzedList;
            _pluginInterface.UiBuilder.OnBuildUi += Draw;
            _hudManager                          =  new HudManager(_pluginInterface.TargetModuleScanner);
            _rezzIcon                         =  BuildRezzIcon(_pluginInterface);
        }

        public void Dispose()
        {
            _hudManager?.Dispose();
            _pluginInterface.UiBuilder.OnBuildUi -= Draw;
            _rezzIcon?.Dispose();
        }

        public void DrawIcon(ImDrawListPtr drawPtr, SharpDX.Vector2 pos, string text = "UNDEADED")
        {
            if (_rezzIcon == null)
                return;

            ImGui.SetCursorPos(new Vector2(pos.X - _rezzIcon.Width / 2f,       pos.Y - _rezzIcon.Height));
            ImGui.Image(_rezzIcon.ImGuiHandle, new Vector2(_rezzIcon.Width, _rezzIcon.Height));
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPos(new Vector2(pos.X, pos.Y) - textSize / 2);
            ImGui.Button(text);
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
        private const           uint    GreenBoxColor          = 0x4000FF00;

        private static unsafe void DrawPartyRect(ImDrawListPtr drawPtr, AtkUnitBase* partyList, int idx, string caster = "")
        {
            idx = 17 - idx;
            var nodePtr  = (AtkComponentNode*) partyList->ULDData.NodeList[idx];
            var colNode  = nodePtr->Component->ULDData.NodeList[2];
            var rectMin  = GetNodePosition(colNode) + GreenBoxPositionOffset * partyList->Scale;
            var rectSize = (new Vector2(colNode->Width, colNode->Height) - GreenBoxSizeOffset) * partyList->Scale;
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, GreenBoxColor, GreenBoxEdgeRounding * partyList->Scale);
            if (caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor, 2);
        }

        private static unsafe void DrawAllianceRect(ImDrawListPtr drawPtr, AtkUnitBase* allianceList, int idx, string caster = "")
        {
            idx = 9 - idx;
            var nodePtr  = allianceList->ULDData.NodeList[idx];
            var comp     = ((AtkComponentNode*) nodePtr)->Component;
            var gridNode = comp->ULDData.NodeList[2]->ChildNode;
            var rectMin  = GetNodePosition(gridNode) + new Vector2(5 * allianceList->Scale, 5 * allianceList->Scale);
            var rectSize = new Vector2(gridNode->Width * allianceList->Scale, gridNode->Height * allianceList->Scale)
              - new Vector2(8 * allianceList->Scale,                          10 * allianceList->Scale);
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, 0x4000FF00, 10 * allianceList->Scale);
            if (caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor);
        }


        public unsafe void DrawOnPartyFrames(ImDrawListPtr drawPtr)
        {
            var party     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_PartyList", 1);
            var drawParty = party != null && party->IsVisible;

            var alliance1     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList1", 1);
            var drawAlliance1 = alliance1 != null && alliance1->IsVisible;

            var alliance2     = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_AllianceList2", 1);
            var drawAlliance2 = alliance2 != null && alliance2->IsVisible;
            var anyParty      = drawParty || drawAlliance1 || drawAlliance2;

            void DrawRect(Actor corpse, Actor? caster)
            {
                if (anyParty)
                {
                    var group = _hudManager.FindGroupMemberById((uint) corpse.ActorId);
                    if (group != null)
                        switch (group.Value.groupIdx)
                        {
                            case 0:
                                if (drawParty)
                                    DrawPartyRect(drawPtr, party, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                            case 1:
                                if (drawAlliance1)
                                    DrawAllianceRect(drawPtr, alliance1, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                            case 2:
                                if (drawAlliance2)
                                    DrawAllianceRect(drawPtr, alliance2, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                        }
                }

                if (_pluginInterface.Framework.Gui.WorldToScreen(corpse.Position, out var screenPos))
                    DrawIcon(drawPtr, screenPos, caster == null ? "Already Raised" : $"Raise: {caster.Name}");
            }

            foreach (var corpse in _rezzed.Values.Where(corpse => corpse != null))
                DrawRect(corpse, null);

            foreach (var (caster, target) in _rezzes.Values.Where(c => c.caster != null && c.target != null))
                DrawRect(target!, caster);
        }

        public void Draw()
        {
            if (!Visible)
                return;

            if (_rezzed.Count + _rezzes.Count == 0)
                return;

            try
            {
                var drawPtrOpt = BeginRezzRects();
                if (drawPtrOpt == null)
                    return;

                var drawPtr = drawPtrOpt.Value;

                ImGui.PushStyleColor(ImGuiCol.Button, 0xA0002000);

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
