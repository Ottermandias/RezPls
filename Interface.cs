using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Windows.Forms.ComponentModel.Com2Interop;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Group;
using ImGuiNET;
using RezzPls.SeFunctions;

namespace RezzPls
{
    public class Interface : IDisposable
    {
        private readonly DalamudPluginInterface    _pluginInterface;
        private readonly Hook<UpdatePartyDelegate> _updatePartyHook;
        private          IntPtr                    _hudAgentPtr = IntPtr.Zero;

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

            UpdateParty updateParty = new(_pluginInterface.TargetModuleScanner);
            _updatePartyHook = updateParty.CreateHook(UpdatePartyHook, this)!;

            _phoenixDown = _pluginInterface.UiBuilder.LoadImage(@"H:\Garfield.png");
        }

        public void Dispose()
        {
            _updatePartyHook.Dispose();
            _pluginInterface.UiBuilder.OnBuildUi -= Draw;
            _phoenixDown.Dispose();
        }
        
        private void UpdatePartyHook(IntPtr hudAgent)
        {
            _hudAgentPtr = hudAgent;
            PluginLog.LogVerbose($"Obtained HUD agent at address 0x{_hudAgentPtr.ToInt64():X16}.");
            _updatePartyHook.Original(hudAgent);
            _updatePartyHook.Disable();
            _updatePartyHook.Dispose();
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
            var rectMin  = GetNodePosition(colNode) + new Vector2(5 * colNode->ScaleX, 5 * colNode->ScaleY);
            var rectSize = new Vector2(colNode->Width, colNode->Height) - new Vector2(8 * colNode->ScaleX, 10 * colNode->ScaleY);
            drawPtr.AddRectFilled(rectMin, rectMin + rectSize, 0x4000FF00, 10);
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

        private const int GroupManagerOffset = 0x1DDAA50;
        private const int AllianceRaidOffset = 0x1180;
        private const int PMemberIdOffset    = 0x01A8;
        private const int MemberNameOffset   = 0x01C4;
        private const int AMemberIdOffset    = AllianceRaidOffset + PMemberIdOffset;

        // @formatter:off
        private static readonly int[,] ActorOffsets = new int[2,24]
        {
            {
                PMemberIdOffset +  0 * 0x230, PMemberIdOffset +  1 * 0x230, PMemberIdOffset +  2 * 0x230, PMemberIdOffset +  3 * 0x230, 
                PMemberIdOffset +  4 * 0x230, PMemberIdOffset +  5 * 0x230, PMemberIdOffset +  6 * 0x230, PMemberIdOffset +  7 * 0x230,
                AMemberIdOffset +  0 * 0x230, AMemberIdOffset +  1 * 0x230, AMemberIdOffset +  2 * 0x230, AMemberIdOffset +  3 * 0x230, 
                AMemberIdOffset +  4 * 0x230, AMemberIdOffset +  5 * 0x230, AMemberIdOffset +  6 * 0x230, AMemberIdOffset +  7 * 0x230,
                AMemberIdOffset +  8 * 0x230, AMemberIdOffset +  9 * 0x230, AMemberIdOffset + 10 * 0x230, AMemberIdOffset + 11 * 0x230, 
                AMemberIdOffset + 12 * 0x230, AMemberIdOffset + 13 * 0x230, AMemberIdOffset + 14 * 0x230, AMemberIdOffset + 15 * 0x230,
            },
            {
                PMemberIdOffset +  0 * 0x230, PMemberIdOffset +  1 * 0x230, PMemberIdOffset +  2 * 0x230, PMemberIdOffset +  3 * 0x230,
                AMemberIdOffset +  0 * 0x230, AMemberIdOffset +  1 * 0x230, AMemberIdOffset +  2 * 0x230, AMemberIdOffset +  3 * 0x230,
                AMemberIdOffset +  4 * 0x230, AMemberIdOffset +  5 * 0x230, AMemberIdOffset +  6 * 0x230, AMemberIdOffset +  7 * 0x230,
                AMemberIdOffset +  8 * 0x230, AMemberIdOffset +  9 * 0x230, AMemberIdOffset + 10 * 0x230, AMemberIdOffset + 11 * 0x230,
                AMemberIdOffset + 12 * 0x230, AMemberIdOffset + 13 * 0x230, AMemberIdOffset + 14 * 0x230, AMemberIdOffset + 15 * 0x230,
                AMemberIdOffset + 16 * 0x230, AMemberIdOffset + 17 * 0x230, AMemberIdOffset + 18 * 0x230, AMemberIdOffset + 19 * 0x230,
            },
        };
        // @formatter:on

        private unsafe (int, int)? FindCorpseInGroup(Actor corpse)
        {
            var basePtr     = (GroupManager*) (_pluginInterface.TargetModuleScanner.Module.BaseAddress + GroupManagerOffset);
            var pvpAlliance = (basePtr->Unk_3D60 & 0x2) == 0x2 ? 1 : 0;
            var maxPlayers  = basePtr->IsAlliance ? 24 : 8;
            for (var i = 0; i < maxPlayers; ++i)
            {
                var memberId = *(uint*) ((byte*) basePtr + ActorOffsets[pvpAlliance, i]);
                if (memberId != corpse.ActorId)
                    continue;

                var groupSize = pvpAlliance == 1 ? 4 : 8;
                return (i / groupSize, i % groupSize);
            }

            return null;
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
                    var group = FindCorpseInGroup(corpse);
                    if (group != null)
                        switch (@group.Value.Item1)
                        {
                            case 0:
                                DrawPartyRect(drawPtr, party, @group.Value.Item2, caster == null ? "" : caster.Name);
                                break;
                            case 1:
                                DrawAllianceRect(drawPtr, alliance1, @group.Value.Item2, caster == null ? "" : caster.Name);
                                break;
                            case 2:
                                DrawAllianceRect(drawPtr, alliance2, @group.Value.Item2, caster == null ? "" : caster.Name);
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
