using Dalamud.Game;
using Dalamud.Logging;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PandorasBox.FeaturesSetup;
using PandorasBox.Helpers;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace PandorasBox.Features.Targets
{
    public unsafe class AutoOpenChests : Feature
    {
        public override string Name => "Automatically Open Chests";

        public override string Description => "Walk up to a chest to automatically open it.";

        public override FeatureType FeatureType => FeatureType.Targeting;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("Set delay (seconds)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300, EnforcedLimit = true)]
            public float Throttle = 0.1f;

            [FeatureConfigOption("Immediately Close Loot Window After Opening")]
            public bool CloseLootWindow = false;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;

        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            Svc.Framework.Update += RunFeature;
            base.Enable();
        }

        private void RunFeature(Framework framework)
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
            {
                TaskManager.Abort();
                return;
            }

            var nearbyNodes = Svc.Objects.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure
                && GameObjectHelper.GetTargetDistance(x) <= 0.5f && !IsOpened(x.ObjectId)).ToList();
            if (nearbyNodes.Count == 0)
                return;

            var nearestNode = nearbyNodes.OrderBy(GameObjectHelper.GetTargetDistance).First();
            var baseObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)nearestNode.Address;

            if (!baseObj->GetIsTargetable())
                return;

            if (!TaskManager.IsBusy)
            {
                TaskManager.DelayNext("Chests", (int)(Config.Throttle * 1000));
                TaskManager.Enqueue(() =>
                {
                    if (GameObjectHelper.GetTargetDistance(nearestNode) > 0.5f) return true;

                    TargetSystem.Instance()->InteractWithObject(baseObj, true);
                    if (Config.CloseLootWindow)
                    {
                        TaskManager.DelayNextImmediate(100);
                        TaskManager.EnqueueImmediate(() => CloseWindow(), 5000, false);
                    }
                    return true;
                }, 10, true);
            }
        }

        private bool IsOpened(uint objectId)
        {
            //Well IDK why in the release version instead of submodule, I can get Loot.Instance()->ItemArraySpan.
            foreach (var item in new Span<LootItem>(Unsafe.AsPointer(ref Loot.Instance()->ItemArray[0]), 16))
            {
                if (item.ChestObjectId == objectId) return true;
            }
            return false;
        }

        private static unsafe bool? CloseWindow()
        {
            if (Svc.GameGui.GetAddonByName("NeedGreed", 1) != IntPtr.Zero)
            {
                var needGreedWindow = (AtkUnitBase*)Svc.GameGui.GetAddonByName("NeedGreed", 1);
                if (needGreedWindow == null) return false;

                if (needGreedWindow->IsVisible)
                {
                    needGreedWindow->Close(true);
                    return true;
                }                
            }
            else
            {
                return false;
            }

            return false;
        }

        public override void Disable()
        {
            SaveConfig(Config);
            Svc.Framework.Update -= RunFeature;
            base.Disable();
        }
    }
}
