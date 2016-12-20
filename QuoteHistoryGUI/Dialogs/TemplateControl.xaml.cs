using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuoteHistoryGUI.Dialogs
{
    /// <summary>
    /// Interaction logic for TemplateControl.xaml
    /// </summary>
    public partial class TemplateControl : UserControl
    {
        public TemplateControl()
        {
            InitializeComponent();
        }

        public string GetTemplates(IEnumerable<Folder> selection)
        {
            string res = "";
            foreach (var sel in selection)
            {
                string path = "";
                var curSel = sel;
                while (curSel != null)
                {
                    path = curSel.Name + "/" + path;
                    curSel = curSel.Parent;
                }
                res += path.Substring(0, path.Length - 1);
                res += ";\n";
            }
            return res;
        }

    }
}
