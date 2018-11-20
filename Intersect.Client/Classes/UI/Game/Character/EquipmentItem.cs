﻿using System;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Networking;
using Intersect.GameObjects;

namespace Intersect.Client.UI.Game.Character
{
    public class EquipmentItem
    {
        private WindowControl mCharacterWindow;
        private Guid mCurrentItemId;
        private ItemDescWindow mDescWindow;
        private int[] mStatBoost = new int[Options.MaxStats];
        private bool mTexLoaded;
        public ImagePanel ContentPanel;
        private int mYindex;
        public ImagePanel Pnl;

        public EquipmentItem(int index, WindowControl characterWindow)
        {
            mYindex = index;
            mCharacterWindow = characterWindow;
        }

        public void Setup()
        {
            Pnl.HoverEnter += pnl_HoverEnter;
            Pnl.HoverLeave += pnl_HoverLeave;
            Pnl.RightClicked += pnl_RightClicked;

            ContentPanel = new ImagePanel(Pnl, "EquipmentIcon");
            Pnl.SetToolTipText(Options.EquipmentSlots[mYindex]);
        }

        void pnl_RightClicked(Base sender, ClickedEventArgs arguments)
        {
            PacketSender.SendUnequipItem(mYindex);
        }

        void pnl_HoverLeave(Base sender, EventArgs arguments)
        {
            if (mDescWindow != null)
            {
                mDescWindow.Dispose();
                mDescWindow = null;
            }
        }

        void pnl_HoverEnter(Base sender, EventArgs arguments)
        {
            if (Globals.InputManager.MouseButtonDown(GameInput.MouseButtons.Left))
            {
                return;
            }
            if (mDescWindow != null)
            {
                mDescWindow.Dispose();
                mDescWindow = null;
            }

            var item = ItemBase.Get(mCurrentItemId);
            if (item == null) return;
            mDescWindow = new ItemDescWindow(item, 1, mCharacterWindow.X - 255, mCharacterWindow.Y, mStatBoost, item.Name);
        }

        public FloatRect RenderBounds()
        {
            FloatRect rect = new FloatRect()
            {
                X = Pnl.LocalPosToCanvas(new Framework.GenericClasses.Point(0, 0)).X,
                Y = Pnl.LocalPosToCanvas(new Framework.GenericClasses.Point(0, 0)).Y,
                Width = Pnl.Width,
                Height = Pnl.Height
            };
            return rect;
        }

        public void Update(Guid currentItemId, int[] statBoost)
        {
            if (currentItemId != mCurrentItemId || !mTexLoaded)
            {
                mCurrentItemId = currentItemId;
                mStatBoost = statBoost;
                var item = ItemBase.Get(mCurrentItemId);
                if (item != null)
                {
                    GameTexture itemTex = Globals.ContentManager.GetTexture(GameContentManager.TextureType.Item,
                        item.Icon);
                    if (itemTex != null)
                    {
                        ContentPanel.Show();
                        ContentPanel.Texture = itemTex;
                    }
                    else
                    {
                        ContentPanel.Hide();
                    }
                }
                else
                {
                    ContentPanel.Hide();
                }
                mTexLoaded = true;
            }
        }
    }
}