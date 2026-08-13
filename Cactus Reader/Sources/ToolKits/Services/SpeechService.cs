using Microsoft.CognitiveServices.Speech;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 语音朗读原子操作：语音合成 → 保存为本地 wav 文件。
    /// 页面只负责播放与 Toast 提示，合成细节统一收敛到此服务。
    /// </summary>
    public static class SpeechService
    {
        /// <summary>合成语音到本地 wav 文件；成功返回文件，失败返回 null。</summary>
        public static async Task<StorageFile> SynthesizeToFileAsync(string text, string voiceName)
        {
            var config = SpeechConfig.FromSubscription("{subscriptionkey}", "{region}");
            config.SpeechSynthesisVoiceName = voiceName;

            using (var synthesizer = new SpeechSynthesizer(config, null))
            {
                using (var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false))
                {
                    if (result.Reason != ResultReason.SynthesizingAudioCompleted)
                    {
                        return null;
                    }

                    using (var audioStream = AudioDataStream.FromResult(result))
                    {
                        string filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "outputaudio.wav");
                        await audioStream.SaveToWaveFileAsync(filePath);
                        return await StorageFile.GetFileFromPathAsync(filePath);
                    }
                }
            }
        }
    }
}
