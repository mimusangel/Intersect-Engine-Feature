﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hjg.Pngcs;
using Intersect_Editor.Forms;
using Intersect_Library;
using Intersect_Library.GameObjects.Maps.MapList;
using Microsoft.Xna.Framework.Graphics;
using Color = System.Drawing.Color;

namespace Intersect_Editor.Classes.Maps
{
    public class MapGrid
    {
        public int GridWidth = 50;
        public int GridHeight = 50;
        public MapGridItem[,] Grid;
        public bool Loaded = false;

        public float Zoom = 1;
        public int TileWidth = 0;
        public int TileHeight = 0;
        public Rectangle ViewRect = new Rectangle();
        public Rectangle ContentRect = new Rectangle();

        private float MaxZoom = 1f;
        private float MinZoom = 0f;
        private bool sizeChanged = true;

        private object texLock = new object();
        private List<Texture2D> textures = new List<Texture2D>();
        private List<Texture2D> freeTextures = new List<Texture2D>();
        private List<MapGridItem> toLoad = new List<MapGridItem>();
        private bool createTextures = false;
        private Thread workerThread;
        public bool ShowLines = true;

        private int _currentCellX = -1;
        private int _currentCellY = -1;
        private MapGridItem _contextMap = null;

        private ToolStripMenuItem _dropDownLinkItem;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _dropDownUnlinkItem;
        private ToolStripMenuItem _recacheMapItem;

        private List<int> LinkMaps = new List<int>();

        public MapGrid(ToolStripMenuItem dropDownItem, ToolStripMenuItem dropDownUnlink,ToolStripMenuItem recacheItem, ContextMenuStrip contextMenu)
        {
            workerThread = new Thread(AsyncLoadingThread);
            workerThread.Start();
            _dropDownLinkItem = dropDownItem;
            _dropDownLinkItem.Click += LinkMapItem_Click;
            _contextMenu = contextMenu;
            _dropDownUnlinkItem = dropDownUnlink;
            _dropDownUnlinkItem.Click += UnlinkMapItem_Click;
            _recacheMapItem = recacheItem;
            _recacheMapItem.Click += _recacheMapItem_Click;
        }


        private void AsyncLoadingThread()
        {
            while (!Globals.ClosingEditor)
            {
                lock (texLock)
                {
                    if (toLoad.Count > 0 && freeTextures.Count > 0)
                    {
                        var itm = toLoad[0];
                        toLoad.RemoveAt(0);
                        var tex = freeTextures[0];
                        freeTextures.RemoveAt(0);

                        var texData = Database.LoadMapCache(itm.mapnum, itm.revision, TileWidth, TileHeight);
                        if (texData != null)
                        {
                            tex.SetData(texData);
                            itm.tex = tex;
                        }
                        else
                        {
                            freeTextures.Add(tex);
                        }
                    }
                }
            }
        }


        public void Load(ByteBuffer bf)
        {
            Loaded = false;
            List<int> gridMaps = new List<int>();
            GridWidth = (int)bf.ReadLong();
            GridHeight = (int)bf.ReadLong();
            lock (texLock)
            {
                UnloadTextures();
            }
            Grid = new MapGridItem[GridWidth, GridHeight];
            for (int x = -1; x <= GridWidth; x++)
            {
                for (int y = -1; y <= GridHeight; y++)
                {
                    if (y == -1 || y == GridHeight || x == -1 || x == GridWidth)
                    {

                    }
                    else
                    {
                        int num = bf.ReadInteger();
                        if (num == -1)
                        {
                            Grid[x, y] = new MapGridItem(num);
                        }
                        else
                        {
                            Grid[x, y] = new MapGridItem(num, bf.ReadString(), bf.ReadInteger());
                            gridMaps.Add(Grid[x, y].mapnum);
                        }
                    }
                }
            }
            //Get a list of maps -- if they are not in this grid.
            LinkMaps.Clear();
            for (int i = 0; i < MapList.GetOrderedMaps().Count; i++)
            {
                if (!gridMaps.Contains(MapList.GetOrderedMaps()[i].MapNum))
                {
                    LinkMaps.Add(MapList.GetOrderedMaps()[i].MapNum);
                }
            }
            MaxZoom = 1f; //Real Size
            Zoom = MinZoom;
            TileWidth = (int)(Options.TileWidth * Options.MapWidth * Zoom);
            TileHeight = (int)(Options.TileHeight * Options.MapHeight * Zoom);
            ContentRect = new Rectangle(ViewRect.Width/2-(TileWidth * (GridWidth + 2))/2, ViewRect.Height/2 - (TileHeight * (GridHeight + 2))/2, TileWidth * (GridWidth + 2), TileHeight * (GridHeight + 2));
            createTextures = true;
            Loaded = true;
        }

        public void DoubleClick(int x, int y)
        {
            for (int x1 = 1; x1 < GridWidth + 1; x1++)
            {
                for (int y1 = 1; y1 < GridHeight + 1; y1++)
                {
                    if (new Rectangle(ContentRect.X + x1 * TileWidth, ContentRect.Y + y1 * TileHeight, TileWidth, TileHeight)
                        .Contains(new System.Drawing.Point(x, y)))
                    {
                        if (Grid[x1 - 1, y1 - 1].mapnum > -1)
                        {
                            Globals.MainForm.EnterMap(Grid[x1 - 1, y1 - 1].mapnum);
                            Globals.MapEditorWindow.Select();
                        }
                    }
                }
            }
        }

        public void ScreenshotWorld()
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Png Image|*.png|JPeg Image|*.jpg|Bitmap Image|*.bmp|Gif Image|*.gif";
            fileDialog.Title = "Save a screenshot of the world";
            fileDialog.ShowDialog();
            if (fileDialog.FileName != "")
            {
                if (
                    MessageBox.Show(
                        "Are you sure you want to save a screenshot of your world to a file? This could take several minutes!",
                        "Save Screenshot?", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                {
                    FetchMissingPreviews(false);
                    Globals.PreviewProgressForm = new frmProgress();
                    Globals.PreviewProgressForm.SetTitle("Saving Screenshot");
                    Thread screenShotThread = new Thread(() => ScreenshotWorld(fileDialog.FileName));
                    screenShotThread.Start();
                    Globals.PreviewProgressForm.ShowDialog();
                }
            }
        }

        void ScreenshotWorld(string filename)
        {
            int rowSize = (Options.TileHeight * Options.MapHeight);
            int colSize = (Options.MapWidth * Options.TileWidth);
            int cols = colSize * (GridWidth);
            int rows = rowSize * (GridHeight);
            Bitmap tmpBitmap = new Bitmap(colSize, rowSize);
            Graphics g = Graphics.FromImage(tmpBitmap);
            var png = new PngWriter(new FileStream(filename, FileMode.OpenOrCreate), new ImageInfo(cols, rows, 16, true));
            var pngReaderDict = new Dictionary<int, Bitmap>();
            var cacheRow = 0;
            //Generate one row at a time.
            for (int y = 0; y < rows; y++)
            {
                int gridRow = (int)Math.Floor(y / (double)rowSize);
                if (gridRow != cacheRow)
                {
                    foreach (var cache in pngReaderDict)
                    {
                        cache.Value.Dispose();
                    }
                    pngReaderDict.Clear();
                    cacheRow = gridRow;
                }
                byte[] row = new byte[png.ImgInfo.Cols * 4];
                for (int x = 0; x < GridWidth; x++)
                {
                    int gridCol = x;

                    MapGridItem item = Grid[gridCol, gridRow];
                    if (item.mapnum >= 0)
                    {
                        Bitmap reader = null;
                        if (pngReaderDict.ContainsKey(item.mapnum))
                        {
                            reader = pngReaderDict[item.mapnum];
                        }
                        else
                        {
                            var data = Database.LoadMapCacheRaw(item.mapnum, item.revision);
                            if (data != null)
                            {
                                reader = new Bitmap(new MemoryStream(data));
                                pngReaderDict.Add(item.mapnum, reader);
                            }
                        }

                        if (reader != null)
                        {
                            var rowNum = y - (gridRow * rowSize);
                            //Get the pixel color we need
                            for (int x1 = (x) * colSize; x1 < (x) * colSize + colSize; x1++)
                            {
                                Color clr = reader.GetPixel(x1 - (x)*colSize, rowNum);// Color.FromArgb(ImageLineHelper.GetPixelToARGB8(line, x1 - (x) * colSize));
                                row[x1 * 4] = clr.R;
                                row[x1 * 4 + 1] = clr.G;
                                row[x1 * 4 + 2] = clr.B;
                                row[x1 * 4 + 3] = clr.A;
                            }
                        }
                        else
                        {
                            for (int x1 = (x) * colSize; x1 < (x) * colSize + colSize; x1++)
                            {
                                row[x1 * 4] = 0;
                                row[x1 * 4 + 1] = 255;
                                row[x1 * 4 + 2] = 0;
                                row[x1 * 4 + 3] = 255;
                            }
                        }
                    }
                    else
                    {
                        for (int x1 = (x) * colSize; x1 < (x) * colSize + colSize; x1++)
                        {
                            row[x1 * 4] = Color.Gray.R;
                            row[x1 * 4 + 1] = Color.Gray.G;
                            row[x1 * 4 + 2] = Color.Gray.B;
                            row[x1 * 4 + 3] = Color.Gray.A;
                        }
                    }
                }
                png.WriteRowByte(row, y);
                Globals.PreviewProgressForm.SetProgress("Saving Row: " + y + "/" + rows, (int)((y / (float)rows) * 100), false);
                Application.DoEvents();
            }
            png.End();
            Globals.PreviewProgressForm.NotifyClose();
        }

        public bool Contains(int mapNum)
        {
            if (Grid != null && Loaded)
            {
                lock (texLock)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        for (int y = 0; y < GridHeight; y++)
                        {
                            if (Grid[x, y].mapnum == mapNum)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void ResetForm()
        {
            lock (texLock)
            {
                UnloadTextures();
                createTextures = true;
            }
        }

        public void FetchMissingPreviews(bool clearAllFirst)
        {
            List<int> maps = new List<int>();
            if (clearAllFirst)
            {
                if (MessageBox.Show("Are you sure you want to clear the existing previews and fetch previews for each map on this grid? This could take several minutes based on the number of maps in this grid!",
                        "Fetch Preview?", MessageBoxButtons.YesNo) != System.Windows.Forms.DialogResult.Yes) return;
                if (
                    MessageBox.Show("Use your current settings with the map cache? (No to disable lighting/fogs/other effects)", "Map Cache Options",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Database.GridHideOverlay = EditorGraphics.HideOverlay;
                    Database.GridHideDarkness = EditorGraphics.HideDarkness;
                    Database.GridHideFog = EditorGraphics.HideFog;
                    Database.GridHideResources = EditorGraphics.HideResources;
                    if (EditorGraphics.LightColor != null)
                    {
                        Database.GridLightColor = System.Drawing.Color.FromArgb(EditorGraphics.LightColor.A, EditorGraphics.LightColor.R, EditorGraphics.LightColor.G, EditorGraphics.LightColor.B).ToArgb();
                    }
                    else
                    {
                        Database.GridLightColor = System.Drawing.Color.FromArgb(255,255,255,255).ToArgb();
                    }
                }
                else
                {
                    Database.GridHideOverlay = true;
                    Database.GridHideDarkness = true;
                    Database.GridHideFog = true;
                    Database.GridHideResources = false;
                    Database.GridLightColor = System.Drawing.Color.White.ToArgb();
                }
                Database.SaveGridOptions();
                Database.ClearAllMapCache();

            }
            //Get a list of maps without images.
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    if (Grid[x, y].mapnum > -1 && Database.LoadMapCacheLegacy(Grid[x, y].mapnum,
                        Grid[x, y].revision) == null)
                    {
                        maps.Add(Grid[x, y].mapnum);
                    }
                }
            }
            if (maps.Count > 0)
            {
                if (clearAllFirst || MessageBox.Show("Are you sure you want to fetch previews for each map? This could take several minutes based on the number of maps in this grid!", "Fetch Preview?", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                {
                    Globals.FetchingMapPreviews = true;
                    Globals.PreviewProgressForm = new frmProgress();
                    Globals.PreviewProgressForm.SetTitle("Fetching Map Previews");
                    Globals.PreviewProgressForm.SetProgress("Fetching Maps: 0/" + maps.Count, 0, false);
                    Globals.FetchCount = maps.Count;
                    Globals.MapsToFetch = maps;
                    for (int i = 0; i < maps.Count; i++)
                    {
                        PacketSender.SendNeedMap(maps[i]);
                    }
                    Globals.PreviewProgressForm.ShowDialog();
                }
            }
        }

        public void RightClickGrid(int x, int y, Panel mapGridView)
        {
            for (int x1 = 0; x1 < GridWidth + 2; x1++)
            {
                for (int y1 = 0; y1 < GridHeight + 2; y1++)
                {
                    if (new Rectangle(ContentRect.X + x1 * TileWidth, ContentRect.Y + y1 * TileHeight, TileWidth, TileHeight)
                            .Contains(new System.Drawing.Point(x, y)))
                    {
                        _currentCellX = x1;
                        _currentCellY = y1;
                        if (_currentCellX >= 0 && _currentCellY >= 0)
                        {
                            if (_currentCellX >= 0 && _currentCellY >= 0 && _currentCellX - 1 <= GridWidth &&
                                _currentCellY - 1 <= GridHeight)
                            {
                                if (_currentCellX == 0 || _currentCellY == 0 || _currentCellX - 1 == GridWidth ||
                                    _currentCellY - 1 == GridHeight || Grid[_currentCellX - 1, _currentCellY - 1].mapnum <= -1)
                                {
                                    int adjacentMap = -1;
                                    //Check Left
                                    if (_currentCellX > 1 && _currentCellY != 0 && _currentCellY - 1 < GridHeight)
                                    {
                                        if (Grid[_currentCellX - 2, _currentCellY - 1].mapnum > -1)
                                            adjacentMap = Grid[_currentCellX - 2, _currentCellY - 1].mapnum;
                                    }
                                    //Check Right
                                    if (_currentCellX < GridWidth && _currentCellY != 0 && _currentCellY - 1 < GridHeight)
                                    {
                                        if (Grid[_currentCellX, _currentCellY - 1].mapnum > -1)
                                            adjacentMap = Grid[_currentCellX, _currentCellY - 1].mapnum;
                                    }
                                    //Check Up
                                    if (_currentCellX != 0 && _currentCellY > 1 && _currentCellX - 1 < GridWidth)
                                    {
                                        if (Grid[_currentCellX - 1, _currentCellY - 2].mapnum > -1)
                                            adjacentMap = Grid[_currentCellX - 1, _currentCellY - 2].mapnum;
                                    }
                                    //Check Down
                                    if (_currentCellX != 0 && _currentCellY < GridHeight && _currentCellX - 1 < GridWidth)
                                    {
                                        if (Grid[_currentCellX - 1, _currentCellY].mapnum > -1)
                                            adjacentMap = Grid[_currentCellX - 1, _currentCellY].mapnum;
                                    }
                                    if (adjacentMap > -1)
                                    {
                                        _contextMenu.Show(mapGridView, new System.Drawing.Point(x, y));
                                        _dropDownUnlinkItem.Visible = false;
                                        _dropDownLinkItem.Visible = true;
                                        _recacheMapItem.Visible = false;
                                    }
                                }
                                else
                                {
                                    _contextMap = Grid[_currentCellX - 1, _currentCellY - 1];
                                    _contextMenu.Show(mapGridView, new System.Drawing.Point(x, y));
                                    _dropDownUnlinkItem.Visible = true;
                                    _recacheMapItem.Visible = true;
                                    _dropDownLinkItem.Visible = false;
                                }
                            }
                        }
                        return;
                    }
                }
            }
            _currentCellX = -1;
            _currentCellY = -1;
        }

        private void UnlinkMapItem_Click(object sender, EventArgs e)
        {
            if (_contextMap != null && _contextMap.mapnum > -1)
            {
                if (MessageBox.Show("Are you sure you want to unlink map " + _contextMap.name + "?", "Unlink Map", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    PacketSender.SendUnlinkMap(_contextMap.mapnum);
            }

        }

        private void _recacheMapItem_Click(object sender, EventArgs e)
        {
            if (_contextMap != null && _contextMap.mapnum > -1)
            {
                //Fetch and screenshot this singular map
                Database.SaveMapCache(_contextMap.mapnum, _contextMap.revision, null);
                if (MapInstance.GetMap(_contextMap.mapnum) != null) MapInstance.GetMap(_contextMap.mapnum).Delete();
                Globals.MapsToFetch = new List<int>() { _contextMap.mapnum };
                PacketSender.SendNeedMap(_contextMap.mapnum);
            }
        }

        private void LinkMapItem_Click(object sender, EventArgs e)
        {
            frmWarpSelection frmWarpSelection = new frmWarpSelection();
            frmWarpSelection.InitForm(false, LinkMaps);
            frmWarpSelection.ShowDialog();
            if (frmWarpSelection.GetResult())
            {
                //Make sure the selected tile is adjacent to a map
                int linkMap = frmWarpSelection.GetMap();
                int adjacentMap = -1;
                //Check Left
                if (_currentCellX > 1 && _currentCellY != 0 && _currentCellY - 1 < GridHeight)
                {
                    if (Grid[_currentCellX - 2, _currentCellY - 1].mapnum > -1)
                        adjacentMap = Grid[_currentCellX - 2, _currentCellY - 1].mapnum;
                }
                //Check Right
                if (_currentCellX < GridWidth && _currentCellY != 0 && _currentCellY - 1 < GridHeight)
                {
                    if (Grid[_currentCellX, _currentCellY - 1].mapnum > -1)
                        adjacentMap = Grid[_currentCellX, _currentCellY - 1].mapnum;
                }
                //Check Up
                if (_currentCellX != 0 && _currentCellY > 1 && _currentCellX - 1 < GridWidth)
                {
                    if (Grid[_currentCellX - 1, _currentCellY - 2].mapnum > -1)
                        adjacentMap = Grid[_currentCellX - 1, _currentCellY - 2].mapnum;
                }
                //Check Down
                if (_currentCellX != 0 && _currentCellY < GridHeight && _currentCellX - 1 < GridWidth)
                {
                    if (Grid[_currentCellX - 1, _currentCellY].mapnum > -1)
                        adjacentMap = Grid[_currentCellX - 1, _currentCellY].mapnum;
                }
                if (adjacentMap != -1)
                {
                    PacketSender.SendLinkMap(adjacentMap, linkMap, _currentCellX - 1, _currentCellY - 1);
                }
            }
        }

        public void Update(Microsoft.Xna.Framework.Rectangle panelBounds)
        {
            MinZoom = Math.Min(panelBounds.Width / (float)(Options.TileWidth * Options.MapWidth * (GridWidth + 2)), panelBounds.Height / (float)(Options.TileHeight * Options.MapHeight * (GridHeight + 2))) / 2f; //Gotta calculate 
            if (Zoom < MinZoom)
            {
                Zoom = MinZoom * 2;
                TileWidth = (int)(Options.TileWidth * Options.MapWidth * Zoom);
                TileHeight = (int)(Options.TileHeight * Options.MapHeight * Zoom);
                ContentRect = new Rectangle(0, 0, TileWidth * (GridWidth + 2), TileHeight * (GridHeight + 2));
                lock (texLock)
                {
                    UnloadTextures();
                }
                createTextures = true;
            }
            ViewRect = new Rectangle(panelBounds.Left, panelBounds.Top, panelBounds.Width, panelBounds.Height);
            if (ContentRect.X + TileWidth > ViewRect.Width) ContentRect.X = ViewRect.Width - TileWidth;
            if (ContentRect.X + ContentRect.Width < TileWidth) ContentRect.X = -ContentRect.Width + TileWidth;
            if (ContentRect.Y + TileHeight > ViewRect.Height) ContentRect.Y = ViewRect.Height - TileHeight;
            if (ContentRect.Y + ContentRect.Height < TileHeight) ContentRect.Y = -ContentRect.Height + TileHeight;
            if (createTextures)
            {
                CreateTextures(panelBounds);
                createTextures = false;
            }
            if (Grid != null)
            {
                lock (texLock)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        for (int y = 0; y < GridHeight; y++)
                        {
                            //Figure out if this texture should be loaded
                            if (new Rectangle(ContentRect.X + (x + 1) * TileWidth, ContentRect.Y + (y + 1) * TileHeight, TileWidth,
                                TileHeight).IntersectsWith(ViewRect) && Grid[x, y] != null)
                            {
                                //if not loaded, add it to the queue
                                if ((Grid[x, y].tex == null || Grid[x, y].tex.IsDisposed) && Grid[x, y].mapnum != -1 && !toLoad.Contains(Grid[x, y]))
                                {
                                    toLoad.Add(Grid[x, y]);
                                }
                            }
                            else
                            {
                                //If loaded, kick it to the curb
                                if (toLoad.Contains(Grid[x, y])) toLoad.Remove(Grid[x, y]);
                                if (Grid[x, y].tex != null)
                                {
                                    freeTextures.Add(Grid[x, y].tex);
                                    Grid[x, y].tex = null;
                                }
                            }
                        }
                    }
                }
            }
        }

        public MapGridItem GetItemAt(int mouseX, int mouseY)
        {
            if (Loaded)
            {
                lock (texLock)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        for (int y = 0; y < GridHeight; y++)
                        {
                            //Figure out if this texture should be loaded
                            if (new Rectangle(ContentRect.X + (x + 1) * TileWidth, ContentRect.Y + (y + 1) * TileHeight, TileWidth,
                                TileHeight).Contains(new System.Drawing.Point(mouseX, mouseY)) && Grid[x, y] != null)
                            {
                                return Grid[x, y];
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void CreateTextures(Microsoft.Xna.Framework.Rectangle panelBounds)
        {
            lock (texLock)
            {
                int hCount = (int)Math.Ceiling((float)panelBounds.Width / TileWidth) + 2;
                int wCount = (int)Math.Ceiling((float)panelBounds.Height / TileHeight) + 2;
                for (int i = 0; i < hCount * wCount && i < GridWidth * GridHeight; i++)
                {
                    textures.Add(new Texture2D(EditorGraphics.GetGraphicsDevice(), TileWidth, TileHeight));
                }
                freeTextures.AddRange(textures.ToArray());
            }
        }

        private void UnloadTextures()
        {
            lock (texLock)
            {
                for (int i = 0; i < textures.Count; i++)
                {
                    textures[i].Dispose();
                }
                textures.Clear();
                freeTextures.Clear();
                if (Grid != null && Loaded)
                {
                    for (int x = 0; x < GridWidth; x++)
                    {
                        for (int y = 0; y < GridHeight; y++)
                        {
                            Grid[x, y].tex = null;
                        }
                    }
                }
            }
        }

        public void Move(int x, int y)
        {
            ContentRect.X -= x;
            ContentRect.Y -= y;
            if (ContentRect.X + TileWidth > ViewRect.Width) ContentRect.X = ViewRect.Width - TileWidth;
            if (ContentRect.X + ContentRect.Width < TileWidth) ContentRect.X = -ContentRect.Width + TileWidth;
            if (ContentRect.Y + TileHeight > ViewRect.Height) ContentRect.Y = ViewRect.Height - TileHeight;
            if (ContentRect.Y + ContentRect.Height < TileHeight) ContentRect.Y = -ContentRect.Height + TileHeight;
        }

        public void ZoomIn(int val, int mouseX, int mouseY)
        {
            int amt = val / 120;

            //Find the original tile we are hovering over
            var x1 = (double)Math.Min(ContentRect.Width, Math.Max(0, mouseX - ContentRect.X)) / (float)TileWidth;
            var y1 = (double)Math.Min(ContentRect.Height, Math.Max(0, mouseY - ContentRect.Y)) / (float)TileHeight;
            var prevZoom = Zoom;
            Zoom += .05f * amt;
            if (prevZoom != Zoom)
            {
                lock (texLock)
                {
                    UnloadTextures();
                }
                createTextures = true;
            }
            if (Zoom < MinZoom) Zoom = MinZoom;
            if (Zoom > MaxZoom) Zoom = MaxZoom;
            TileWidth = (int)(Options.TileWidth * Options.MapWidth * Zoom);
            TileHeight = (int)(Options.TileHeight * Options.MapHeight * Zoom);
            //were gonna get the X/Y of where the content rect would need so that the grid location that the mouse is hovering over would be center of the viewing rect
            //Get the current location of the mouse over the current content rectangle

            var x2 = (int)(x1 * TileWidth);
            var y2 = (int)(y1 * TileHeight);

            ContentRect = new Rectangle(-x2 + mouseX, -y2 + mouseY, TileWidth * (GridWidth + 2), TileHeight * (GridHeight + 2));

            //Lets go the extra mile to make sure our selected map is 
        }
    }

    public class MapGridItem
    {
        public string name { get; set; }
        public int revision { get; set; }
        public int mapnum { get; set; }
        public Texture2D tex { get; set; }
        public MapGridItem(int num, string name = "", int revision = 0)
        {
            this.mapnum = num;
            this.name = name;
            this.revision = revision;
        }
    }
}