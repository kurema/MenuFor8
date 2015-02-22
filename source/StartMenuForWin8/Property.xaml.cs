using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XmlSetting;
using System.Xml.Serialization;
using System.IO;

namespace StartMenuSetting
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool comboboxChanged = false;
        private bool checkboxChanged = false;

        //設定と最近使ったプログラムのxmlファイルの場所。コンパイル時には決定せず、実行中は不変なのでreadonlyで設定。
        /// <summary>
        /// startmenuSetting.xmlの場所。
        /// </summary>
        private readonly string xmlPathSet;
        /// <summary>
        /// recentProgram.xmlの場所。
        /// </summary>
        private readonly string xmlPathRecent;

        public MainWindow()
        {
            InitializeComponent();

            if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed)
            {
                xmlPathSet = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.DataDirectory + @"\startmenuSetting.xml";
                xmlPathRecent = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.DataDirectory + @"\recentProgram.xml";
            }
            else
            {
                xmlPathSet = "startmenuSetting.xml";
                xmlPathRecent = "recentProgram.xml";
            }

            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyTitleAttribute asmttl = (System.Reflection.AssemblyTitleAttribute)Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyTitleAttribute));
            System.Reflection.AssemblyCompanyAttribute asmcmp = (System.Reflection.AssemblyCompanyAttribute)Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyCompanyAttribute));
            System.Reflection.AssemblyCopyrightAttribute asmcpr = (System.Reflection.AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyCopyrightAttribute));
            System.Version asmver = asm.GetName().Version;
            VersionLabel.Content = asmttl.Title + " ver." + asmver + " by " + asmcmp.Company;
            CopyRightBlock.Text = asmcpr.Copyright;
            
            startmenuSetting csm = new startmenuSetting();
            XmlSerializer xs = new XmlSerializer(typeof(startmenuSetting));
            try
            {
                using (FileStream fs = new FileStream(xmlPathSet, FileMode.Open))
                {
                    csm = (startmenuSetting)xs.Deserialize(fs);
                }
            }
            catch
            {
                MessageBox.Show("設定ファイル'startmenuSetting.xml'にアクセスできません。");
                this.Close();
            }
            string dispS=null;
            if (csm.power.powerButton.Count() > 0 && csm.power.powerButton.ElementAt(0) is menuItem
                && (csm.power.powerButton.ElementAt(0) as menuItem).visible)
            {
                dispS = (csm.power.powerButton.ElementAt(0) as menuItem).head;
            }
            if (csm.power.powerMenu.Item is menu)
            {
                menu mn = csm.power.powerMenu.Item as menu;
                foreach (object o in mn.Items)
                {
                    if (o is menuItem && ((menuItem)o).visible)
                    {
                        menuItem mi= (menuItem)o;
                        ComboBoxItem cbi = new ComboBoxItem();
                        string si = mi.head;
                        si = new System.Text.RegularExpressions.Regex(@"\(\w+\)").Replace(si, "");
                        cbi.Content = si;
                        cbi.Tag = mi;
                        comboBox1.Items.Add(cbi);
                        if (si == dispS) comboBox1.SelectedItem = cbi;
                    }
                }
            }

            if (System.IO.File.Exists(xmlPathRecent))
            {
                checkBox1.IsChecked = true;
            }
            else
            {
                checkBox1.IsChecked = false;
            }
            comboBox1.SelectionChanged+=new SelectionChangedEventHandler(comboBox1_SelectionChanged);

            checkBox1.Unchecked += new RoutedEventHandler(checkBox1_Checked);
            checkBox1.Checked+=new RoutedEventHandler(checkBox1_Checked);
        }

        //本来はコマンドを使うべき。面倒なので、イベント駆動で。
        private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            comboboxChanged = true;
            activateApply();
        }

        private void checkBox1_Checked(object sender, RoutedEventArgs e)
        {
            checkboxChanged = true;
            activateApply();
        }
        private void activateApply()
        {
            buttonApply.IsEnabled = true;
        }
        private void applyChange()
        {
            if (checkboxChanged)
            {
                if (checkBox1.IsChecked==false)
                {
                    System.IO.File.Delete(xmlPathRecent);
                }
                else if (checkBox1.IsChecked == true && !System.IO.File.Exists(xmlPathRecent))
                {
                    XmlRecentProgram.recent rc = new XmlRecentProgram.recent();
                    System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(XmlRecentProgram.recent));
                    using (System.IO.FileStream fs = new System.IO.FileStream(xmlPathRecent, System.IO.FileMode.Create))
                    {
                        serializer.Serialize(fs, rc);
                    }
                }
            }
            if (comboboxChanged)
            {
                startmenuSetting csm = new startmenuSetting();
                XmlSerializer xs = new XmlSerializer(typeof(startmenuSetting));
                using (FileStream fs = new FileStream(xmlPathSet, FileMode.Open))
                {
                    csm = (startmenuSetting)xs.Deserialize(fs);
                }
                string si = ((menuItem)(((Control)comboBox1.SelectedItem).Tag)).head;
                si = new System.Text.RegularExpressions.Regex(@"\(\w+\)").Replace(si, "");
                if (csm.power.powerButton.Count() > 0)
                {
                    csm.power.powerButton.SetValue((menuItem)(((Control)comboBox1.SelectedItem).Tag), 0);
                    ((menuItem)csm.power.powerButton[0]).head = si;
                }
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(startmenuSetting));
                using (System.IO.FileStream fs = new System.IO.FileStream(xmlPathSet, System.IO.FileMode.Create))
                {
                    serializer.Serialize(fs, csm);
                }
            }
            comboboxChanged = false;
            checkboxChanged = false;
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            applyChange();
            this.Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void buttonApply_Click(object sender, RoutedEventArgs e)
        {
            applyChange();
            buttonApply.IsEnabled = false;
        }

        private void buttonCustom_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("notepad.exe", xmlPathSet);
        }
    }
}
