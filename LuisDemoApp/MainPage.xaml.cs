// /* 
// *  Copyright (c) 2018-2018  Hiroaki Fujii All rights reserved. Licensed under the MIT license. 
// *  See LICENSE in the source repository root for complete license information. 
// */

#region using

using System;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Cognitive.LUIS;

#endregion

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace LuisDemoApp
{
    /// <summary>
    ///     メインページ。
    /// </summary>
    public sealed partial class MainPage
    {
        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            Loaded += _Load;
            btnSend.Click += _Click;
        }

        #region イベント

        /// <summary>
        ///     ロード時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _Load(object sender, RoutedEventArgs e)
        {
            var loader = new ResourceLoader();

            txtCommand.Text = "";
            lblDisplay.Text = "";
            btnSend.Content = loader.GetString("LABEL_SEND");
        }

        /// <summary>
        ///     ボタンをクリック時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void _Click(object sender, RoutedEventArgs e)
        {
            var loader = new ResourceLoader();
            // コマンドの入力チェック
            var command = txtCommand.Text.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                try
                {
                    lblDisplay.Text = "";
                    // LUISにコマンドを送信する
                    var client = new LuisClient(Constants.APP_ID, Constants.LUIS_KEY, true, Constants.APP_DOMAIN);
                    var result = await client.Predict(command);
                    // 一致率の高い順にソートして、先頭のデータを取得
                    var intent = result.Intents.OrderByDescending(i => i.Score).ElementAt(0);

                    // LUISから取得した分類に応じた画像を表示
                    imgFile.Source = null;
                    string uri;
                    if (intent.Name == "とんかつ")
                        uri = "ms-appx:///images/tonkatsu.png";
                    else if (intent.Name == "エビフライ")
                        uri = "ms-appx:///Images/friedprawns.png";
                    else if (intent.Name == "魚フライ")
                        uri = "ms-appx:///Images/friedfish.png";
                    else if (intent.Name == "カキフライ")
                        uri = "ms-appx:///Images/friedoysters.png";
                    else if (intent.Name == "コロッケ")
                        uri = "ms-appx:///Images/croquette.png";
                    else if (intent.Name == "牛丼")
                        uri = "ms-appx:///Images/beefriceboarl.png";
                    else
                        uri = "ms-appx:///Images/beefriceboarl.png";

                    var image = new BitmapImage(new Uri(uri));
                    imgFile.Source = image;
                    lblDisplay.Text = loader.GetString("LABEL_ORDERED");

                }
                catch (Exception ex)
                {
                    var dialog = new MessageDialog(ex.Message);
                    await dialog.ShowAsync();
                }
            }
            else
            {
                var dialog = new MessageDialog(loader.GetString("MSG_NO_COMMAND"));
                await dialog.ShowAsync();
            }
        }

        #endregion
    }
}