﻿using System;
using System.Collections.Generic;

using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.GameObjects;

namespace Intersect.Client.Interface.Game
{
	public class SendMailBoxWindow
	{
		private WindowControl mSendMailBoxWindow;

		private Label mTo;
		private TextBox mToTextbox;
		private Label mTitle;
		private TextBox mTitleTextbox;
		private Label mMessage;
		private TextBox mMsgTextbox;

		private Label mItem;
		private ComboBox mItemComboBox;
		private Label mQuantity;
		private TextBoxNumeric mQuantityTextBoxNumeric;

		private Button mSendButton;
		private Button mCloseButton;


		public SendMailBoxWindow(Canvas gameCanvas)
		{
			mSendMailBoxWindow = new WindowControl(gameCanvas, Strings.MailBox.sendtitle, false, "SendMailBoxWindow");
			mSendMailBoxWindow.DisableResizing();
			Interface.InputBlockingElements.Add(mSendMailBoxWindow);

			mTo = new Label(mSendMailBoxWindow, "To")
			{
				Text = Strings.MailBox.mailto
			};
			mToTextbox = new TextBox(mSendMailBoxWindow, "ToTextbox");
			Interface.FocusElements.Add(mToTextbox);

			mTitle = new Label(mSendMailBoxWindow, "Title")
			{
				Text = Strings.MailBox.mailtitle
			};
			mTitleTextbox = new TextBox(mSendMailBoxWindow, "TitleTextbox");
			mTitleTextbox.SetMaxLength(20);
			Interface.FocusElements.Add(mTitleTextbox);

			mMessage = new Label(mSendMailBoxWindow, "Message")
			{
				Text = Strings.MailBox.mailmsg
			};
			mMsgTextbox = new TextBox(mSendMailBoxWindow, "MsgTextbox");
			mMsgTextbox.SetMaxLength(255);
			Interface.FocusElements.Add(mMsgTextbox);

			mItem = new Label(mSendMailBoxWindow, "Item")
			{
				Text = Strings.MailBox.mailitem
			};
			mItemComboBox = new ComboBox(mSendMailBoxWindow, "ItemComboBox");
			mItemComboBox.ItemSelected += Item_SelectedComboBox;
			Interface.FocusElements.Add(mItemComboBox);

			mQuantity = new Label(mSendMailBoxWindow, "Quantity")
			{
				Text = Strings.MailBox.mailquantity
			};
			mQuantity.IsHidden = true;
			mQuantityTextBoxNumeric = new TextBoxNumeric(mSendMailBoxWindow, "QuantityTextBoxNumeric");
			mQuantityTextBoxNumeric.IsHidden = true;
			mQuantityTextBoxNumeric.TextChanged += Quantity_ChangeTextBoxNumeric;
			Interface.FocusElements.Add(mQuantityTextBoxNumeric);


			mSendButton = new Button(mSendMailBoxWindow, "SendButton");
			mSendButton.SetText(Strings.MailBox.send);
			mSendButton.Clicked += SendButton_Clicked;

			mCloseButton = new Button(mSendMailBoxWindow, "CloseButton");
			mCloseButton.SetText(Strings.MailBox.close);
			mCloseButton.Clicked += CloseButton_Clicked;

			mSendMailBoxWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
		}

		public void UpdateItemList()
		{
			mItemComboBox.DeleteAll();
			mItemComboBox.AddItem(Strings.MailBox.itemnone, "", System.Guid.Empty);
			foreach (Items.Item item in Globals.Me.Inventory)
			{
				if (item.ItemId != Guid.Empty)
				{
					mItemComboBox.AddItem(item.Base.Name, "", item.ItemId);
				}
			}
		}

		private void Quantity_ChangeTextBoxNumeric(Base sender, EventArgs e)
		{
			var item = mItemComboBox.SelectedItem;
			Guid itemID = (Guid)(item.UserData);
			int val = UpdateQuantity(itemID, (int)mQuantityTextBoxNumeric.Value);
			mQuantity.Text = $"{Strings.MailBox.mailquantity}: {val}";
		}

		private void Item_SelectedComboBox(Base sender, ItemSelectedEventArgs e)
		{
			var item = mItemComboBox.SelectedItem;
			Guid itemID = (Guid)(item.UserData);
			if (itemID == Guid.Empty)
			{
				mQuantityTextBoxNumeric.Value = 0;
				mQuantityTextBoxNumeric.IsHidden = true;
				mQuantity.IsHidden = true;
			}
			else
			{
				var ibase = ItemBase.Get(itemID);
				if (ibase != null)
				{
					mQuantityTextBoxNumeric.Value = 1;
					mQuantityTextBoxNumeric.IsHidden = !ibase.IsStackable;
					mQuantity.IsHidden = !ibase.IsStackable;
				}
			}
		}

		private int UpdateQuantity(Guid itemID, int quantity)
		{
			if (quantity < 0)
			{
				quantity = 0;
			}
			foreach (Items.Item it in Globals.Me.Inventory)
			{
				if (it.ItemId == itemID)
				{
					if (quantity > it.Quantity)
					{
						quantity = it.Quantity;
					}
					if (it.Base.IsStackable == false)
					{
						return 1;
					}
					return quantity;
				}
			}
			return 0;
		}

		void SendButton_Clicked(Base sender, ClickedEventArgs arguments)
		{
			if (mToTextbox.Text.Trim().Length <= 3 || mTitleTextbox.Text.Trim().Length <= 3)
			{
				return;
			}
			var item = mItemComboBox.SelectedItem;
			Guid itemID = (Guid)(item.UserData);
			int quantity = 0;
			if (itemID != Guid.Empty)
			{
				quantity = UpdateQuantity(itemID, (int)mQuantityTextBoxNumeric.Value);
				if (quantity == 0)
				{
					itemID = Guid.Empty;
				}
			}
			PacketSender.SendMail(mToTextbox.Text, mTitleTextbox.Text, mMsgTextbox.Text, itemID, quantity);
		}

		void CloseButton_Clicked(Base sender, ClickedEventArgs arguments)
		{
			PacketSender.SendCloseMail();
		}

		public void Close()
		{
			mSendMailBoxWindow.Close();
		}

		public bool IsVisible()
		{
			return !mSendMailBoxWindow.IsHidden;
		}

		public void Hide()
		{
			mSendMailBoxWindow.IsHidden = true;
		}
	}
}
