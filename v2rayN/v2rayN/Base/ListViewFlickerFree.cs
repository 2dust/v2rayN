using System.Drawing;
using System.Windows.Forms;

namespace v2rayN.Base
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
                int MaxWidth = 0;
                Graphics graphics = this.CreateGraphics();

                string str;
                int width;

                this.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

                for (int i = 0; i < this.Columns.Count; i++)
                {
                    ColumnHeader c = this.Columns[i];
                    str = c.Text;
                    MaxWidth = c.Width;

                    foreach (ListViewItem item in this.Items)
                    {
                        Font font = item.SubItems[i].Font;
                        str = item.SubItems[i].Text;
                        width = (int)graphics.MeasureString(str, font).Width;
                        if (width > MaxWidth)
                        {
                            MaxWidth = width;
                        }
                    }
                    c.Width = MaxWidth;
                }
            }
            catch { }
        }
    }
}