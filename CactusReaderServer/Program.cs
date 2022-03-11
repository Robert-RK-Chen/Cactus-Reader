using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace CactusReaderService
{
    class Program
    {
        static void Main(string[] args)
        {
            Uri localUri = new Uri("http://106.54.173.192:9527");
            // 开始运行WCF服务
            using (WebServiceHost host = new WebServiceHost(typeof(Service), localUri))
            {
                // 配置缓冲区的最大值
                WebHttpBinding binding = new WebHttpBinding
                {
                    MaxReceivedMessageSize = 500 * 1024 * 1024
                };
                host.AddServiceEndpoint(typeof(IService), binding, "");

                host.Opened += (a, b) => Console.WriteLine("服务已启动。");
                host.Closed += (a, b) => Console.WriteLine("服务已关闭。");
                try
                {
                    // 打开服务
                    host.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                Console.ReadKey();
            }
        }
    }

    [ServiceContract]
    public interface IService
    {
        [OperationContract, WebInvoke(UriTemplate = "/upload-profile-image")]
        void UploadProfileImage(Stream content);
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class Service : IService
    {
        readonly static string profilePath =
            @"C:\Storages\Environment\apache-tomcat-10.0.17\webapps\cactus-reader-repo\";

        public void UploadProfileImage(Stream content)
        {
            IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;

            // 从标头获取用户 UID
            string UID = request.Headers["UID"];

            // 开始接收文件
            try
            {
                // 获取用户文档库位置
                string imgPath = profilePath + UID + @"\";
                string newFilePath = Path.Combine(imgPath, "ProfilePicture.PNG");

                // 如果文件存在，将其删除
                if (File.Exists(newFilePath))
                {
                    File.Delete(newFilePath);
                }

                using (FileStream fileStream = File.OpenWrite(newFilePath))
                {
                    // 从客户端上传的流中将数据复制到文件流中
                    content.CopyTo(fileStream);
                }

                Console.WriteLine(string.Format("在{0}成功接收文件。", DateTime.Now.ToLongTimeString()));
                // 向客户端发送回应消息
                WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                // 处理错误
                Console.WriteLine(ex.Message);
                WebOperationContext.Current.OutgoingResponse.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                WebOperationContext.Current.OutgoingResponse.StatusDescription = ex.Message;
            }
        }
    }
}
