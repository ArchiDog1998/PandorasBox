using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using PandorasBox.FeaturesSetup;
using System.Collections.Generic;
using System.Linq;

namespace PandorasBox.Features.Actions
{
    public unsafe class AutoTankStance : Feature
    {
        public override string Name => "Auto-Tank Stance";

        public override string Description => "Activates your tank stance automatically upon job switching or entering a dungeon.";

        public override FeatureType FeatureType => FeatureType.Actions;

        public List<uint> Stances { get; set; } = new List<uint>() { 79, 91, 743, 1833 };

        public uint MainTank = 0;

        public class Configs : FeatureConfig
        {
            [FeatureConfigOption("Set delay (seconds)", FloatMin = 0.1f, FloatMax = 10f, EditorSize = 300, EnforcedLimit = true)]
            public float Throttle = 0.1f;

            [FeatureConfigOption("Activate when party size is less than or equal to", IntMin = 1, IntMax = 8, EditorSize = 300)]
            public int MaxParty = 1;

            [FeatureConfigOption("Activate if main tank dies (respects party size option)", "", 1)]
            public bool ActivateOnDeath = false;

            [FeatureConfigOption("Only activate on entrance if no other tank has stance", "", 2)]
            public bool NoOtherTanks = false;
        }

        public Configs Config { get; private set; }

        public override bool UseAutoConfig => true;


        public override void Enable()
        {
            Config = LoadConfig<Configs>() ?? new Configs();
            OnJobChanged += RunFeature;
            Svc.ClientState.TerritoryChanged += CheckIfDungeon;
            Svc.Framework.Update += CheckParty;
            base.Enable();
        }

        private void CheckParty(Framework framework)
        {
            if (Svc.Party.Length == 0 || Svc.Party.Any(x => x == null) || Svc.ClientState.LocalPlayer == null || Svc.Condition[ConditionFlag.BetweenAreas]) return;
            if (Config.ActivateOnDeath && Svc.Party.Any(x => x != null && x.ObjectId != Svc.ClientState.LocalPlayer.ObjectId && x.Statuses.Any(y => Stances.Any(z => y.StatusId == z))))
            {
                MainTank = Svc.Party.First(x => x.ObjectId != Svc.ClientState.LocalPlayer.ObjectId && x.Statuses.Any(y => Stances.Any(z => y.StatusId == z))).ObjectId;
            }
            else
            {
                MainTank = 0;
            }

            if (Svc.Party.Any(x => x.ObjectId == MainTank))
            {
                if (MainTank != 0 && Svc.Party.First(x => x.ObjectId == MainTank).GameObject.IsDead && !Svc.ClientState.LocalPlayer.StatusList.Any(x => Stances.Any(y => x.StatusId == y)))
                {
                    EnableStance(Svc.ClientState.LocalPlayer.ClassJob.Id);
                    TaskManager.Enqueue(() => TaskManager.Abort());
                }
            }
        }

        private void CheckIfDungeon(object sender, ushort e)
        {
            if (GameMain.Instance()->CurrentContentFinderConditionId == 0) return;
            TaskManager.Enqueue(() => Svc.ClientState.LocalPlayer != null);
            TaskManager.DelayNext("TankWaitForConditions", 2000);
            TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.BetweenAreas], "TankCheckConditionBetweenAreas");
            TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent], "TankCheckConditionCutscene");
            TaskManager.Enqueue(() => EnableStance(Svc.ClientState.LocalPlayer?.ClassJob.Id), "TankStanceDungeonEnabled");

        }

        private void RunFeature(uint? jobId)
        {
            if (Svc.ClientState.LocalPlayer.ClassJob.GameData.Role == 1)
            {
                EnableStance(jobId);
            }
        }

        private bool EnableStance(uint? jobId)
        {
            if (Svc.ClientState.LocalPlayer.ClassJob.GameData.Role != 1) return true;

            var am = ActionManager.Instance();
            if (Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]) return false;
            TaskManager.DelayNext("TankStance", (int)(Config.Throttle * 1000));
            TaskManager.Enqueue(() =>
            {
                if (Svc.Party.Length > Config.MaxParty) return true;
                if (Config.NoOtherTanks && Svc.Party.Any(x => x.ObjectId != Svc.ClientState.LocalPlayer.ObjectId && x.Statuses.Any(y => Stances.Any(z => y.StatusId == z)))) return true;
                switch (Svc.ClientState.LocalPlayer.ClassJob.Id)
                {
                    case 1:
                    case 19:
                        if (Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 79)) return true;
                        if (am->GetActionStatus(ActionType.Spell, 28) == 0)
                        {
                            am->UseAction(ActionType.Spell, 28);
                            return true;
                        }
                        return false;
                    case 3:
                    case 21:
                        if (Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 91)) return true;
                        if (am->GetActionStatus(ActionType.Spell, 48) == 0)
                        {
                            am->UseAction(ActionType.Spell, 48);
                            return true;
                        }
                        return false;
                    case 32:
                        if (Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 743)) return true;
                        if (am->GetActionStatus(ActionType.Spell, 3629) == 0)
                        {
                            am->UseAction(ActionType.Spell, 3629);
                            return true;
                        }
                        return false;
                    case 37:
                        if (Svc.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1833)) return true;
                        if (am->GetActionStatus(ActionType.Spell, 16142) == 0)
                        {
                            am->UseAction(ActionType.Spell, 16142);
                            return true;
                        }
                        return false;

                }

                return true;
            });


            return true;
        }

        public override void Disable()
        {
            SaveConfig(Config);
            OnJobChanged -= RunFeature;
            Svc.ClientState.TerritoryChanged -= CheckIfDungeon;
            Svc.Framework.Update -= CheckParty;
            base.Disable();
        }
    }
}
