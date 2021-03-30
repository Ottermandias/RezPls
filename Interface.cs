using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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

        private ImGuiScene.TextureWrap _phoenixDown;

        public Interface(DalamudPluginInterface pluginInterface, ActorWatcher actorWatcher)
        {
            _pluginInterface                     =  pluginInterface;
            _rezzes                              =  actorWatcher.RezzList;
            _rezzed                              =  actorWatcher.RezzedList;
            _pluginInterface.UiBuilder.OnBuildUi += Draw;
            _hudManager                          =  new HudManager(_pluginInterface.TargetModuleScanner);

            _phoenixDown = _pluginInterface.UiBuilder.LoadImage(@"H:\Garfield.png");
        }

        public void Dispose()
        {
            _hudManager.Dispose();
            _pluginInterface.UiBuilder.OnBuildUi -= Draw;
            _phoenixDown.Dispose();
        }

        private static readonly Vector2 IconSize = new(150, 200);

        public void DrawIcon(ImDrawListPtr drawPtr, SharpDX.Vector2 pos, string text = "UNDEADED")
        {
            ImGui.SetCursorPos(new Vector2(pos.X, pos.Y - IconSize.Y / 2) - IconSize / 2);
            ImGui.Image(_phoenixDown.ImGuiHandle, IconSize);
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

        private static unsafe void DrawPartyRect(ImDrawListPtr drawPtr, AtkUnitBase* partyList, int idx, string caster = "")
        {
            idx = 17 - idx;
            var nodePtr  = (AtkComponentNode*) partyList->ULDData.NodeList[idx];
            var colNode  = nodePtr->Component->ULDData.NodeList[2];
            var rectMin  = GetNodePosition(colNode) + new Vector2(5 * partyList->Scale, 5 * partyList->Scale);
            var rectSize = new Vector2(colNode->Width * partyList->Scale, colNode->Height * partyList->Scale) - new Vector2(8 * partyList->Scale, 10 * partyList->Scale);
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, 0x4000FF00, 10 * partyList->Scale);
            if (caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            ImGui.Text(caster);
        }

        private static unsafe void DrawAllianceRect(ImDrawListPtr drawPtr, AtkUnitBase* allianceList, int idx, string caster = "")
        {
            idx = 9 - idx;
            var nodePtr  = allianceList->ULDData.NodeList[idx];
            var rectMin  = GetNodePosition(nodePtr) + new Vector2(5 * nodePtr->ScaleX, 5 * nodePtr->ScaleY);
            var rectSize = new Vector2(nodePtr->Width, nodePtr->Height) - new Vector2(8 * nodePtr->ScaleX, 10 * nodePtr->ScaleY);
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, 0x4000FF00, 10);
            if (caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            ImGui.Text(caster);
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
                                if(drawParty)
                                    DrawPartyRect(drawPtr, party, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                            case 1:
                                if(drawAlliance1)
                                    DrawAllianceRect(drawPtr, alliance1, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                            case 2:
                                if(drawAlliance2)
                                    DrawAllianceRect(drawPtr, alliance2, group.Value.idx, caster == null ? "" : caster.Name);
                                break;
                        }
                }

                if (_pluginInterface.Framework.Gui.WorldToScreen(corpse.Position, out var screenPos))
                    DrawIcon(drawPtr, screenPos, caster == null ? "Resurrected" : $"Being resurrected by {caster.Name}");
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
