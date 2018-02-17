// /* 
// *  Copyright (c) 2018-2018  Hiroaki Fujii All rights reserved. Licensed under the MIT license. 
// *  See LICENSE in the source repository root for complete license information. 
// */

#region using

using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Popups;

#endregion

namespace VisonDemoApp.Helper
{
    /// <inheritdoc />
    /// <summary>
    ///     カメラ処理ヘルパーインターフェイス
    /// </summary>
    public interface ICameraHelper : IDisposable
    {
        MediaCapture MediaCapture { get; set; }

        bool Initialized { get; }

        /// <summary>
        ///     初期化する
        /// </summary>
        /// <returns></returns>
        Task InitializeAsync();

        /// <summary>
        ///     カメラのプレビュー表示開始
        /// </summary>
        /// <returns></returns>
        Task StartPreview();

        /// <summary>
        ///     カメラのプレビュー表示終了
        /// </summary>
        /// <returns></returns>
        Task StopPreview();

        Task<StorageFile> CapturePhoto(string cardId = "Vision");
    }

    /// <inheritdoc />
    /// <summary>
    ///     カメラ処理ヘルパークラス
    /// </summary>
    public class CameraHelper : ICameraHelper
    {
        public MediaCapture MediaCapture { get; set; }

        public bool Initialized { get; private set; }

        /// <inheritdoc />
        /// <summary>
        ///     アンマネージ リソースの解放またはリセットに関連付けられているアプリケーション定義のタスクを実行します。
        /// </summary>
        public async void Dispose()
        {
            try
            {
                if (MediaCapture != null)
                    await MediaCapture.StopPreviewAsync();
                MediaCapture?.Dispose();
                MediaCapture = null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     初期化する
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            if (MediaCapture == null)
            {
                var cameraDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (cameraDevices.Count > 0)
                {
                    // カメラの初期化
                    var camera = cameraDevices[0];
                    var settings = new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = camera.Id,
                        PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                    };
                    MediaCapture = new MediaCapture();
                    await MediaCapture.InitializeAsync(settings);
                    MediaCapture.Failed += _FailMediaCapture;
                    MediaCapture.VideoDeviceController.PrimaryUse = CaptureUse.Video;

                    // 解像度の設定
                    var resolutions =
                        MediaCapture.VideoDeviceController
                            .GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview)
                            .OfType<ImageEncodingProperties>()
                            .ToList();
                    if (resolutions.Count > 0 && resolutions.Count < 3)
                    {
                        // 解像度が2段階以下の場合、最低の解像度に設定
                        await MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(
                            MediaStreamType.VideoPreview,
                            resolutions[0]);
                    }
                    else if (resolutions.Count > 2)
                    {
                        // 解像度が2段階以上の場合、中間の解像度に設定
                        var max = resolutions[resolutions.Count - 1];
                        var pos = Convert.ToInt32(Math.Floor(Convert.ToDecimal(max) / 2));
                        await MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(
                            MediaStreamType.VideoPreview,
                            resolutions[pos]);
                    }

                    Initialized = true;
                }
                else
                {
                    Initialized = false;
                }
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     カメラのプレビュー表示開始
        /// </summary>
        /// <returns></returns>
        public async Task StartPreview()
        {
            if (Initialized)
            {
                await MediaCapture.StartPreviewAsync();
            }
            else
            {
                await InitializeAsync();
                await MediaCapture.StartPreviewAsync();
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     カメラのプレビュー表示終了
        /// </summary>
        /// <returns></returns>
        public async Task StopPreview()
        {
            await MediaCapture.StopPreviewAsync();
        }

        /// <summary>
        ///     撮影する
        /// </summary>
        /// <param name="cardId">ICカードID</param>
        /// <returns>写真ファイル</returns>
        public async Task<StorageFile> CapturePhoto(string cardId = "Vision")
        {
            var filename = cardId + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg";
            var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(filename,
                CreationCollisionOption.GenerateUniqueName);
            await MediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

            return file;
        }

        /// <summary>
        ///     メディア処理に失敗時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void _FailMediaCapture(MediaCapture sender, MediaCaptureFailedEventArgs e)
        {
            var dialog = new MessageDialog(e.Message);
            await dialog.ShowAsync();
        }
    }
}