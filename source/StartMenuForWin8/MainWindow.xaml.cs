using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Collections;
using Microsoft.WindowsAPICodePack.Shell;
using XmlSetting;

/*-
 * Copyright (c) 2012 Makoto Kurema
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR(S) ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR(S) BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace StartMenuForWin8
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : GlassWindow
    {
        //Windows Vista以前に対応するためにはDirectoryInfoをShellObject非依存に書き換える必要があるかもしれません。
        //今回はWindows 8専用のためOS依存性の高い実装になっています。

        /// <summary>
        /// 複数フォルダに対応するためのクラス。
        /// これによりStartMenuSetting.xmlの<![CDATA[<menuFile>]]>内に複数のディレクトリに対応できる。
        /// ライブラリを作成せずにライブラリと同様にフォルダをまとめられる。
        /// WindowsAPICodePack依存のため古いWindowsの場合正しく動かない可能性がある。
        /// </summary>
        public class DirectoryInfo
        {
            private List<ShellObject> container = new List<ShellObject>();

            public void Add(params string[] arg)
            {
                foreach (string s in arg)
                {
                    container.Add(ShellFolder.FromParsingName(s));
                }
            }
            public void Add(params ShellObject[] arg)
            {
                container.AddRange(arg);
            }
            public void Add(object[] objs,ItemsChoiceType[] types)
            {
                for (int i = 0; i < objs.Count(); i++)
                {
                    switch (types[i])
                    {
                        case ItemsChoiceType.directory:
                            Add(ShellContainer.FromParsingName((string)objs[i]));
                            break;
                        case ItemsChoiceType.knownFolder:
                            IKnownFolder ikf;
                            string ipt = ((menuFileKnownFolder)objs[i]).Item;
                            switch (((menuFileKnownFolder)objs[i]).ItemElementName)
                            {
                                case ItemChoiceType.canonicalName:
                                    ikf = KnownFolderHelper.FromCanonicalName(ipt);
                                    break;
                                case ItemChoiceType.folderId:
                                    ikf = KnownFolderHelper.FromKnownFolderId(Guid.Parse(ipt));
                                    break;
                                case ItemChoiceType.parsingName:
                                    ikf = KnownFolderHelper.FromParsingName(ipt);
                                    break;
                                case ItemChoiceType.path:
                                    ikf = KnownFolderHelper.FromPath(ipt);
                                    break;
                                default:
                                    throw (new NotImplementedException());
                                    //break;
                            }
                            Add((ShellContainer)ikf);
                            break;
                        case ItemsChoiceType.shellLibrary:
                            Add(ShellLibrary.Load((string)objs[i],true));
                            break;
                        case ItemsChoiceType.shellSearchCollection:
                            Add(ShellSearchCollection.FromParsingName((string)objs[i]));
                            break;
                    }
                }
            }

            public override string ToString()
            {
                if (container.Count() == 0)
                {
                    return "Empty";
                }
                else if (container.Count() == 1)
                {
                    return container[0].Name;
                }
                else
                {
                    return container[0].Name + "...";
                }
            }

            public DirectoryInfo()
            {
            }
            public DirectoryInfo(params ShellObject[] arg)
            {
                Add(arg);
            }
            public DirectoryInfo(params string[] arg)
            {
                Add(arg);
            }
            public DirectoryInfo(object[] objs, ItemsChoiceType[] types)
            {
                Add(objs, types);
            }

            public void Open()
            {
                foreach (ShellObject so in container)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(so.ParsingName);
                    }
                    catch(Exception exc)
                    {                
                        MessageBox.Show(Properties.Resources.StringExceptionCatched+"\n"
                            +Properties.Resources.StringTarget+ ":" + so.ParsingName + "\n" + exc, 
                            Properties.Resources.StringFileCantOpen);
                    }                
                }
            }

            public ShellObject[] getMember()
            {
                List<ShellObject> ret = new List<ShellObject>();
                foreach (ShellObject mem in container)
                {
                    if (mem is ShellContainer)
                    {
                        foreach (ShellObject men in (ShellContainer)mem)
                        {
                            ret.Add(men);
                        }
                    }
                    else
                    {
                        ret.Add(mem);
                    }
                }
                return ret.ToArray();
            }

            public ShellThumbnail getThumbnail()
            {
                if (container.Count() == 0)
                {
                    return null;
                }
                else
                {
                    return container[0].Thumbnail;
                }
            }
        }

        public XmlRecentProgram.recent RecentUsedProgram;
        public ImageSource DefaultIcon;

        //設定と最近使ったプログラムのxmlファイルの場所。コンパイル時には決定せず、実行中は不変なのでreadonlyで設定。
        /// <summary>
        /// startmenuSetting.xmlの場所。
        /// </summary>
        private readonly string xmlPathSet;
        /// <summary>
        /// recentProgram.xmlの場所。
        /// </summary>
        private readonly string xmlPathRecent;
        

        //スリープを実行するためのdll
        [System.Runtime.InteropServices.DllImport("Powrprof.dll",SetLastError = true)]
        public static extern bool SetSuspendState(bool hibernate,bool forceCritical,bool disableWakeEvent);
        /// <summary>
        /// タスクバーの高さ。
        /// </summary>
        int taskbarHeight = 40;

        public MainWindow()
        {
            InitializeComponent();

            //Property.xaml.csにも同様の定義がある。設定ファイル名を変更する際はそちらも変えること。
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

            ParseSetting();
            SetDefuaultIcon();

            SearchBox.Focus();
        }

        /// <summary>
        /// ユーザーイメージを取得します。
        /// </summary>
        private void SetDefuaultIcon()
        {
            DefaultIcon = null;
            //Contactsフォルダ内を探します。
            ShellContainer cnt = (ShellContainer)KnownFolders.Contacts;
            if (cnt.Count() > 0)
            {
                //標準では最初のファイルのサムネイルに設定します。
                DefaultIcon = cnt.ElementAt(0).Thumbnail.MediumBitmapSource;
                //もし、ファイル名にユーザー名を含むファイルが存在する場合はそのサムネイルに設定します。
                //私の場合はユーザー名とファイル名が違いました。Windows Liveとの関係もあるかもしれません。
                //Contactsフォルダを常用している場合、全く関係ない人の画像となる可能性もあります。
                //対策方法はわかりませんでした。
                foreach (ShellObject ob in cnt)
                {
                    if (ob.Name.Contains(Environment.UserDomainName))
                    {
                        DefaultIcon = cnt.ElementAt(0).Thumbnail.MediumBitmapSource;
                    }
                }
                imageIcon.Source = DefaultIcon;
            }
            try
            {
                ShellContainer sc = (ShellContainer)KnownFolderHelper.FromCanonicalName("AccountPictures");
                if (sc.Count() > 0)
                {
                    DefaultIcon = sc.ElementAt(0).Thumbnail.MediumBitmapSource;
                }
                imageIcon.Source = DefaultIcon;
            }
            catch
            {
            }
        }

        /// <summary>
        /// StartMenuSetting.xmlを読み込んで画面を構成する。
        /// </summary>
        // 本来はバインディングを利用すべきだったかもしれないが、面倒そうなので断念。
        private void ParseSetting()
        {
            startmenuSetting csm = new startmenuSetting();
            XmlSerializer xs=new XmlSerializer(typeof(startmenuSetting));
            try
            {
                using (FileStream fs = new FileStream(xmlPathSet, FileMode.Open))
                {
                    csm = (startmenuSetting)xs.Deserialize(fs);
                }
            }
            catch
            {
                MessageBox.Show(Properties.Resources.StringCantAccessFile+":\n'startmenuSetting.xml'");
                this.Close();
            }
            //別タスクで最近使ったプログラムのXML読み込み。IO待ちを回避。
            var xsrTask = Task.Factory.StartNew(() =>
            {
                XmlSerializer xss = new XmlSerializer(typeof(XmlRecentProgram.recent));
                try
                {
                    using (FileStream fss = new FileStream(xmlPathRecent, FileMode.Open))
                    {
                        RecentUsedProgram = (XmlRecentProgram.recent)xss.Deserialize(fss);
                    }
                }
                catch
                {
                    RecentUsedProgram = new XmlRecentProgram.recent();
                }
            });
            //ウィンドウ設定
            taskbarHeight=int.Parse(csm.setting.taskbarHeight);
            switch (csm.setting.windowDesign)
            {
                case windowDesign.glass:
                    //AeroGlassの場合。
                    this.AllowsTransparency = false;
                    BorderWindow.Background = (Brush)this.FindResource("WindowBrushTransparent");
                    this.ResizeMode = ResizeMode.CanResize;
                    this.Loaded += new RoutedEventHandler(GlassWindow_Loaded);
                    break;
                case windowDesign.transparent:
                    //すりガラスなしの透過ウィンドウの場合。
                    BorderWindow.Background = (Brush)this.FindResource("WindowBrushTransparent");
                    break;
                case windowDesign.normal:
                    //透過なしウィンドウの場合。
                    BorderWindow.Background = (Brush)this.FindResource("WindowBrush");
                    break;
            }

            //アイコン表示設定
            imageIcon.Visibility = csm.icon.visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            //電源ボタン設定
            stackPanelPower.Visibility = csm.power.visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            Style powerButtonStyle = (Style)FindResource("PowerButtonLeftStyle");
            foreach (object o in csm.power.powerButton)
            {
                Button powerButton = new Button();
                powerButton.Style = powerButtonStyle;
                if (o is menuFile)
                {
                    menuFile mf = (menuFile)o;
                    if (mf.visible)
                    {
                        powerButton.Content = mf.head;
                        Set_Button_MenuFile_Open(powerButton, mf);
                    }
                    else
                    {
                        continue;
                    }
                }
                else if(o is menuItem)
                {                  
                    menuItem mi = (menuItem)o;
                    if (mi.visible)
                    {
                        powerButton.Content = mi.head;
                        Set_Button_MenuItem(powerButton, mi.execute);
                    }
                    else { continue; }
                }
                stackPanelPower.Children.Add(powerButton);
                powerButtonStyle = (Style)FindResource("PowerButtonMiddleStyle");
            }
            Button powerMenu = new Button();
            powerMenu.Style = (Style)FindResource("PowerButtonRightStyle");
            object tempObj = csm.power.powerMenu.Item;
            stackPanelPower.Children.Add(powerMenu);
            powerMenu.Content = "▶";
            if (tempObj is menu)
            {
                Set_Button_menu(powerMenu, (menu)tempObj);
            }
            else if (tempObj is menuFile)
            {
                Set_Button_MenuFile_Menu(powerMenu, (menuFile)tempObj);
            }
            else if (tempObj is menuItem)
            {
                Set_Button_MenuItem(powerMenu,((menuItem)tempObj).execute);
            }

            //よく使うアイテム設定
            Style pbsStyle = (Style)this.FindResource("PushButtonStyle");
            Style mbsStyle = (Style)this.FindResource("MenuButtonStyle");

            foreach (object o in csm.items.menu.Items)
            {
                if (o is menu)
                {
                    menu tmp = (menu)o;
                    if (tmp.visible)
                    {
                        Button bt = new Button();
                        bt.Style = mbsStyle;
                        bt.Content = BasicHeadConversion(tmp.head);
                        Set_Button_menu(bt, (menu)o);
                        stackPanelRecentItem.Children.Add(bt);
                    }
                }else if(o is menuFile){
                    menuFile tmp = (menuFile)o;
                    if (tmp.visible && tmp.actAsShortcut)
                    {
                        Button bt = new Button();
                        bt.Style = pbsStyle;
                        bt.Content = BasicHeadConversion(tmp.head);
                        stackPanelRecentItem.Children.Add(bt);
                        Set_Button_MenuFile_Open(bt, tmp);
                    }
                    else if (tmp.visible)
                    {
                        Button bt = new Button();
                        bt.Style = mbsStyle;
                        bt.Content = BasicHeadConversion(tmp.head);
                        stackPanelRecentItem.Children.Add(bt);
                        Set_Button_MenuFile_Menu(bt, tmp);
                    }
                }
                else if (o is menuItem)
                {
                    menuItem tmp = (menuItem)o;
                    if (tmp.visible)
                    {
                        Button bt = new Button();
                        bt.Style = pbsStyle;
                        bt.Content = BasicHeadConversion(tmp.head);
                        Set_Button_MenuItem(bt, tmp.execute);
                        stackPanelRecentItem.Children.Add(bt);
                    }
                }
                else if (o is recent)
                {
                    recent tmp = (recent)o;
                    if (tmp.visible && tmp.actAsMenu)
                    {
                        Button bt = new Button();
                        Set_Button_Recent(bt, tmp);
                        bt.Style = mbsStyle;
                        stackPanelRecentItem.Children.Add(bt);
                    }
                    else if (tmp.visible)
                    {
                        if (tmp.type == recentType.program)
                        {
                            for (int i = 0;
                                i < int.Parse(tmp.count) && RecentUsedProgram.Items != null && i < RecentUsedProgram.Items.Count(); i++)
                            {
                                Button bt = new Button();
                                ShellObject so = ShellObject.FromParsingName(RecentUsedProgram.Items.ElementAt(i).href);
                                bt.Style = pbsStyle;
                                bt.Content = so.Name;
                                bt.Tag = so.ParsingName;
                                bt.Click += new RoutedEventHandler(Button_OpenProgram);
                                stackPanelRecentItem.Children.Add(bt);
                            }
                        }
                        else// if (rc.type == recentType.file) 
                        {
                            ShellContainer sc = (ShellContainer)KnownFolders.Recent;
                            int i = 0;
                            foreach (ShellObject so in sc)
                            {
                                if (i < int.Parse(tmp.count))
                                {
                                    Button bt = new Button();
                                    bt.Style = pbsStyle;
                                    bt.Content = so.Name;                                    
                                    bt.Tag = so;
                                    bt.Click += new RoutedEventHandler(Button_OpenTagedFile);
                                    stackPanelRecentItem.Children.Add(bt);
                                }
                                i++;
                            }
                        }
                        //よく使うアイテムで単体として最近使ったファイルへのリンクを行った場合。
                    }
                }
                else if (o is separater)
                {
                    if (((separater)o).visible)
                    {
                        Separator s = new Separator();
                        s.Style = (Style)this.FindResource("ItemSeparator");
                        stackPanelRecentItem.Children.Add(s);
                    }
                }
            }
            //タスクの終了を待つ。
            Task.WaitAll(xsrTask);
            //ほぼ必ず使うスタイルなので予め読み込んでおく。
            Style prgBStyle = (Style)this.FindResource("ProgramButtonStyle");
            Style prgSStyle = (Style)this.FindResource("ProgramSeparator");

            //よく使うプログラム等設定。
            foreach (object o in csm.programs.menu.Items)
            {
                if (o is separater && ((separater)o).visible)
                {
                    Separator sp = new Separator();
                    sp.Style = prgSStyle;
                    StackPanelPrograms.Children.Add(sp);
                }
                else if (o is recent && ((recent)o).visible)
                {
                    recent rc = (recent)o;
                    ShellContainer sc = ((ShellContainer)KnownFolders.Recent);
                    //(rc.type == recentType.file)の場合のみ使用だが、エラー対策で地に書く。
                    //ShellContainerの内容が遅延評価ならパフォーマンス上の問題はないが…。
                    for (int i = 0; i < int.Parse(rc.count); i++)
                    {
                        Button bt;
                        if (rc.type == recentType.file)
                        {
                            bt = Prepare_ButtonWithStyle(prgBStyle, 29, sc.ElementAt(i).Thumbnail.SmallBitmapSource, sc.ElementAt(i).Name);
                            if (sc.Count() <= i) { break; }
                            bt.Tag = (ShellObject)sc.ElementAt(i);
                            bt.Click += new RoutedEventHandler(Button_OpenTagedFile);
                        }
                        else
                        {
                            if (RecentUsedProgram.Items == null || RecentUsedProgram.Items.Count() <= i) { break; }
                            if (System.IO.File.Exists(RecentUsedProgram.Items[i].href))
                            {
                                ShellObject so = ShellObject.FromParsingName(RecentUsedProgram.Items[i].href);
                                bt = Prepare_ButtonWithStyle(prgBStyle, 29, so.Thumbnail.SmallBitmapSource, so.Name);
                                bt.Tag = so.ParsingName;
                                bt.Click += new RoutedEventHandler(Button_OpenProgram);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        StackPanelPrograms.Children.Add(bt);
                    }
                }
                else if (o is menuItem && ((menuItem)o).visible)
                {
                    Button bt = new Button();
                    bt = Prepare_ButtonWithStyle(prgBStyle, 29, null, ((menuItem)o).head);

                    Set_Button_MenuItem(bt, ((menuItem)o).execute);
                    StackPanelPrograms.Children.Add(bt);
                }
                else if (o is menuFile && ((menuFile)o).visible)
                {
                    Button bt = new Button();
                    menuFile mf = (menuFile)o;
                    DirectoryInfo di = new DirectoryInfo(mf.Items, mf.ItemsElementName);
                    bt = Prepare_ButtonWithStyle(prgBStyle, 29, di.getThumbnail().SmallBitmapSource, di.ToString());
                    Set_Button_MenuFile_Open(bt, (menuFile)o);
                    StackPanelPrograms.Children.Add(bt);
                }
            }

        }

        private string BasicHeadConversion(string s)
        {
            s = s.Replace("%UserName%", Environment.UserName);
            s = s.Replace("%MachineName%", Environment.MachineName);
            return s;
        }

        #region Set_Button関数
        void Set_Button_MenuItem(Button b,execute x)
        {
            b.Tag = x;
            b.Click += new RoutedEventHandler(Control_ExecuteTagedExecute);
        }

        void Set_Button_MenuFile_Open(Button b, menuFile mf)
        {
            b.Tag = new DirectoryInfo(mf.Items, mf.ItemsElementName);
            b.Click += new RoutedEventHandler(Button_OpenTagedFile);
            b.MouseEnter += new MouseEventHandler(Button_ShowIcon);
            b.MouseLeave += new MouseEventHandler(Button_ResetIcon);
        }

        void Set_Button_MenuFile_Menu(Button b, menuFile mf)
        {
            b.Tag = new DirectoryInfo(mf.Items, mf.ItemsElementName);
            b.Click += new RoutedEventHandler(Control_OpenTagedContainer);
            b.MouseEnter += new MouseEventHandler(Button_ShowIcon);
            b.MouseLeave += new MouseEventHandler(Button_ResetIcon);
        }

        void Button_ResetIcon(object sender, MouseEventArgs e)
        {
            if(DefaultIcon !=null)imageIcon.Source = DefaultIcon;

        }

        void Button_ShowIcon(object sender, MouseEventArgs e)
        {
            Button bt = (Button)sender;
            if (bt.Tag is DirectoryInfo)
            {
                DirectoryInfo di = (DirectoryInfo)bt.Tag;
                imageIcon.Source = di.getThumbnail().MediumBitmapSource;
            }
            else if(bt.Tag is ContainerTag)
            {
                ContainerTag ct = (ContainerTag)bt.Tag;
                imageIcon.Source = ct.Directory.getThumbnail().MediumBitmapSource;
            }
        }

        void Set_Button_Recent(Button b, recent rc)
        {
            b.Tag = rc;
            if (rc.type == recentType.file)
            {
                b.Tag = (ShellObject)KnownFolders.Recent;
                b.Click += new RoutedEventHandler(Control_OpenTagedContainer);
                b.Content = Properties.Resources.StringRecentItems;
            }else// if(rc.type ==recentType.program)
            {
                b.Tag = rc;
                b.Click += new RoutedEventHandler(Control_OpenTagedRecent);
                b.Content = Properties.Resources.StringRecentPrograms;
            }
        }

        void Set_Button_menu(Button c, menu m)
        {
            c.Tag = m;
            c.Click += new RoutedEventHandler(Button_expandTagedMenu);
        }

        #endregion

        /// <summary>
        /// ShellObjectの対象ファイルを関連付けされたアプリケーションで開きます。
        /// 関数にするほどではありません。
        /// </summary>
        /// <param name="so">開くファイル</param>
        void OpenShellObject(ShellObject so)
        {
            try
            {
                System.Diagnostics.Process.Start(so.ParsingName);
            }
            catch (Exception exc)
            {
                MessageBox.Show(Properties.Resources.StringExceptionCatched + "\n" + Properties.Resources.StringTarget + ":" + so.ParsingName + "\n" + exc, Properties.Resources.StringFileCantOpen);
            }
        }

        // (通常Tagに格納された)StartMenuSetting.xmlのデシリアライズされたクラスからMenuItemを追加
        #region addMenuItemFrom...
        void addMenuItemsFromRecent(ItemCollection imc)
        {
            imc.Clear();
            foreach (XmlRecentProgram.recentProgram rcp in RecentUsedProgram.Items)
            {
                if (System.IO.File.Exists(rcp.href))
                {
                    MenuItem mn = new MenuItem();
                    ShellObject sho = ShellObject.FromParsingName(rcp.href);
                    mn.Header = sho.Name;
                    mn.Tag = sho.ParsingName;
                    mn.Click += new RoutedEventHandler(Button_OpenProgram);
                    Image im = new Image();
                    im.Width = 16; im.Height = 16;
                    im.Source = sho.Thumbnail.SmallBitmapSource;
                    mn.Icon = im;
                    imc.Add(mn);
                }
            }            
        }

        void addMenuItemsFromXmlMenu(menu m,ItemCollection imc)
        {
            foreach (object o in m.Items)
            {
                if (o is menuItem)
                {
                    menuItem mi=(menuItem)o;
                    if (mi.visible)
                    {
                        MenuItem mni = new MenuItem();
                        mni.Header = mi.head;
                        mni.Tag = mi.execute;
                        mni.Click += new RoutedEventHandler(Control_ExecuteTagedExecute);
                        imc.Add(mni);
                    }
                }
                else if (o is menu)
                {
                    menu mn = (menu)o;
                    if (mn.visible)
                    {
                        MenuItem mni = new MenuItem();
                        mni.Header = mn.head;
                        mni.Items.Add(new MenuItem());
                        mni.Tag = mn;
                        mni.SubmenuOpened += new RoutedEventHandler(MenuItem_ExpandTagedMenu);
                        imc.Add(mni);
                    }
                }
                else if (o is menuFile)
                {
                    menuFile mf = (menuFile)o;
                    if (mf.visible && mf.actAsShortcut)
                    {
                        MenuItem mni = new MenuItem();
                        mni.Header = mf.head;
                        mni.Tag = new DirectoryInfo(mf.Items, mf.ItemsElementName);
                        mni.Click += new RoutedEventHandler(Button_OpenTagedFile);
                        imc.Add(mni);
                    }
                    else if (mf.visible)
                    {
                        MenuItem mni = new MenuItem();
                        mni.Header = mf.head;
                        mni.Tag = new DirectoryInfo(mf.Items, mf.ItemsElementName);
                        mni.Items.Add(new MenuItem());
                        mni.SubmenuOpened += new RoutedEventHandler(Control_OpenTagedContainer);
                        imc.Add(mni);
                    }
                }
                else if (o is separater)
                {
                    separater sp = (separater)o;
                    if (sp.visible)
                    {
                        imc.Add(new Separator());
                    }
                }
                else if (o is recent)
                {
                    recent rc = (recent)o;
                    if(rc.visible &&rc.actAsMenu){
                        MenuItem mni = new MenuItem();
                        if (rc.type == recentType.file)
                        {
                            mni.Header = Properties.Resources.StringRecentItems;
                            mni.Items.Add(new MenuItem());
                            mni.Tag = (ShellFolder)KnownFolders.Recent;                    
                            mni.SubmenuOpened += new RoutedEventHandler(Control_OpenTagedContainer);
                        }
                        else
                        {
                            mni.Header = Properties.Resources.StringRecentPrograms;
                            mni.Items.Add(new MenuItem());
                            mni.SubmenuOpened += new RoutedEventHandler(Control_OpenTagedRecent);
                        }
                        imc.Add(mni);
                    }
                    else if (rc.visible)
                    {
                        //最近使ったアイテムへの直接リンク
                        //一応作りましたがあまり想定していない利用法です。
                        //十分なテストもしていません。推奨はしません。
                        if (rc.type == recentType.program)
                        {
                            for (int i = 0;
                                i < int.Parse(rc.count) && RecentUsedProgram.Items != null && i < RecentUsedProgram.Items.Count(); i++)
                            {
                                MenuItem mni = new MenuItem();
                                ShellObject so = ShellObject.FromParsingName(RecentUsedProgram.Items.ElementAt(i).href);
                                mni.Header = so.Name;
                                Image im = new Image();
                                im.Source = so.Thumbnail.SmallBitmapSource;
                                im.Width = 16; im.Height = 16;
                                im.Source.Freeze();
                                mni.Icon = im;
                                mni.Tag = so.ParsingName;
                                mni.Click += new RoutedEventHandler(Button_OpenProgram);
                                imc.Add(mni);
                            }
                        }
                        else// if (rc.type == recentType.file) 
                        {
                            ShellContainer sc = (ShellContainer)KnownFolders.Recent;
                            int i = 0;
                            foreach (ShellObject so in sc)
                            {
                                if (i < int.Parse(rc.count))
                                {
                                    MenuItem mni = new MenuItem();
                                    mni.Header = so.Name;
                                    Image im = new Image();
                                    im.Source = so.Thumbnail.SmallBitmapSource;
                                    im.Source.Freeze();
                                    im.Width = 16; im.Height = 16;
                                    mni.Icon = im;
                                    mni.Tag = so.ParsingName;
                                    mni.Click += new RoutedEventHandler(Button_OpenTagedFile);
                                    imc.Add(mni);
                                }
                                i++;
                            }
                        }
                    }
                }
            }

        }
        #endregion

        /// <summary>
        /// StartMenuSetting.xmlの<![CDATA[<execute>]]>で支持された命令を実行。
        /// ファイルを開く、コマンド実行、ビルトインコマンド(現在スリープ処理のみ)実行の三種。
        /// </summary>
        /// <param name="ex">StartMenuSetting.xmlの<![CDATA[<execute>]]>をデシリアライズしたもの。</param>
        void execExecute(execute ex)
        {
            //this.Deactivated -= Window_Deactivated;
            //MessageBox.Show("以下の内容が実行されます。\nコマンドの種類:"+ex.commandType+"\nコマンド:" + ex.command + "\nパラメーター:" + ex.parameter);
            switch (ex.commandType)
            {
                case commandType.open:
                    try
                    {
                        System.Diagnostics.Process.Start(ex.parameter);
                    }
                    catch(Exception exc)
                    {
                        MessageBox.Show(Properties.Resources.StringExceptionCatched + "\n" + Properties.Resources.StringTarget + ":" + ex.parameter + "\n" + exc, Properties.Resources.StringCantExecuteCommand);
                    }                
                    break;
                case commandType.builtIn:
                    switch (ex.command)
                    {
                        case "power_sleep":
                            //スリープモードに移行する。
                            SetSuspendState(false, false, false);
                            break;
                        case "dialog_run":
                            // COMのプログラムIDから型を取得する
                            Type typeShell = Type.GetTypeFromProgID("Shell.Application");
                            // Activatorを使って型からオブジェクトを作成する
                            object objShell = Activator.CreateInstance(typeShell);
                            //名前を指定して実行（FileRun）
                            typeShell.InvokeMember("FileRun",System.Reflection.BindingFlags.InvokeMethod,null, objShell, null);
                            break;
                    }
                    break;
                case commandType.command:
                    System.Diagnostics.ProcessStartInfo psi =new System.Diagnostics.ProcessStartInfo();
                    psi.FileName = ex.command;
                    psi.Arguments = ex.parameter;
                    switch (ex.windowState)
                    {
                        case windowState.close:
                            psi.CreateNoWindow = true;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                            break;
                        case windowState.hidden:
                            psi.CreateNoWindow = false;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                            break;
                        case windowState.maximized:
                            psi.CreateNoWindow = false;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Maximized;
                            break;
                        case windowState.minimized:
                            psi.CreateNoWindow = false;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
                            break;
                        case windowState.normal:
                            psi.CreateNoWindow = false;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                            break;

                    }
                    try
                    {
                        System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                    }
                    catch(Exception exc)
                    {
                        MessageBox.Show(Properties.Resources.StringExceptionCatched + "\n" + Properties.Resources.StringTarget + ":" +psi + "\n" + exc, Properties.Resources.StringCantExecuteCommand);
                    }                
                    break;
            }
            
        }

        /// <summary>
        /// コンテキストメニューをControlの右側で開く。
        /// </summary>
        /// <param name="b">コンテキストメニューの対象。このControlの右側で開く。</param>
        /// <param name="cm">コンテキストメニュー。</param>
        void ExpandContextMenu(Control b, ContextMenu cm)
        {
            if (!cm.IsOpen)
            {
                cm.PlacementTarget = b;
                cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                cm.IsOpen = true;
            }
        }
        
        
        /// <summary>
        /// mi.Tagで指示されたファイルのアイコンをmiに設定。
        /// </summary>
        /// <param name="mi">対象のMenuItem</param>
        private void drawIcon(MenuItem mi)
        {
            Image im = new Image();
            if (mi.Tag is DirectoryInfo)
            {
                im.Source = ((DirectoryInfo)mi.Tag).getThumbnail().SmallBitmapSource;
            }
            else if (mi.Tag is ShellObject)
            {
                ShellObject so = (ShellObject)mi.Tag;
                im.Source = so.Thumbnail.SmallBitmapSource;
            }
            else if (mi.Tag is string)
            {
                 System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon((string)mi.Tag);
                 im.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            im.Width = 16; im.Height = 16;
            mi.Icon = im;
        }

        #region:MainWindow Event
        void Control_OpenTagedRecent(object sender, RoutedEventArgs e)
        {
            if(sender is MenuItem)
            {
                addMenuItemsFromRecent(((MenuItem)sender).Items);
            }
            else if (sender is Control)
            {
                ContextMenu ct = new ContextMenu();
                addMenuItemsFromRecent(ct.Items);
                ExpandContextMenu((Control)sender, ct);
            }
        }

        void Button_OpenTagedFile(object sender, RoutedEventArgs e)
        {
            this.Deactivated -= Window_Deactivated;
            object ob = (object)((Control)sender).Tag;
            if (ob is DirectoryInfo)
            {
                ((DirectoryInfo)ob).Open();
            }
            else if (ob is ShellObject)
            {
                OpenShellObject((ShellObject)ob);
            }
            else //if(ob is string)
            {
                try
                {
                    System.Diagnostics.Process.Start((string)ob);
                }
                catch (Exception exc)
                {
                    MessageBox.Show(Properties.Resources.StringExceptionCatched + "\n" + Properties.Resources.StringTarget + ":" + (string)ob + "\n" + exc, Properties.Resources.StringFileCantOpen);
                }
            }
            this.Close();
        }

        void Button_expandTagedMenu(object sender, RoutedEventArgs e)
        {
            Button c = (Button)sender;
            ContextMenu cm = new ContextMenu();
            addMenuItemsFromXmlMenu((menu)c.Tag, cm.Items);

            c.Tag = cm;
            c.Click -= Button_expandTagedMenu;
            c.Click += new RoutedEventHandler(Button_ShowContextMenu);
            ExpandContextMenu(c, cm);
        }


        void MenuItem_ExpandTagedMenu(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            mi.Items.Clear();
            addMenuItemsFromXmlMenu((menu)mi.Tag, mi.Items);
            mi.SubmenuOpened -= new RoutedEventHandler(MenuItem_ExpandTagedMenu);
        }

        void Control_ExecuteTagedExecute(object sender, RoutedEventArgs e)
        {
            this.Deactivated -= Window_Deactivated;
            Control b = (Control)sender;
            execExecute((execute)b.Tag);
            this.Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
                this.Close();
        }


        private void MenuItem_Click_OpenProperty(object sender, RoutedEventArgs e)
        {
            StartMenuSetting.MainWindow prp = new StartMenuSetting.MainWindow();
            prp.Show();
            this.Deactivated -= Window_Deactivated;
            prp.Closed += new EventHandler(propertyWindow_Closed);
        }

        void propertyWindow_Closed(object sender, EventArgs e)
        {
            this.Close();
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Deactivated -= Window_Deactivated;
        }

        private struct ContainerTag
        {
            public DirectoryInfo Directory;
            public ContextMenu Menu;
        }


        private void Control_OpenTagedContainer(object sender, RoutedEventArgs e)
        {
            Control cnt = (Control)sender;
            ShellObject[] member;

            DirectoryInfo tmpDi;


            //Tagの種類によって振る舞いを変化。DirectoryInfoは複数のフォルダを同時に参照する場合などに使う。(Programs等。)
            if (cnt.Tag is DirectoryInfo)
            {
                DirectoryInfo di = (DirectoryInfo)cnt.Tag;
                member = di.getMember();

                tmpDi = di;
            }
            else// if(cnt.Tag is ShellContainer)
            {
                ShellContainer sc = (ShellContainer)cnt.Tag;
                member = sc.ToArray<ShellObject>();

                tmpDi = new DirectoryInfo(sc);
            }
            //menuを初期化。呼び出し元により追加先を変化。
            System.Windows.Controls.ItemsControl menu;
            if (cnt is Button)
            {
                menu = new ContextMenu();
                ExpandContextMenu((Button)sender, (ContextMenu)menu);
                //cnt.Tag = menu;
                cnt.Tag = new ContainerTag() { Directory = tmpDi, Menu =(ContextMenu)menu };
                ((Button)sender).Click -= Control_OpenTagedContainer;
                ((Button)sender).Click += new RoutedEventHandler(Button_ShowContextMenu);
                ((Button)sender).MouseDoubleClick += new MouseButtonEventHandler(Button_OpenContainer);
            }
            else// if(cnt is MenuItem)
            {
                menu = (System.Windows.Controls.ItemsControl)sender;
                menu.Items.Clear();
                ((MenuItem)sender).SubmenuOpened -= Control_OpenTagedContainer;
            }

            //一応非同期処理に対応。
            //しかし、実際早くなっているようには全く思えない。
            Dispatcher.BeginInvoke(new Action<ItemsControl,ShellObject[]>((menuT,memberT)=>{

            foreach (ShellObject sobj in memberT)
            {
                MenuItem mitem = new MenuItem();
                mitem.Header = sobj.Name;
                if (sobj is ShellContainer)
                {
                    mitem.SubmenuOpened += new RoutedEventHandler(Control_OpenTagedContainer);
                    mitem.MouseDoubleClick += new MouseButtonEventHandler(Button_OpenTagedFile);
                    mitem.Items.Add(new MenuItem());
                    menuT.Items.Insert(0, mitem);
                }
                else
                {
                    menuT.Items.Add(mitem);
                    mitem.Click += new RoutedEventHandler(Button_OpenTagedFile);
                }
                mitem.Tag = sobj;
                drawIcon(mitem);
                //mitem.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>{ drawIcon(mitem); }));                
            }
            if (menuT.Items.Count == 0)
            {
                MenuItem mni = new MenuItem();
                mni.Header = "(なし)";
                menuT.Items.Add(mni);
            }
            }),menu,member);

        }

        void Button_OpenContainer(object sender, MouseButtonEventArgs e)
        {
            Button b = (Button)sender;
            if (b.Tag is ContainerTag)
            {
                ContainerTag ct = (ContainerTag)b.Tag;
                ct.Directory.Open();
            }
            this.Close();
        }

        void Button_ShowContextMenu(object sender, RoutedEventArgs e)
        {
            Button b = (Button)sender;
            if (b.Tag is ContextMenu)
            {
                ContextMenu cm = (ContextMenu)b.Tag;
                ExpandContextMenu(b, cm);
            }
            else if (b.Tag is ContainerTag)
            {
                ContainerTag ct = (ContainerTag)b.Tag;
                ExpandContextMenu(b, ct.Menu);
            }
        }


        private void GlassWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (AeroGlassCompositionEnabled)
            { 
                //下のコードで例外が発生。原因が突き止められませんでした。
                //SetAeroGlassTransparency(); }

                //ウィンドウを設定。
                System.Windows.Interop.WindowInteropHelper interopHelper= new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr windowHandle =  interopHelper.Handle;
                System.Windows.Interop.HwndSource.FromHwnd(windowHandle).CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            }

            this.Background = Brushes.Transparent;
            this.MinHeight = this.Height;
            this.MaxHeight = this.Height;
            this.MinWidth = this.Width;
            this.MaxWidth = this.Width;
        }

        private bool IsProgramAllWritten = false;
        private void ProgramDockPanelRecent_Click(object sender, RoutedEventArgs e)
        {
            Panel.SetZIndex(ProgramDockPanelRecent, 1);
            Panel.SetZIndex(ProgramDockPanelAll, 2);
            Panel.SetZIndex(SearchScrollViewer, 0);
            if (!IsProgramAllWritten)
            {
                ProgramAllTreeViewScrollViewer.Height = ProgramAllTreeViewScrollViewer.ActualHeight;
                LoadProgramAll(ProgramAllTreeView,new DirectoryInfo((ShellContainer)KnownFolders.Programs, (ShellContainer)KnownFolders.CommonPrograms));
                IsProgramAllWritten = true;
            }
        }

        private void ProgramDockPanelAll_Click(object sender, RoutedEventArgs e)
        {
            Panel.SetZIndex(ProgramDockPanelRecent, 2);
            Panel.SetZIndex(ProgramDockPanelAll, 1);
            Panel.SetZIndex(SearchScrollViewer, 0);

        }

        private void GlassWindow_Loaded_TaskBarHeight(object sender, RoutedEventArgs e)
        {
            this.Top = SystemParameters.VirtualScreenHeight - this.Height - taskbarHeight;
        }
        #endregion

        #region 全てのプログラム関係
        private struct ProgramAllContainerTag
        {
            public StackPanel stackPanel;
            public DirectoryInfo dir;
        }

        private struct ShellObjectInfo
        {
            public ImageSource imageSource;
            public string parsingName;
            public string name;
        }
        
        private StackPanel LoadProgramAll(StackPanel stpnl, DirectoryInfo di)
        {
            ShellObject[] sos = di.getMember();
            Dictionary<string, DirectoryInfo> did = new Dictionary<string, DirectoryInfo>();
            List<ShellObjectInfo> dirs = new List<ShellObjectInfo>();
            List<ShellObjectInfo> objs = new List<ShellObjectInfo>();
//            StackPanel stpnl = new StackPanel();

            foreach (ShellObject so in sos)
            {
                if (so is ShellContainer)
                {
                    if (did.ContainsKey(so.Name))
                    {
                        did[so.Name].Add(so);
                    }
                    else
                    {
                        did[so.Name] = new DirectoryInfo(so);
                        ImageSource ims=so.Thumbnail.SmallBitmapSource;
                        ims.Freeze();
                        dirs.Add(new ShellObjectInfo() { imageSource = ims, parsingName = "", name = so.Name });
                    }
                }
                else
                {
                    ImageSource ims=so.Thumbnail.SmallBitmapSource;
                    ims.Freeze();
                    objs.Add(new ShellObjectInfo() { imageSource = ims, parsingName = so.ParsingName, name = so.Name });
                }
            }
            Style st = (Style)FindResource("SearchResultButton");
            foreach (ShellObjectInfo soi in dirs)
            {
                Button bt = Prepare_ButtonWithStyle(st, 18, soi.imageSource, soi.name);
                StackPanel stp=new StackPanel();
                stp.Margin = new Thickness(16, 0, 0, 0);
                stp.Visibility = Visibility.Collapsed;
                bt.Tag = new ProgramAllContainerTag() { dir = did[soi.name], stackPanel = stp };
                bt.Click += new RoutedEventHandler(Button_PrepareStackPanel);
                stpnl.Children.Add(bt);
                stpnl.Children.Add(stp);
            }
            foreach (ShellObjectInfo soi in objs)
            {
                Button bt = Prepare_ButtonWithStyle(st, 18, soi.imageSource, soi.name);
                bt.Tag = soi.parsingName;
                bt.Click += new RoutedEventHandler(Button_OpenProgram);
                stpnl.Children.Add(bt);
            }
            if (dirs.Count() == 0 && objs.Count == 0)
            {
                stpnl.Children.Add(Prepare_ButtonWithStyle(st, 18, null, Properties.Resources.StringNoneInFolder));
            }

            return stpnl;
        }

        void Button_OpenProgram(object sender, RoutedEventArgs e)
        {
            this.Deactivated -= Window_Deactivated;
            Control bt = (Control)sender;
            try
            {
                System.Diagnostics.Process.Start((string)bt.Tag);
            }
            catch(Exception exc)
            {
                MessageBox.Show(Properties.Resources.StringExceptionCatched+ "\n"+Properties.Resources.StringTarget+":" + (string)bt.Tag + "\n" + exc, Properties.Resources.StringFileCantOpen);
            }
            RecentXml_AddEntry(bt.Tag as string);
            this.Close();
        }

        void RecentXml_AddEntry(string s)
        {
            if (System.IO.File.Exists(xmlPathRecent))
            {
                XmlRecentProgram.recentProgram[] rcs = RecentUsedProgram.Items;
                int lastPoint = -1;
                bool found = false;
                string newpath = s;
                List<XmlRecentProgram.recentProgram> rcnew = new List<XmlRecentProgram.recentProgram>();
                List<XmlRecentProgram.recentProgram> rcout = new List<XmlRecentProgram.recentProgram>();
                List<XmlRecentProgram.recentProgram> rckeep = new List<XmlRecentProgram.recentProgram>();
                if (rcs != null)
                {
                    foreach (XmlRecentProgram.recentProgram rc in rcs)
                    {
                        if (System.IO.File.Exists(rc.href))
                        {
                            if (newpath == rc.href)
                            {
                                found = true;
                                rc.point = (int.Parse(rc.point) + 1).ToString();
                            }
                            if (lastPoint != -1 || lastPoint <= int.Parse(rc.point))
                            {
                                rcnew.Add(rc);
                            }
                            else
                            {
                                rckeep.Add(rc);
                            }
                        }
                    }
                }
                if (!found)
                {
                    XmlRecentProgram.recentProgram rcp = new XmlRecentProgram.recentProgram();
                    rcp.href = newpath;
                    rcp.point = "1";
                    rcnew.Add(rcp);
                }
                if (rckeep != null)
                {
                    foreach (XmlRecentProgram.recentProgram rc in rckeep)
                    {
                        if (rcout.Count > 30) break;
                        if (rcnew != null)
                        {
                            foreach (XmlRecentProgram.recentProgram rcc in rcnew)
                            {
                                if (int.Parse(rc.point) < int.Parse(rcc.point))
                                {
                                    rcout.Add(rcc);
                                    rcnew.Remove(rcc);
                                }
                            }
                        }
                        rcout.Add(rc);
                    }
                }
                rcout.AddRange(rcnew);
                XmlRecentProgram.recent recentout = new XmlRecentProgram.recent();
                recentout.Items = rcout.ToArray();
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(XmlRecentProgram.recent));
                using (System.IO.FileStream fs = new System.IO.FileStream(xmlPathRecent, System.IO.FileMode.Create))
                {
                    serializer.Serialize(fs, recentout);
                }
            }

        }

        void Button_PrepareStackPanel(object sender, RoutedEventArgs e)
        {
            Button bt = (Button)sender;
            ProgramAllContainerTag pact = (ProgramAllContainerTag)bt.Tag;
            LoadProgramAll(pact.stackPanel,pact.dir);
            pact.stackPanel.Visibility = Visibility.Visible;
            bt.Click -= new RoutedEventHandler(Button_PrepareStackPanel);
            bt.Click += new RoutedEventHandler(Button_OpenCloseStackPanel);
        }

        void Button_OpenCloseStackPanel(object sender, RoutedEventArgs e)
        {
            Button bt = (Button)sender;
            ProgramAllContainerTag pact = (ProgramAllContainerTag)bt.Tag;
            if (pact.stackPanel.Visibility == Visibility.Visible)
            {
                pact.stackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                pact.stackPanel.Visibility = Visibility.Visible;
            }
        }

        Button Prepare_ButtonWithStyle(Style st, int IconWidth,ImageSource ims,string name)
        {
            Button bt = new Button();
            bt.Style = st;
            Grid grd = new Grid();
            bt.Content = grd;
            ColumnDefinition cd1 = new ColumnDefinition();
            ColumnDefinition cd2 = new ColumnDefinition();
            cd1.Width = new GridLength(IconWidth, GridUnitType.Pixel);
            cd2.Width = new GridLength(1, GridUnitType.Star);
            grd.ColumnDefinitions.Add(cd1);
            grd.ColumnDefinitions.Add(cd2);
            Image img = new Image();
            TextBlock tb = new TextBlock();
            tb.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(img, 0);
            Grid.SetColumn(tb, 1);
            if(ims!=null)img.Source = ims;
            tb.Text = name;
            grd.Children.Add(img);
            grd.Children.Add(tb);
            return bt;
        }
        #endregion

        #region 検索関係
        /// <summary>
        /// 検索の中止を別スレッドの検索関数に知らせるためのグローバル変数。
        /// </summary>
        bool ContinueSearching = false;
        //private System.Threading.Thread backgroundSearchThread = null;
        //string SearchText = null;

//        System.Threading.CancellationToken ct = new System.Threading.CancellationToken();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ContinueSearching = false;
            SearchScrollViewer.MaxHeight = SearchScrollViewer.ActualHeight;
            TextBox tb = (TextBox)sender;
            if (String.IsNullOrWhiteSpace(tb.Text))
            {
                Panel.SetZIndex(SearchScrollViewer, 0);
                return;
            }
            else if (tb.Text.Contains(@":\"))
            {
                string st = tb.Text;
                Style stl = (Style)FindResource("SearchResultButton");
                if (System.IO.File.Exists(st))
                {
                    SearchResultView.Children.Clear();
                    addSearchResult(ShellObject.FromParsingName(st), stl);
                }
                else
                {
                    SearchResultView.Children.Clear();
                    System.Text.RegularExpressions.Match mc = System.Text.RegularExpressions.Regex.Match(tb.Text, @"^.+\\");
                    string fn= System.Text.RegularExpressions.Regex.Replace(tb.Text, @"^.+\\", "");
                    if (mc.Success)
                    {
                        if (System.IO.Directory.Exists(mc.Value))
                        {
                            foreach(ShellObject so in (ShellContainer)ShellContainer.FromParsingName(mc.Value)){
                                if(so.Name.StartsWith(fn))addSearchResult(so, stl);
                            }
                        }
                    }
                }
            }
            else
            {
                Panel.SetZIndex(SearchScrollViewer, 3);

                ////普通のスレッドを利用した場合。
                ////動作はするがAbortにより例外が発生。ただし、文字入力は楽に進む。
                ////スレッド作成コストがあるので一文字ずつ動作させると検索はむしろ検索は重くなる。
                ////以上の問題より不採用。
                //if (backgroundSearchThread != null)
                //    backgroundSearchThread.Abort();

                //SearchText = tb.Text;
                //// Create a background thread to do the search
                //backgroundSearchThread = new System.Threading.Thread(new System.Threading.ThreadStart(SearchWord));
                //// ApartmentState.STA is required for COM
                //backgroundSearchThread.SetApartmentState(System.Threading.ApartmentState.STA);
                //backgroundSearchThread.Start();

                //同じスレッドで実行した場合。
                //SearchWord(tb.Text);

                //Dispatcherを利用した場合。
                //特に問題は発生しないが速度が上がったようには思えない。
                Dispatcher.BeginInvoke(new Action<string>(SearchWord), tb.Text);

                //スレッドプールを利用した場合。
                //var waitCallback = new System.Threading.WaitCallback(SearchWord);
                //System.Threading.ThreadPool.QueueUserWorkItem(waitCallback, tb.Text);
            }
        }

        private void SearchWord(string st)//or(object o)or()
        {
            //string st = (string)o;
            //string st = SearchText;
            ContinueSearching = true;

            //クエリーとして評価する場合
            //普通に検索はできるのだが、ディレクトリ名(C:\ProgramData\Microsoft\Windows\Start Menu\Programs等)が含む文字に反応してしまうので使用中止。
            //SearchCondition searchCondition = SearchConditionFactory.ParseStructuredQuery(st, new System.Globalization.CultureInfo("ja-Jp"));

            //ファイル名・日本語ファイル名・親ファイル名で評価する場合。
            //Windows 8環境で例外発生。アンマネージドコードに問題があるようなので対処は不可能と判断。
            //SearchCondition searchCondition =
            //    SearchConditionFactory.CreateAndOrCondition(SearchConditionType.Or, false,
            //    SearchConditionFactory.CreateLeafCondition(
            //    Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System.FileName, st,
            //    SearchConditionOperation.ValueContains),
            //    SearchConditionFactory.CreateLeafCondition(
            //    Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System.ItemNameDisplay, st,
            //    SearchConditionOperation.ValueContains),
            //    SearchConditionFactory.CreateLeafCondition(
            //    Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System.ItemFolderNameDisplay, st,
            //    SearchConditionOperation.ValueContains)
            //    );

            //ファイル名のみで判断。
            //この場合、ローカルネーム(ex."ファイル名を指定して実行")での検索は不可能になります。
            SearchCondition searchCondition =
                SearchConditionFactory.CreateLeafCondition(
                Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System.FileName, st,
                SearchConditionOperation.ValueContains);

            //検索するフォルダを指定。この場合、ユーザー毎と共有のプログラムフォルダ。
            ShellSearchFolder searchFolder = new ShellSearchFolder(searchCondition
                , (ShellContainer)KnownFolders.Programs,(ShellContainer)KnownFolders.CommonPrograms
                );

            try
            {
                if (ContinueSearching)
                {
                    UpdateSearchResult(searchFolder);
                }
            }
            finally
            {
                searchFolder.Dispose();
                searchFolder = null;
            }
        }

        /// <summary>
        /// 検索結果をSearchResultViewに表示する関数。
        /// </summary>
        /// <param name="result">検索方法の定義</param>
        private void UpdateSearchResult(ShellSearchFolder result)
        {

            List<string> pars = new List<string>();
            Dictionary<string, ImageSource> ims = new Dictionary<string, ImageSource>();
            Dictionary<string, string> nm = new Dictionary<string, string>();
    
            foreach (ShellObject so in result)
            {
                string parnam;
                parnam= so.ParsingName;
                //下の行は三回呼ばれています。改変時には注意してください。
                //マルチスレッド処理用です。
                if (!ContinueSearching) { return; }//本来は例外を発生すべきだが、成否判断は必要ないのでreturn.
                pars.Add(parnam);
                if (!ContinueSearching) { return; }
                BitmapSource bms=so.Thumbnail.SmallBitmapSource;
                bms.Freeze();
                ims.Add(parnam, bms);
                if (!ContinueSearching) { return; }
                nm.Add(parnam, so.Name);
            }
            //SearchResultView.Dispatcher.BeginInvoke(new Action<List<string>, Dictionary<string, ImageSource>, Dictionary<string, string>>((rs, im, nam) =>{
                        Style st = (Style)FindResource("SearchResultButton");
                        SearchResultView.Children.Clear();
                        //foreach (string so in rs)
                        foreach (string so in pars)
                        {
                            addSearchResult(so, st, ims[so], nm[so]);
                            //addSearchResult(so, st, im[so], nam[so]);
                        }
            //        }), pars, ims, nm);
        }

        private void addSearchResult(ShellObject so,Style st)
        {
            ImageSource im = so.Thumbnail.SmallBitmapSource;
            im.Freeze();
            addSearchResult(so.ParsingName, st, im, so.Name); 
        }

        private void addSearchResult(String so, Style st, ImageSource ims, string nm)
        {
            Button bt = Prepare_ButtonWithStyle(st, 18, ims, nm);
            bt.Tag = so;
            bt.Click += new RoutedEventHandler(Button_OpenTagedFile);
            SearchResultView.Children.Add(bt);
        }
        #endregion        
    }
}
