using System;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    public class ProfileUploadTool
    {
        readonly static string SERVER_IP_ADDRESS = "http://106.54.173.192:9527";

        private static ProfileUploadTool instance;

        public static ProfileUploadTool Instance
        {
            get
            {
                return instance ?? (instance = new ProfileUploadTool());
            }
        }

        private ProfileUploadTool() { }

        public async void RecoveryBackgroundTransfer()
        {
            var uploadTasks = await BackgroundUploader.GetCurrentUploadsAsync();
            if (uploadTasks.Count > 0)
            {
                UploadOperation uploadOpt = uploadTasks[0];
                SetUpLoad(uploadOpt, false);
            }
        }

        private async void SetUpLoad(UploadOperation opr, bool starting)
        {
            // 当上传进度更新时能收到报告
            Progress<UploadOperation> progressReporter = new Progress<UploadOperation>(OnProgressHandler);
            // 启动或附加任务
            try
            {
                if (starting)
                {
                    await opr.StartAsync().AsTask(progressReporter);
                }
                else
                {
                    await opr.AttachAsync().AsTask(progressReporter);
                }
            }
            catch (Exception ex)
            {
                var state = BackgroundTransferError.GetStatus(ex.HResult);
                System.Diagnostics.Debug.WriteLine("错误：" + state);
            }
        }

        private void OnProgressHandler(UploadOperation p)
        {
            BackgroundUploadProgress progress = p.Progress;
            switch (progress.Status)
            {
                case BackgroundTransferStatus.Canceled:
                    System.Diagnostics.Debug.WriteLine("任务已取消。");
                    break;
                case BackgroundTransferStatus.Completed:
                    System.Diagnostics.Debug.WriteLine("任务已完成。");
                    break;
                case BackgroundTransferStatus.Error:
                    System.Diagnostics.Debug.WriteLine("发生了错误。");
                    break;
                case BackgroundTransferStatus.Running:
                    System.Diagnostics.Debug.WriteLine("任务执行中。");
                    break;
            }
        }

        public async void UploadProfileImg(StorageFile file, string UID, string method)
        {
            int n = (await BackgroundUploader.GetCurrentUploadsAsync()).Count;
            if (n > 200)
            {
                return;
            }

            BackgroundUploader uploader = new BackgroundUploader();
            uploader.SetRequestHeader("UID", UID);
            uploader.Method = "POST";

            // 创建上传任务
            UploadOperation uploadOpt = uploader.CreateUpload(new Uri(SERVER_IP_ADDRESS + method), file);
            SetUpLoad(uploadOpt, true);
        }
    }
}
