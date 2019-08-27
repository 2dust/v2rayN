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
    }
}
