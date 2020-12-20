using System;
using System.Windows.Forms;
using v2rayN.Mode;

namespace v2rayN.Forms
{
    public partial class BaseServerForm : BaseForm
    {
        public int EditIndex { get; set; }
        protected VmessItem vmessItem = null;

        public BaseServerForm()
        {
            InitializeComponent();
        }
 
    }
}
