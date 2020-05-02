﻿using System;
using System.Collections.Generic;
using Intersect.Enums;
using Intersect.GameObjects;

namespace Intersect.Client.Items
{

    public class Item
    {

        public Guid? BagId;

        public Guid ItemId;

        public int Quantity;

        public int[] StatBuffs = new int[(int) Stats.StatCount];

		public Dictionary<string, int> Tags = new Dictionary<string, int>();

		public Item()
        {
        }

        public ItemBase Base => ItemBase.Get(ItemId);

        public void Load(Guid id, int quantity, Guid? bagId, int[] statBuffs, Dictionary<string, int> tags)
        {
            ItemId = id;
            Quantity = quantity;
            BagId = bagId;
            StatBuffs = statBuffs;
			Tags = tags;
			if (Tags == null)
			{
				Tags = new Dictionary<string, int>();
			}
        }

        public Item Clone()
        {
            var newItem = new Item()
            {
                ItemId = ItemId,
                Quantity = Quantity,
                BagId = BagId
            };

            for (var i = 0; i < (int) Stats.StatCount; i++)
            {
                newItem.StatBuffs[i] = StatBuffs[i];
			}
			foreach (KeyValuePair<string, int> tag in Tags)
			{
				newItem.Tags.Add(tag.Key, tag.Value);
			}

			return newItem;
        }

    }

}
