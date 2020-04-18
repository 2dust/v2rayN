using System.Windows.Forms;

namespace v2rayN.Base
{

    // ref: https://stackoverflow.com/questions/1214289/how-do-i-sort-integers-in-a-listview
    class Sorter : System.Collections.IComparer
    {
        public int Column = 0;
        public int Sorting = 0;
        public int Compare(object x, object y) // IComparer Member
        {
            if (!(x is ListViewItem) || !(y is ListViewItem))
                return (0);
            
            ListViewItem l1 = (ListViewItem)x;
            ListViewItem l2 = (ListViewItem)y;

            int doIntSort = Sorting;
            if (doIntSort > 0) // Tag will be number
            {
                ulong.TryParse(l1.SubItems[Column].Tag?.ToString(), out ulong fl1);
                ulong.TryParse(l2.SubItems[Column].Tag?.ToString(), out ulong fl2);

                if (doIntSort == 1)
                {
                    return fl1.CompareTo(fl2);
                }
                else
                {
                    return fl2.CompareTo(fl1);
                }
            }
            else
            {
                string str1 = l1.SubItems[Column].Text;
                string str2 = l2.SubItems[Column].Text;

                if (doIntSort == -1)
                {
                    return str1.CompareTo(str2);
                }
                else
                {
                    return str2.CompareTo(str1);
                }
            }
        }
    }
}
