#Menu For 8について

##はじめに
Menu For 8のソースコードです。  
これはWindows 8向けのスタートメニュークローン・ランチャーです。  
Windows 8.1でスタートメニューが復活したため、**このソフトは現在適切に動作しません。**私も使ってません。

WindowsAPICodePack関係のファイルを除いてあります。  
実際にコンパイルするには、以下のサイトからWindows(R) API Code Pack for Microsoft(R) .NET Framework ver 1.1をダウンロード、展開し、参照を追加しなおしてください。  
http://archive.msdn.microsoft.com/WindowsAPICodePack  
setup.zipには含むため、一部別のライセンスが適用されます。

またlogo内には、ロゴのicoファイルとPaint.net用の画像ファイルがあります。  
ico4.pdnはレイヤー等の情報が含まれています。  
商標等に細心の注意を払い利用してください。  

##注意
* リソース(Properties/Resources.resx)からコード(Properties/Resources.Designer.cs)を自動作成するResGen.exeに問題があり、コンストラクタ(Properties/Resources.Designer.cs内Resources())をinternalで作成するようです。この時、場合によって正常にコンパイルできなくなります。  
本来はアクセッサークラスを作成すべきですが、暫定的に``internal Resources() {``
をこのように``public Resources() {``
書き換えています。  
Properties/Resourcesのコンストラクタが見当たらないとの例外が発生した場合には(Mainwindow.xamlで発生します)同様に書き換えてみてください。  
  参考:http://connect.microsoft.com/VisualStudio/feedback/details/628281/silverlight-wp7-resource-files-and-binding
* 当初から名前を変更したため、名称に関して若干の混乱が発生しています。このソフトの名前はMenu For 8です。これは名称内にMicrosoft製品の名前を含まない・誤解を与えない事を目的とするものです。しかし、名前空間やクラス名などではStartMenuForWin8が使われています。あくまで開発中の名前で現在のソフトウェアの名前ではありません。ご注意ください。

## 対象環境
* Microsoft Visual Studio 2010シリーズまたはVisual C# 2010 Express。
* 実行はWindows 8上を想定しています。Windows 7より前のOSでは正常に動作しない可能性もあります。
* Windows 8.1以降ではスタートボタンが復活したため動作しません。

##連絡
以下のメールアドレスにどうぞ。  
kurema_makoto_software@yahoo.co.jp

##アンインストール
通常通り「プログラムのアンインストール」等の項目から削除できます。

---------------------------------------------
商標等について。
Microsoft は米国 Microsoft Corporation の米国およびその他の国における登録商標または商標です。  
Windows、Windows Server、Windows Vista、Windows 7 は米国 Microsoft Corporation の米国およびその他の国における登録商標または商標です。  
Visual Studio、Visual Basic、Visual C#、Visual C++ は米国 Microsoft Corporation の米国およびその他の国における登録商標または商標です。  
.NET Framework は米国 Microsoft Corporation の米国およびその他の国における登録商標または商標です。  
Windowsの正式名称は、Microsoft Windows Operating Systemです。  
Windows 8は現時点での開発コードネームです。将来名称が変更される可能性があります。  
その他、本ソフトウェアに記載の製品名は、各社の商標または登録商標です。  
