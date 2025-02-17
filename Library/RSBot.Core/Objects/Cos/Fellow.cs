﻿using RSBot.Core.Client.ReferenceObjects;
using RSBot.Core.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSBot.Core.Objects.Cos
{
    public class Fellow : Cos
    {
        /// <summary>
        /// Gets or sets the satiety.
        /// </summary>
        /// <value>
        /// The satiety.
        /// </value>
        public int Satiety { get; set; }

        /// <summary>
        /// Gets or sets the stored sp.
        /// </summary>
        /// <value>
        /// The stored sp.
        /// </value>
        public int StoredSp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this cos is offensive
        /// </summary>
        /// <value>
        /// <c>true</c> if this cos is offensive; otherwise, <c>false</c>.
        /// </value>
        public bool IsCounterAttackOn => (Settings & 0x10) == 0x10;

        /// <summary>
        /// Gets the maximum experience.
        /// </summary>
        /// <value>
        /// The maximum experience.
        /// </value>
        public override long MaxExperience => Game.ReferenceManager.GetRefLevel(Level).Exp_C_Pet2;

        /// <summary>
        /// Gets the maximum stored sp.
        /// </summary>
        /// <value>
        /// The maximum stored sp.
        /// </value>
        public int MaxStoredSp => Game.ReferenceManager.GetRefLevel(Level).StoredSp_Pet2;

        public override bool UseHealthPotion()
        {
            var usingItem = Game.Player.Inventory.GetItem(p => p.Record.IsFellowHpPotion);
            if (usingItem == null)
                return false;

            usingItem.UseFor(UniqueId);

            return true;
        }

        /// <summary>
        /// Uses the hunger potion.
        /// </summary>
        /// <returns></returns>
        public bool UseSatietyPotion()
        {
            var item = Game.Player.Inventory.GetItem(p => p.Record.IsPet2SatietyPotion &&
                (p.Record.ReqLevelType1 == ObjectReqLevelType.None || (p.Record.ReqLevel1 >= Level && p.Record.ReqLevel2 <= Level)));
            if (item == null)
                return false;

            item.UseFor(UniqueId);

            return true;
        }

        public override bool UseBadStatusPotion()
        {
            return false;
        }

        public override void Deserialize(Packet packet)
        {
            Experience = packet.ReadLong();
            Level = packet.ReadByte();
            Satiety = packet.ReadInt();
            var unknown1 = packet.ReadUShort();
            StoredSp = packet.ReadInt();
            var unknown2 = packet.ReadInt();
            Settings = packet.ReadInt();
            Name = packet.ReadString();
            Inventory = new InventoryItemCollection(packet);

            if (string.IsNullOrWhiteSpace(Name))
                Name = LanguageManager.GetLangBySpecificKey("RSBot", "LabelPetName");
        }
    }
}
