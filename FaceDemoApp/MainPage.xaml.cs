// /* 
// *  Copyright (c) 2018-2018  Hiroaki Fujii All rights reserved. Licensed under the MIT license. 
// *  See LICENSE in the source repository root for complete license information. 
// */

#region using

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using FaceDemoApp.Helper;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

#endregion

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace FaceDemoApp
{
    /// <summary>
    ///     メインページ
    /// </summary>
    public sealed partial class MainPage
    {
        private ICameraHelper _cameraHelper;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            Application.Current.Resuming += _ResumeApp;
            Application.Current.Suspending += _SuspendApp;

            Loaded += _Loaded;
            Unloaded += _Unloaded;
            btnAdd.Click += _Click;
            btnStart.Click += _Click;
        }

        #region イベント
      
        /// <summary>
        /// アプリケーションレジューム時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _ResumeApp(object sender, object e)
        {
            await _cameraHelper.InitializeAsync();
            await _cameraHelper.StartPreview();
        }

        /// <summary>
        /// アプリケーションサスペンド時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _SuspendApp(object sender, object e)
        {
            _cameraHelper.Dispose();
        }

        /// <summary>
        /// ロード時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Loaded(object sender, RoutedEventArgs e)
        {
            // フレームの戻るボタンを無効にする
            if (Frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility
                    = AppViewBackButtonVisibility.Collapsed;
            }
            var loader = new ResourceLoader();
            lblAge.Text = loader.GetString("LABEL_AGE");
            lblName.Text = loader.GetString("LABEL_NAME");
            lblRate.Text = String.Empty;
            btnAdd.Content = loader.GetString("LABEL_ADD");
            btnStart.Content = loader.GetString("LABEL_DETECT");

            // カメラの初期化処理とプレビュー開始
            if (_cameraHelper == null || !_cameraHelper.Initialized)
            {
                if (_cameraHelper == null)
                    _cameraHelper = new CameraHelper();
                await _cameraHelper.InitializeAsync();
            }
            ctlCameraFeed.Source = _cameraHelper.MediaCapture;
            if (ctlCameraFeed.Source != null)
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
        ///     ボタンをクリックされたときの処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Click(object sender, RoutedEventArgs e)
        {
            var control = (Button) sender;
            if (control.Name == "btnAdd")
            {
                Frame.Navigate(typeof(UpdatePerson));
            }
            else if (control.Name == "btnStart")
            {
                var loader = new ResourceLoader();
                lblName.Text = loader.GetString("LABEL_NAME");
                lblAge.Text = loader.GetString("LABEL_AGE");
                lblRate.Text = "";
                var detect = false;
                while (_cameraHelper.MediaCapture.CameraStreamState == CameraStreamState.Streaming)
                {
                    // カメラで撮影（ローカルに保存）
                    var folder = ApplicationData.Current.TemporaryFolder;
                    var file = await folder.CreateFileAsync("temp.jpg", CreationCollisionOption.ReplaceExisting);
                    await _cameraHelper.MediaCapture.CapturePhotoToStorageFileAsync(
                        ImageEncodingProperties.CreateJpeg(), file);

                    try
                    {
                        // Face APIクライアントを生成
                        var client = new FaceServiceClient(Constants.FACE_KEY, Constants.FACE_ENDPOINT);
                        // 写真から顔を検知
                        using (var stream = File.OpenRead(file.Path))
                        {
                            // 年齢情報つきで顔を検知
                            var faces = await client.DetectAsync(stream, true, false, new []{ FaceAttributeType.Age });
                            if (faces != null &&faces.Length > 0)
                            {
                                var guids = _FacesToGuids(faces);
                                // 登録済みの顔か判断する
                                var result = await client.IdentifyAsync(Constants.TARGET_GROUP, guids);
                                if (result != null && result.Length > 0)
                                {
                                    // 一致率の高い人の情報を取得する
                                    var person = await client.GetPersonAsync(Constants.TARGET_GROUP, result[0].Candidates[0].PersonId);
                                    if (person != null)
                                    {
                                        lblName.Text = loader.GetString("LABEL_NAME") + " " + person.Name;
                                        lblAge.Text = loader.GetString("LABEL_AGE") + " " 
                                            + faces.First( f => f.FaceId == result[0].FaceId ).FaceAttributes.Age;
                                        lblRate.Text = result[0].Candidates[0].Confidence.ToString("##0.000%");
                                        detect = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (FaceAPIException exp)
                    {
                        var dialog = new MessageDialog(exp.ErrorMessage);
                        await dialog.ShowAsync();
                        return;
                    }

                    if (detect)
                        return;

                    await Task.Delay(200);
                }
            }
        }

        #endregion

        #region private method

        /// <summary>
        /// FaceオブジェクトからGUIDに変換する
        /// </summary>
        /// <param name="faces">Faceオブジェクト配列</param>
        /// <returns>GUID配列</returns>
        private static Guid[] _FacesToGuids(Face[] faces)
        {
            var guids = new Guid[faces.Length];
            for (var i = 0; i < faces.Length; i++)
            {
                guids[i] = faces[i].FaceId;
            }
            return guids;
        }

        #endregion
    }
}