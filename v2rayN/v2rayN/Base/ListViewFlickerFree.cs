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

        public void RegisterDragEvent(Action<int, int> update)
        {
            _updateFunc = update;
            AllowDrop = true;

            ItemDrag += lv_ItemDrag;
            DragDrop += lv_DragDrop;
            DragEnter += lv_DragEnter;
            DragOver += lv_DragOver;
            DragLeave += lv_DragLeave;
        }

        private void lv_DragDrop(object sender, DragEventArgs e)
        {
            int targetIndex = InsertionMark.Index;
            if (targetIndex == -1)
            {
                return;
            }
            if (InsertionMark.AppearsAfterItem)
            {
                targetIndex++;
            }


            if (SelectedIndices.Count <= 0)
            {
                return;
            }

            _updateFunc(SelectedIndices[0], targetIndex);

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
            InsertionMark.Index = -1;
        }

        private void lv_DragOver(object sender, DragEventArgs e)
        {
            Point targetPoint = PointToClient(new Point(e.X, e.Y));
            int targetIndex = InsertionMark.NearestIndex(targetPoint);

            if (targetIndex > -1)
            {
                Rectangle itemBounds = GetItemRect(targetIndex);
                EnsureVisible(targetIndex);

                if (targetPoint.Y > itemBounds.Top + (itemBounds.Height / 2))
                {
                    InsertionMark.AppearsAfterItem = true;
                }
                else
                {
                    InsertionMark.AppearsAfterItem = false;
                }
            }
            InsertionMark.Index = targetIndex;
        }

        private void lv_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(e.Item, DragDropEffects.Move);
            InsertionMark.Index = -1;
        }
    }
}