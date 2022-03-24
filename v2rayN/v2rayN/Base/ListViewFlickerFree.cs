using System;
using System.Drawing;
using System.Windows.Forms;

namespace v2rayN.Base
{
    class ListViewFlickerFree : ListView
    {
        Action<int, int> _updateFunc;

        public ListViewFlickerFree()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.AllPaintingInWmPaint
                    , true);
            UpdateStyles();
        }

        public void RegisterDragEvent(Action<int, int> _update)
        {
            _updateFunc = _update;
            this.AllowDrop = true;

            this.ItemDrag += new ItemDragEventHandler(this.lv_ItemDrag);
            this.DragDrop += new DragEventHandler(this.lv_DragDrop);
            this.DragEnter += new DragEventHandler(this.lv_DragEnter);
            this.DragOver += new DragEventHandler(this.lv_DragOver);
            this.DragLeave += new EventHandler(this.lv_DragLeave);
        }

        private void lv_DragDrop(object sender, DragEventArgs e)
        {
            int targetIndex = this.InsertionMark.Index;
            if (targetIndex == -1)
            {
                return;
            }
            if (this.InsertionMark.AppearsAfterItem)
            {
                targetIndex++;
            }


            if (this.SelectedIndices.Count <= 0)
            {
                return;
            }

            _updateFunc(this.SelectedIndices[0], targetIndex);

            //ListViewItem draggedItem = (ListViewItem)e.Data.GetData(typeof(ListViewItem));
            //this.BeginUpdate();
            //this.Items.Insert(targetIndex, (ListViewItem)draggedItem.Clone());
            //this.Items.Remove(draggedItem);
            //this.EndUpdate();
        }


        private void lv_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }

        private void lv_DragLeave(object sender, EventArgs e)
        {
            this.InsertionMark.Index = -1;
        }

        private void lv_DragOver(object sender, DragEventArgs e)
        {
            Point targetPoint = this.PointToClient(new Point(e.X, e.Y));
            int targetIndex = this.InsertionMark.NearestIndex(targetPoint);

            if (targetIndex > -1)
            {
                Rectangle itemBounds = this.GetItemRect(targetIndex);
                this.EnsureVisible(targetIndex);

                if (targetPoint.Y > itemBounds.Top + (itemBounds.Height / 2))
                {
                    this.InsertionMark.AppearsAfterItem = true;
                }
                else
                {
                    this.InsertionMark.AppearsAfterItem = false;
                }
            }
            this.InsertionMark.Index = targetIndex;
        }

        private void lv_ItemDrag(object sender, ItemDragEventArgs e)
        {
            this.DoDragDrop(e.Item, DragDropEffects.Move);
            this.InsertionMark.Index = -1;
        }
    }
}