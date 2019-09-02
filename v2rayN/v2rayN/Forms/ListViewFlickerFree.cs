using System.Drawing;
using System.Windows.Forms;

namespace v2rayN.Forms
{
    class ListViewFlickerFree : ListView
    {
        public ListViewFlickerFree()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.AllPaintingInWmPaint
                    , true);
            UpdateStyles();
        }


        public void AutoResizeColumns()
        {
            try
            {
                int count = this.Columns.Count;
                int MaxWidth = 0;
                Graphics graphics = this.CreateGraphics();
                Font font = this.Font;
                ListView.ListViewItemCollection items = this.Items;

                string str;
                int width;

                this.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

                for (int i = 0; i < count; i++)
                {
                    str = this.Columns[i].Text;
                    MaxWidth = this.Columns[i].Width;

                    foreach (ListViewItem item in items)
                    {
                        str = item.SubItems[i].Text;
                        width = (int)graphics.MeasureString(str, font).Width;
                        if (width > MaxWidth)
                        {
                            MaxWidth = width;
                        }
                    }
                    if (i == 0)
                    {
                        this.Columns[i].Width = MaxWidth;
                    }
                    this.Columns[i].Width = MaxWidth;
                }
            }
            catch { }
        }
    }
}