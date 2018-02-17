// /* 
// *  Copyright (c) 2018-2018  Hiroaki Fujii All rights reserved. Licensed under the MIT license. 
// *  See LICENSE in the source repository root for complete license information. 
// */

#region using

using System;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Cognitive.CustomVision.Prediction;
using VisonDemoApp.Helper;

#endregion

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace VisonDemoApp
{
    /// <summary>
    ///     メインページ。
    /// </summary>
    public sealed partial class MainPage
    {
        private ICameraHelper _cameraHelper;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            Application.Current.Resuming += _ResumeApp;
            Application.Current.Suspending += _SuspendApp;

            Loaded += _Loaded;
            Unloaded += _Unloaded;
            btnShot.Click += _Click;
        }

        #region イベント

        /// <summary>
        ///     アプリケーションレジューム時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _ResumeApp(object sender, object e)
        {
            await _cameraHelper.InitializeAsync();
            await _cameraHelper.StartPreview();
        }

        /// <summary>
        ///     アプリケーションサスペンド時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _SuspendApp(object sender, object e)
        {
            _cameraHelper.Dispose();
        }

        /// <summary>
        ///     ロード時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Loaded(object sender, RoutedEventArgs e)
        {
            // フレームの戻るボタンを無効にする
            if (Frame.CanGoBack)
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility
                    = AppViewBackButtonVisibility.Collapsed;
            var loader = new ResourceLoader();
            txtDisplay.Text = string.Empty;
            btnShot.Content = loader.GetString("LABEL_SHOT");

            // カメラの初期化処理とプレビュー開始
            if (_cameraHelper == null || !_cameraHelper.Initialized)
            {
                if (_cameraHelper == null)
                    _cameraHelper = new CameraHelper();
                await _cameraHelper.InitializeAsync();
            }

            ctlCapture.Source = _cameraHelper.MediaCapture;
            if (ctlCapture.Source != null)
                await _cameraHelper.StartPreview();
        }

        /// <summary>
        ///     アンロード時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Unloaded(object sender, RoutedEventArgs e)
        {
            // カメラの表示を停止
            var stopPreview = _cameraHelper?.StopPreview();
            if (stopPreview != null)
                await stopPreview;

            // 認証用に撮影した写真ファイルを削除
            var folder = ApplicationData.Current.TemporaryFolder;
            var files = await folder.GetFilesAsync();
            files.ToList().ForEach(async file =>
            {
                if (file.FileType.ToUpper() == ".JPG")
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            });
        }

        /// <summary>
        /// ボタンをクリックした時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Click(object sender, RoutedEventArgs e)
        {
            var button = (Button) sender;
            if (button.Name == "btnShot")
            {
                txtDisplay.Text = "";
                var loader = new ResourceLoader();
                try
                {
                    // カメラで撮影する
                    var file = await _cameraHelper.CapturePhoto();
                    using (var stream = File.OpenRead(file.Path))
                    {
                        // クライアント生成
                        var client = new PredictionEndpoint
                        {
                            BaseUri = new Uri(Constants.ENDPOINT),
                            ApiKey = Constants.VISION_KEY,
                        };
                        // 画像を送ってタグを取得する
                        var result = await client.PredictImageWithHttpMessagesAsync(
                                            new Guid(Constants.PROJECT_ID), 
                                            stream,
                                            new Guid(Constants.ITERATION_ID));
                        var predictions = result.Body.Predictions;
                        // 合致率が高い順にソートして先頭を取得
                        var prediction = predictions.ToArray().OrderByDescending(p => p.Probability).ElementAt(0);
                        string text;
                        if (prediction.Tag == "ペットボトル")
                            text = prediction.Tag + " " + loader.GetString("MSG_TRUSH");
                        else if (prediction.Tag == "缶")
                            text = prediction.Tag + " " + loader.GetString("MSG_STAMP");
                        else if (prediction.Tag == "瓶")
                            text = prediction.Tag + " " + loader.GetString("MSG_SMASH");
                        else
                            text = loader.GetString("LABEL_UNKNOWN");
                        // 表示
                        txtDisplay.Text = text;
                    }

                }
                catch (Exception exp)
                {
                    var dialog = new MessageDialog(exp.Message);
                    await dialog.ShowAsync();
                }
            }
        }

        #endregion
    }
}