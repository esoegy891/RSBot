﻿using RSBot.Core;
using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Components;
using RSBot.Core.Event;
using RSBot.Core.Extensions;
using RSBot.Core.Objects;
using RSBot.Core.Objects.Skill;
using RSBot.Skills.Components;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace RSBot.Skills.Views
{
    [System.ComponentModel.ToolboxItem(false)]
    public partial class Main : UserControl
    {
        private class MasteryComboBoxItem
        {
            public RefSkillMastery Record;

            public byte Level;

            public override string ToString()
            {
                return Record.Name + $" lv.{Level}";
            }
        }

        private bool _applySkills;
        private object _lock;
        private MasteryComboBoxItem _selectedMastery;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            InitializeComponent();
            SubscribeEvents();

            listAttackingSkills.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;
            listBuffs.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;
            listSkills.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;
            listActiveBuffs.SmallImageList = Core.Extensions.ListViewExtensions.StaticImageList;

            _lock = new object();
        }

        /// <summary>
        /// Subscribes the events.
        /// </summary>
        private void SubscribeEvents()
        {
            EventManager.SubscribeEvent("OnEnterGame", OnEnterGame);
            EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);
            EventManager.SubscribeEvent("OnLearnSkill", new Action<SkillInfo, bool>(OnLearnSkill));
            EventManager.SubscribeEvent("OnLearnSkillMastery", new Action<MasteryInfo>(OnLearnSkillMastery));
            EventManager.SubscribeEvent("OnWithdrawSkill", new Action<SkillInfo>(OnWithdrawSkill));
            EventManager.SubscribeEvent("OnAddBuff", new Action<SkillInfo>(OnAddBuff));
            EventManager.SubscribeEvent("OnRemoveBuff", new Action<SkillInfo>(OnRemoveBuff));
            EventManager.SubscribeEvent("OnResurrectionRequest", OnResurrectionRequest);
            EventManager.SubscribeEvent("OnExpSpUpdate", OnSpUpdated);
            EventManager.SubscribeEvent("OnAddItemPerk", new Action<uint, uint>(OnAddItemPerk));
            EventManager.SubscribeEvent("OnRemoveItemPerk", new Action<uint, ItemPerk>(OnRemoveItemPerk));
        }

        /// <summary>
        /// Called when [remove item perk].
        /// </summary>
        /// <param name="targetId">The target identifier.</param>
        /// <param name="removedPerk">The removed perk.</param>
        private void OnRemoveItemPerk(uint targetId, ItemPerk? removedPerk)
        {
            if (targetId != Game.Player.UniqueId || removedPerk == null)
                return;

            for (var i = 0; i < listActiveBuffs.Items.Count; i++)
            {
                var listItem = listActiveBuffs.Items[i];

                if (listItem?.Tag is not ItemPerk perkInfo || perkInfo.Token != removedPerk.Token) continue;
                
                listItem.Remove();
                return;
            }
        }

        /// <summary>
        /// Called when [add item perk].
        /// </summary>
        /// <param name="targetId">The target identifier.</param>
        /// <param name="token">The token.</param>
        private void OnAddItemPerk(uint targetId, uint token)
        {
            if (targetId != Game.Player.UniqueId)
                return;

            var perk = Game.Player.State.ActiveItemPerks[token];
            var item = new ListViewItem
            {
                Text = perk.Item?.GetRealName(),
                Tag = perk
            };

            listActiveBuffs.Items.Add(item);

            item.LoadSkillImage();
        }
        /// <summary>
        /// Will be triggered if EXP/SP were gained. Increases the selected mastery level (if available)
        /// </summary>
        private void OnSpUpdated()
        {
            if (_selectedMastery == null || !checkLearnMastery.Checked) return;
            
            while (_selectedMastery.Level + numMasteryGap.Value < Game.Player.Level)
            {
                if (!checkLearnMasteryBotStopped.Checked && !Kernel.Bot.Running) break;

                var nextMasteryLevel = Game.ReferenceManager.GetRefLevel((byte) (_selectedMastery.Level + 1));

                if (nextMasteryLevel.Exp_M > Game.Player.SkillPoints)
                {
                    Log.Debug($"Auto. upping mastery cancelled due to insufficient skill points. Required: {nextMasteryLevel.Exp_M}");

                    break;
                }

                Log.Notify($"Auto. train mastery [{_selectedMastery.Record.Name} to lv. {nextMasteryLevel}");
                LearnMasteryHandler.LearnMastery(_selectedMastery.Record.ID);
                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// The first event that will be fired after the player enters the game
        /// </summary>
        private void OnEnterGame()
        {
            checkShowAttacks.Checked = PlayerConfig.Get<bool>("RSBot.Skills.ShowAttacks", true);
            checkShowBuffs.Checked = PlayerConfig.Get<bool>("RSBot.Skills.ShowBuffs", true);
            checkHideLowerLevelSkills.Checked = PlayerConfig.Get<bool>("RSBot.Skills.HideLowerLevelSkills");
            checkAcceptResurrection.Checked = PlayerConfig.Get<bool>("RSBot.Skills.AcceptResurrection");
            checkResurrectParty.Checked = PlayerConfig.Get<bool>("RSBot.Skills.ResurrectPartyMembers");
            checkCastBuffsInTowns.Checked = PlayerConfig.Get<bool>("RSBot.Skills.CastBuffsInTowns");
            checkCastBuffsDuringWalkBack.Checked = PlayerConfig.Get<bool>("RSBot.Skills.CastBuffsDuringWalkBack");
            checkBoxNoAttack.Checked = PlayerConfig.Get<bool>("RSBot.Skills.NoAttack");
            checkLearnMastery.Checked = PlayerConfig.Get<bool>("RSBot.Skills.learnMastery");
            numMasteryGap.Value = PlayerConfig.Get<byte>("RSBot.Skills.masteryGap", 0);
            checkWarlockMode.Checked = PlayerConfig.Get<bool>("RSBot.Skills.WarlockMode", false);
        }

        /// <summary>
        /// Applies the attack skills.
        /// </summary>
        private void ApplyAttackSkills()
        {
            for (var i = 0; i < comboMonsterType.Items.Count; i++)
            {
                var skillIds = PlayerConfig.GetArray<uint>("RSBot.Skills.Attacks_" + i);

                foreach (var skillId in skillIds)
                {
                    var refSkill = Game.ReferenceManager.GetRefSkill(skillId);
                    var skillInfo = Game.Player.Skills.GetSkillInfoByGroupId(refSkill.GroupID);

                    if (skillInfo == null)
                        continue;

                    switch (i)
                    {
                        case 1:
                            SkillManager.Skills[MonsterRarity.Champion].Add(skillInfo);
                            continue;
                        case 2:
                            SkillManager.Skills[MonsterRarity.Giant].Add(skillInfo);
                            continue;
                        case 3:
                            SkillManager.Skills[MonsterRarity.GeneralParty].Add(skillInfo);
                            continue;
                        case 4:
                            SkillManager.Skills[MonsterRarity.ChampionParty].Add(skillInfo);
                            continue;
                        case 5:
                            SkillManager.Skills[MonsterRarity.GiantParty].Add(skillInfo);
                            continue;
                        case 6:
                            SkillManager.Skills[MonsterRarity.Elite].Add(skillInfo);
                            continue;
                        case 7:
                            SkillManager.Skills[MonsterRarity.EliteStrong].Add(skillInfo);
                            continue;
                        case 8:
                            SkillManager.Skills[MonsterRarity.Unique].Add(skillInfo);
                            continue;
                        default:
                            SkillManager.Skills[MonsterRarity.General].Add(skillInfo);
                            continue;
                    }
                }
            }
        }

        /// <summary>
        /// Applies the buff skills.
        /// </summary>
        private void ApplyBuffSkills()
        {
            SkillManager.Buffs.Clear();

            foreach (var buffId in PlayerConfig.GetArray<uint>("RSBot.Skills.Buffs"))
            {
                var refSkill = Game.ReferenceManager.GetRefSkill(buffId);
                var skillInfo = Game.Player.Skills.GetSkillInfoByGroupId(refSkill.GroupID);

                if (skillInfo == null)
                    continue;

                SkillManager.Buffs.Add(skillInfo);
            }
        }

        /// <summary>
        /// Applies the skills.
        /// </summary>
        private void ApplySkills()
        {
            if (!_applySkills)
                return;

            foreach (var collection in SkillManager.Skills.Values)
                collection.Clear();

            ApplyAttackSkills();
            ApplyBuffSkills();
        }

        private void LoadMasteries()
        {
            var selectedMastery = PlayerConfig.Get<string>("RSBot.Skills.selectedMastery");
            comboLearnMastery.BeginUpdate();
            comboLearnMastery.Items.Clear();

            foreach (var mastery in Game.Player.Skills.Masteries)
                comboLearnMastery.Items.Add(new MasteryComboBoxItem { Level = mastery.Level, Record = mastery.Record });

            foreach (MasteryComboBoxItem item in comboLearnMastery.Items)
                if (item.Record.NameCode == selectedMastery)
                    comboLearnMastery.SelectedItem = item;

            comboLearnMastery.EndUpdate();

            comboLearnMastery.Update();
        }

        /// <summary>
        /// Loads the attacks.
        /// </summary>
        /// <param name="index">The index.</param>
        private void LoadAttacks(int index = 0)
        {
            lock (_lock)
            {
                listAttackingSkills.BeginUpdate();
                listAttackingSkills.Items.Clear();

                var skillArray = PlayerConfig.GetArray<uint>("RSBot.Skills.Attacks_" + index);
                var refSkills = skillArray.Select(s => Game.ReferenceManager.GetRefSkill(s)).ToList();

                foreach (var skillInfo in Game.Player.Skills.KnownSkills.FindAll(p => p.Enabled && p.IsAttack).ToArray())
                {
                    var hasAnyOfThisSkill = refSkills.FirstOrDefault(s => s.GroupID == skillInfo.Record.GroupID) != null;
                    if (!hasAnyOfThisSkill)
                        continue;

                    var item = new ListViewItem(skillInfo.Record.GetRealName()) { Tag = skillInfo };
                    item.SubItems.Add("lv. " + skillInfo.Record.Basic_Level);
                    listAttackingSkills.Items.Add(item);
                    item.LoadSkillImageAsync();
                }

                ApplyAttackSkills();
                listAttackingSkills.EndUpdate();
            }
        }

        /// <summary>
        /// Loads the buffs.
        /// </summary>
        private void LoadBuffs()
        {
            lock (_lock)
            {
                listBuffs.BeginUpdate();
                listBuffs.Items.Clear();

                var skillArray = PlayerConfig.GetArray<uint>("RSBot.Skills.Buffs");
                var refSkills = skillArray.Select(s => Game.ReferenceManager.GetRefSkill(s)).ToList();

                foreach (var skillInfo in Game.Player.Skills.KnownSkills.FindAll(p => p.Enabled && !p.IsAttack).ToArray())
                {
                    var hasAnyOfThisSkill = refSkills.FirstOrDefault(s => s.GroupID == skillInfo.Record.GroupID) != null;
                    if (!hasAnyOfThisSkill)
                        continue;

                    var item = new ListViewItem(skillInfo.Record.GetRealName()) { Tag = skillInfo };

                    item.SubItems.Add("lv. " + skillInfo.Record.Basic_Level);
                    listBuffs.Items.Add(item);
                    item.LoadSkillImageAsync();
                }

                ApplyBuffSkills();
                listBuffs.EndUpdate();
            }
        }

        /// <summary>
        /// Loads the imbues.
        /// </summary>
        private void LoadImbues()
        {
            lock (_lock)
            {
                comboImbue.Items.Clear();

                var selectedImbue = PlayerConfig.Get<uint>("RSBot.Skills.Imbue");

                comboImbue.SelectedIndex = comboImbue.Items.Add("None");

                foreach (var skill in Game.Player.Skills.KnownSkills.Where(s => s.IsImbue && s.Enabled))
                {
                    /*
                    if (skill.IsLowLevel())
                        continue;
                    */
                    var index = comboImbue.Items.Add(skill);

                    if (selectedImbue == 0)
                        continue;

                    var refSkill = Game.ReferenceManager.GetRefSkill(selectedImbue);
           
                    if (refSkill?.GroupID == skill.Record?.GroupID)
                        comboImbue.SelectedIndex = index;
                }
            }
        }

        /// <summary>
        /// Loads the resurrection skills.
        /// </summary>
        private void LoadResurrectionSkills()
        {
            lock (_lock)
            {
                comboResurrectionSkill.Items.Clear();

                comboResurrectionSkill.SelectedIndex = comboResurrectionSkill.Items.Add("None");

                foreach (var skill in Game.Player.Skills.KnownSkills.Where(
                    s => s.Record != null && s.Record.TargetEtc_SelectDeadBody &&
                   (s.Record.Params[3] == 1751474540 || s.Record.Params[3] == 1919776116)))
                {
                    if (skill.IsLowLevel())
                        continue;

                    var index = comboResurrectionSkill.Items.Add(skill);
                    var resurrectionSkillId = PlayerConfig.Get<uint>("RSBot.Skills.ResurrectionSkill");
                    if (resurrectionSkillId == 0)
                        continue;

                    var refSkill = Game.ReferenceManager.GetRefSkill(resurrectionSkillId);
                    if (refSkill?.GroupID == skill.Record?.GroupID)
                        comboResurrectionSkill.SelectedIndex = index;
                }
            }
        }

        /// <summary>
        /// Loads the skills.
        /// </summary>
        private void LoadSkills()
        {
            lock (_lock)
            {
                if (Game.Player == null)
                    return;

                LoadImbues();
                LoadResurrectionSkills();

                listSkills.BeginUpdate();
                listSkills.Items.Clear();
                listSkills.Groups.Clear();

                foreach (var mastery in Game.Player.Skills.Masteries)
                {
                    var group = new ListViewGroup(Game.ReferenceManager.GetTranslation(mastery.Record.NameCode) + " (lv. " + mastery.Level + ")");
                    group.Tag = mastery.Id;
                    listSkills.Groups.Add(group);
                }

                foreach (var skill in Game.Player.Skills.KnownSkills.Where(s => s.Enabled && s.Record.ReqCommon_Mastery1 != 1000))
                {
                    if (checkHideLowerLevelSkills.Checked && skill.IsLowLevel())
                        continue;

                    if (skill.IsPassive)
                        continue;

                    if (!skill.IsAttack && skill.Record.Target_Required && !skill.Record.TargetGroup_Self)
                        continue;

                    var name = skill.Record.GetRealName();

                    var item = new ListViewItem(name) { Tag = skill };
                    item.SubItems.Add("lv. " + skill.Record.Basic_Level);

                    foreach (var @group in listSkills.Groups.Cast<ListViewGroup>().Where(@group => Convert.ToInt32(@group.Tag) == skill.Record.ReqCommon_Mastery1))
                        item.Group = @group;

                    if (skill.IsAttack && checkShowAttacks.Checked)
                        listSkills.Items.Add(item);
                    else if (!skill.IsAttack && !skill.IsImbue && checkShowBuffs.Checked)
                        listSkills.Items.Add(item);

                    item.LoadSkillImage();
                }

                listSkills.EndUpdate();
            }
        }

        /// <summary>
        /// Saves the attacks.
        /// </summary>
        private void SaveAttacks()
        {
            var savedSkills = listAttackingSkills.Items.Cast<ListViewItem>().Select(p => ((SkillInfo)p.Tag).Id).ToArray();

            PlayerConfig.SetArray("RSBot.Skills.Attacks_" + comboMonsterType.SelectedIndex, savedSkills);

            _applySkills = true;
            ApplySkills();
            _applySkills = false;
        }

        /// <summary>
        /// Saves the buffs.
        /// </summary>
        private void SaveBuffs()
        {
            var savedBuffs = listBuffs.Items.Cast<ListViewItem>().Select(p => ((SkillInfo)p.Tag).Id).ToArray();

            PlayerConfig.SetArray("RSBot.Skills.Buffs", savedBuffs);

            ApplyBuffSkills();
        }

        /// <summary>
        /// Run the event after added the buff from the character
        /// </summary>
        /// <param name="buffInfo">The added <see cref="BuffInfo"/></param>
        private void OnAddBuff(SkillInfo buffInfo)
        {
            try
            {
                var item = new ListViewItem
                {
                    Text = buffInfo.Record.GetRealName(),
                    Tag = buffInfo
                };

                item.SubItems.Add("lv. " + buffInfo.Record.Basic_Level);

                listActiveBuffs.Items.Add(item);
                item.LoadSkillImageAsync();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Run the event after removed the buff from the character
        /// </summary>
        /// <param name="buffInfo">The removed <see cref="BuffInfo"/></param>
        private void OnRemoveBuff(SkillInfo removingBuff)
        {
            try
            {
                for (int i = 0; i < listActiveBuffs.Items.Count; i++)
                {
                    var listItem = listActiveBuffs.Items[i];
                    if (listItem == null)
                        continue;

                    var itemBuffInfo = listItem.Tag as SkillInfo;
                    if (itemBuffInfo != null &&
                        itemBuffInfo.Id == removingBuff.Id &&
                        itemBuffInfo.Token == removingBuff.Token)
                    {
                        // System.IndexOutOfRangeException: 'Index was outside the bounds of the array.' ??
                        listItem.Remove();
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Core_s the on learn skill.
        /// </summary>
        /// <param name="skill">The skill.</param>
        /// <param name="update">if set to <c>true</c> [update].</param>
        private void OnLearnSkill(SkillInfo skill, bool update)
        {
            var name = skill.Record.GetRealName();
            if (update)
                Log.NotifyLang("SkillUpgraded", name);
            else
                Log.NotifyLang("SkillLearned", name);

            LoadSkills();
            LoadAttacks();
            LoadBuffs();

            _applySkills = true;
            ApplySkills();
            _applySkills = false;
        }

        /// <summary>
        /// Core_s the on learn skill mastery.
        /// </summary>
        /// <param name="info">The information.</param>
        private void OnLearnSkillMastery(MasteryInfo info)
        {
            Log.NotifyLang("MasteryUpgraded", info.Record.Name);

            LoadMasteries();
            LoadSkills();
        }

        /// <summary>
        /// Main_s the on load character.
        /// </summary>
        private void OnLoadCharacter()
        {
            comboMonsterType.SelectedIndex = 0;
            LoadSkills();
            LoadAttacks();
            LoadBuffs();
            LoadMasteries();
            _applySkills = true;
            ApplySkills();
            _applySkills = false;

            listActiveBuffs.Items.Clear();
        }

        /// <summary>
        /// Core_s the on resurrection request
        /// </summary>
        private void OnResurrectionRequest()
        {
            if (Game.AcceptanceRequest != null && PlayerConfig.Get<bool>("RSBot.Skills.AcceptResurrection"))
                Game.AcceptanceRequest.Accept();
        }

        /// <summary>
        /// Core_s the on withdraw skill.
        /// </summary>
        /// <param name="oldSkill">The old skill.</param>
        private void OnWithdrawSkill(SkillInfo oldSkill)
        {
            Log.NotifyLang("SkillWithdrawn", oldSkill.Record.GetRealName());

            LoadSkills();
            LoadAttacks();
            LoadBuffs();

            _applySkills = true;
            ApplySkills();
            _applySkills = false;
        }

        /// <summary>
        /// Handles the Click event of the btnMoveAttackSkillDown control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnMoveAttackSkillDown_Click(object sender, EventArgs e)
        {
            listAttackingSkills.MoveSelectedItems(MoveDirection.Down);
            SaveAttacks();
        }

        /// <summary>
        /// Handles the Click event of the btnMoveAttackSkillUp control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnMoveAttackSkillUp_Click(object sender, EventArgs e)
        {
            listAttackingSkills.MoveSelectedItems(MoveDirection.Up);
            SaveAttacks();
        }

        /// <summary>
        /// Handles the Click event of the btnMoveBuffSkillDown control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnMoveBuffSkillDown_Click(object sender, EventArgs e)
        {
            listBuffs.MoveSelectedItems(MoveDirection.Down);
            SaveBuffs();
        }

        /// <summary>
        /// Handles the Click event of the btnMoveBuffSkillUp control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnMoveBuffSkillUp_Click(object sender, EventArgs e)
        {
            listBuffs.MoveSelectedItems(MoveDirection.Up);
            SaveBuffs();
        }

        /// <summary>
        /// Handles the Click event of the btnRemoveAttackSkill control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnRemoveAttackSkill_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listAttackingSkills.SelectedItems)
                item.Remove();

            SaveAttacks();
        }

        /// <summary>
        /// Handles the Click event of the btnRemoveBuffSkill control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void btnRemoveBuffSkill_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listBuffs.SelectedItems)
                item.Remove();

            SaveBuffs();
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkAcceptResurrection control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkAcceptResurrection_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.AcceptResurrection", checkAcceptResurrection.Checked);
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkCastBuffsInTowns control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkCastBuffsInTowns_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.CastBuffsInTowns", checkCastBuffsInTowns.Checked);
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkCastBuffsWhenWalkBack control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkCastBuffsWhenWalkBack_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.CastBuffsDuringWalkBack", checkCastBuffsDuringWalkBack.Checked);
        }

        /// <summary>
        /// Handles the CheckedChanged event of the checkResurrectParty control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void checkResurrectParty_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.ResurrectPartyMembers", checkResurrectParty.Checked);
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the comboImue control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void comboImbue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboImbue.SelectedIndex < 0)
                return;

            SkillInfo imbue = null;

            if (comboImbue.SelectedIndex > 0)
                imbue = comboImbue.SelectedItem as SkillInfo;

            SkillManager.ImbueSkill = imbue;
            PlayerConfig.Set("RSBot.Skills.Imbue", imbue == null ? 0 : imbue.Id);
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the comboMonsterType control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void comboMonsterType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAttacks(comboMonsterType.SelectedIndex);
        }

        /// <summary>
        /// Handles the SelectedIndexChanged event of the comboResurrectionSkill control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void comboResurrectionSkill_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboResurrectionSkill.SelectedIndex < 0)
                return;

            SkillInfo skill = null;

            if (comboResurrectionSkill.SelectedIndex > 0)
                skill = comboResurrectionSkill.SelectedItem as SkillInfo;

            SkillManager.ResurrectionSkill = skill;
            PlayerConfig.Set("RSBot.Skills.ResurrectionSkill", skill == null ? 0 : skill.Id);
        }

        /// <summary>
        /// Handles the CheckedChanged event of the filters control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void Filter_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.ShowBuffs", checkShowBuffs.Checked);
            PlayerConfig.Set("RSBot.Skills.ShowAttacks", checkShowAttacks.Checked);
            PlayerConfig.Set("RSBot.Skills.HideLowerLevelSkills", checkHideLowerLevelSkills.Checked);

            LoadSkills();
        }

        /// <summary>
        /// Handles the Click event of the menuAddAttack control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void menuAddAttack_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listSkills.SelectedItems)
            {
                var selectedRefSkill = item.Tag as SkillInfo;
                if (listAttackingSkills.Items.Cast<ListViewItem>()
                   .Count(p => ((SkillInfo)p.Tag).Record.ID != selectedRefSkill.Id && ((SkillInfo)p.Tag).Record.Action_Overlap != 0 && ((SkillInfo)p.Tag).Record.Action_Overlap == selectedRefSkill.Record.Action_Overlap) != 0)
                    continue;

                if (selectedRefSkill != null && selectedRefSkill.IsAttack)
                    listAttackingSkills.Items.Add((ListViewItem)item.Clone());
            }

            SaveAttacks();
        }

        /// <summary>
        /// Handles the Click event of the menuAddBuff control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void menuAddBuff_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listSkills.SelectedItems)
            {
                var selectedRefSkill = item.Tag as SkillInfo;
                if (listBuffs.Items.Cast<ListViewItem>()
                   .Count(p => ((SkillInfo)p.Tag).Record.Action_Overlap != 0 && ((SkillInfo)p.Tag).Record.Action_Overlap == selectedRefSkill.Record.Action_Overlap) != 0)
                    continue;

                if (selectedRefSkill != null && !selectedRefSkill.IsAttack)
                    listBuffs.Items.Add((ListViewItem)item.Clone());
            }

            SaveBuffs();
        }

        private void checkBoxNoAttack_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.NoAttack", checkBoxNoAttack.Checked);
        }

        private void checkLearnMastery_Click(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.learnMastery", checkLearnMastery.Checked);
        }

        private void numMasteryGap_ValueChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.masteryGap", numMasteryGap.Value);
        }

        private void comboLearnMastery_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboLearnMastery.SelectedIndex < 0) return;

            var selectedItem = (MasteryComboBoxItem)comboLearnMastery.SelectedItem;
            _selectedMastery = selectedItem;

            PlayerConfig.Set("RSBot.Skills.selectedMastery", selectedItem.Record.NameCode);
        }

        private void checkWarlockMode_CheckedChanged(object sender, EventArgs e)
        {
            PlayerConfig.Set("RSBot.Skills.WarlockMode", checkWarlockMode.Checked);
        }

        private void listSkills_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listSkills.SelectedItems.Count <= 0)
                return;

#if !DEBUG
            if(GlobalConfig.Get<bool>("RSBot.DebugEnvironment") == false)
                return;
#endif

            if (!(listSkills.SelectedItems[0].Tag is SkillInfo skillInfo)) 
                return;

            var itemForm = new SkillProperties(skillInfo.Record);
            itemForm.Show();
        }
    }
}
