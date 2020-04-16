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
                this.SuspendLayout();
                Graphics graphics = this.CreateGraphics();

                // 原生 ColumnHeaderAutoResizeStyle.ColumnContent 将忽略列头宽度
                this.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

                for (int i = 0; i < this.Columns.Count; i++)
                {
                    ColumnHeader c = this.Columns[i];
                    int cWidth = c.Width;
                    string MaxStr = "";
                    Font font = this.Items[0].SubItems[0].Font;

                    foreach (ListViewItem item in this.Items)
                    {
                        // 整行视作相同字形，不单独计算每个单元格
                        font = item.SubItems[i].Font;
                        string str = item.SubItems[i].Text;
                        if (str.Length > MaxStr.Length) // 未考虑非等宽问题
                            MaxStr = str;
                    }
                    int strWidth = (int)graphics.MeasureString(MaxStr, font).Width;
                    c.Width = System.Math.Max(cWidth, strWidth);
                }
                this.ResumeLayout();
            }
            catch { }
        }
    }
}