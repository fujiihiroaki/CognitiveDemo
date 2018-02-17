// /* 
// *  Copyright (c) 2018-2018  Hiroaki Fujii All rights reserved. Licensed under the MIT license. 
// *  See LICENSE in the source repository root for complete license information. 
// */

#region using

using System;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using FaceDemoApp.Helper;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

#endregion

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace FaceDemoApp
{
    /// <summary>
    ///     人物情報登録ページ
    /// </summary>
    public sealed partial class UpdatePerson
    {
        /// <summary>
        ///     カメラ処理ヘルパー
        /// </summary>
        private ICameraHelper _cameraHelper;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        public UpdatePerson()
        {
            InitializeComponent();

            Application.Current.Resuming += _ResumeApp;
            Application.Current.Suspending += _SuspendApp;

            Loaded += _Load;
            Unloaded += _Unloaded;
            btnClose.Click += _Click;
            btnUpdate.Click += _Click;
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
        private async void _Load(object sender, RoutedEventArgs e)
        {
            var loader = new ResourceLoader();
            lblName.Text = loader.GetString("LABEL_NAME");
            txtName.Text = "";
            btnClose.Content = loader.GetString("LABEL_CLOSE");
            btnUpdate.Content = loader.GetString("LABEL_UPDATE");

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
        }

        /// <summary>
        ///     ボタンをクリックされたときの処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void _Click(object sender, RoutedEventArgs e)
        {
            var control = (Button) sender;
            if (control.Name == "btnClose")
                Frame.GoBack();
            else if (control.Name == "btnUpdate")
                if (!string.IsNullOrEmpty(txtName.Text))
                {
                    _Shot();
                }
                else
                {
                    var dialog = new MessageDialog("ERROR");
                    await dialog.ShowAsync();
                }
        }

        #endregion

        #region private method

        /// <summary>
        ///     撮影する
        /// </summary>
        private async void _Shot()
        {
            // カメラで撮影（ローカルに保存）
            var folder = ApplicationData.Current.TemporaryFolder;
            var file = await folder.CreateFileAsync("temp.jpg", CreationCollisionOption.ReplaceExisting);
            await _cameraHelper.MediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            try
            {
                // Face APIクライアントを生成
                var client = new FaceServiceClient(Constants.FACE_KEY, Constants.FACE_ENDPOINT);

                var loader = new ResourceLoader();
                // 認識した顔の数をチェックする
                Face[] f;
                using (var stream = File.OpenRead(file.Path))
                {
                    f = await client.DetectAsync(stream, false);
                }
                if (f.Length == 0)
                {
                    var dialog = new MessageDialog(loader.GetString("NO_FACES"));
                    await dialog.ShowAsync();
                    return;
                }

                // ユーザ情報を追加/更新
                var name = txtName.Text.Trim();
                var persons = await client.ListPersonsAsync(Constants.TARGET_GROUP);
                var person = persons.FirstOrDefault(p => p.Name == name);
                if (person == null)
                {
                    // 追加処理　Azure Cognitiveサービスにユーザ情報を追加
                    var result = await client.CreatePersonAsync(Constants.TARGET_GROUP, name, "");
                    person = new Person {Name = name, UserData = "", PersonId = result.PersonId};
                    // 追加処理　Azure Cognitiveサービスに画像ファイルをアップロードし学習
                    using (var stream = File.OpenRead(file.Path))
                    {
                        var faceResult = await client.AddPersonFaceAsync(Constants.TARGET_GROUP, result.PersonId, stream);
                        person.PersistedFaceIds = new[] {faceResult.PersistedFaceId};
                    }
                    // ユーザグループ情報を更新
                    await client.TrainPersonGroupAsync(Constants.TARGET_GROUP);
                }
                else
                {
                    // ユーザ情報の更新　Azure Cognitiveサービスに画像ファイルをアップロードし学習
                    if (!string.IsNullOrEmpty(file.Path))
                    {
                        using (var stream = File.OpenRead(file.Path))
                        {
                            var faceResult =
                                await client.AddPersonFaceAsync(Constants.TARGET_GROUP, person.PersonId, stream);
                            person.PersistedFaceIds
                                = person.PersistedFaceIds.Concat(new[] {faceResult.PersistedFaceId}).ToArray();
                        }
                        
                    }
                }
                var message = new MessageDialog(loader.GetString("MSG_COMPLETE"));
                await message.ShowAsync();

            }
            catch (FaceAPIException e)
            {
                var dialog = new MessageDialog(e.ErrorMessage);
                await dialog.ShowAsync();
            }
            catch (Exception e)
            {
                var dialog = new MessageDialog(e.Message);
                await dialog.ShowAsync();
            }
        }

        #endregion
    }
}