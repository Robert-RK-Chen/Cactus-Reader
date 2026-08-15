using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.MediaProperties;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 语音朗读原子操作：流式合成 → 边接收边播放。
    /// 底层使用 Xiaomi MiMo 语音合成服务（MiMo-V2.5-TTS，OpenAI 兼容流式接口）。
    /// 页面只负责把返回的 MediaStreamSource 挂到播放器并播放，无需等待整段合成完成。
    /// </summary>
    public static class SpeechService
    {
        // Xiaomi MiMo 语音合成 API：https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/audio/speech-synthesis-v2.5
        private const string MiMoEndpoint = "https://api.xiaomimimo.com/v1/chat/completions";
        private const string MiMoModel = "mimo-v2.5-tts";

        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        /// <summary>
        /// 流式合成语音，返回可立即开始播放的 MediaStreamSource（PCM 24kHz / 16bit / 单声道）。
        /// 音频在后台边接收边提供给播放器，显著缩短首字出声的等待时间。
        /// </summary>
        /// <param name="text">待合成文本。</param>
        /// <param name="voiceName">预置音色，如 冰糖 / 茉莉 / 苏打 / 白桦 / Mia / Chloe / Milo / Dean。</param>
        /// <param name="style">发音风格（可选），如 温柔 / 活泼 / 严肃；传空字符串表示不指定风格。</param>
        /// <param name="speed">语速（1.0 为正常；&lt;0.9 放慢，&gt;1.1 加快，映射为 MiMo 语速标签）。</param>
        /// <param name="tune">音调（1.0 为正常；&lt;0.9 降低，&gt;1.1 升高，映射为 MiMo 音调标签）。</param>
        /// <returns>可直接设置到 MediaPlayer 的 MediaStreamSource；失败返回 null。</returns>
        public static async Task<MediaStreamSource> CreateStreamingSourceAsync(
            string text, string voiceName, string style = "", double speed = 1.0, double tune = 1.0)
        {
            // MiMo 标签控制：多个标签置于同一对括号内，整体放在待合成文本开头：(标签1 标签2)待合成内容
            var tags = new List<string>();
            if (!string.IsNullOrEmpty(style)) { tags.Add(style); }
            string speedTag = GetSpeedTag(speed);
            if (!string.IsNullOrEmpty(speedTag)) { tags.Add(speedTag); }
            string tuneTag = GetTuneTag(tune);
            if (!string.IsNullOrEmpty(tuneTag)) { tags.Add(tuneTag); }

            string content = tags.Count > 0 ? $"({string.Join(" ", tags)}){text}" : text;

            var requestBody = new JObject
            {
                ["model"] = MiMoModel,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "user", ["content"] = "" },
                    new JObject { ["role"] = "assistant", ["content"] = content }
                },
                ["audio"] = new JObject
                {
                    // 流式输出须指定 pcm16，便于逐块拼接为连续音频
                    ["format"] = "pcm16",
                    ["voice"] = voiceName
                },
                ["stream"] = true
            };

            try
            {
                // API Key 从设置（Windows 凭据保险箱）读取，不再硬编码
                string apiKey = SettingsService.GetMimoApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    return null;
                }

                var request = new HttpRequestMessage(HttpMethod.Post, MiMoEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    response.Dispose();
                    return null;
                }

                System.IO.Stream networkStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                // 创建 PCM 音频描述符（24kHz、单声道、16bit），对应流式返回的 pcm16 格式
                AudioEncodingProperties audioProps = AudioEncodingProperties.CreatePcm(24000, 1, 16);
                MediaStreamSource source = new MediaStreamSource(new AudioStreamDescriptor(audioProps));

                // 会话对象通过事件委托与 source 强关联：后台解析 SSE 流，持续把 PCM 数据喂给播放器
                new StreamingAudioSession(source, networkStream, response);
                return source;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>语速数值映射为 MiMo 语速标签（1.0 为正常，不附加标签）。</summary>
        private static string GetSpeedTag(double speed)
        {
            if (speed < 0.9) { return "语速放慢"; }
            if (speed > 1.1) { return "语速加快"; }
            return "";
        }

        /// <summary>音调数值映射为 MiMo 音调标签（1.0 为正常，不附加标签）。</summary>
        private static string GetTuneTag(double tune)
        {
            if (tune < 0.9) { return "音调降低"; }
            if (tune > 1.1) { return "音调升高"; }
            return "";
        }

        /// <summary>
        /// 流式会话（生产者-消费者）：
        /// 后台任务解析 SSE 流中的 PCM16 数据块入队，MediaStreamSource.SampleRequested 取出并组装为音频样本。
        /// </summary>
        private sealed class StreamingAudioSession : IDisposable
        {
            // PCM16 24kHz 单声道 → 每秒字节数
            private const int BytesPerSecond = 24000 * 2;

            private readonly ConcurrentQueue<byte[]> chunks = new ConcurrentQueue<byte[]>();
            private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly System.IO.Stream networkStream;
            private readonly HttpResponseMessage response;
            private long timestampTicks;
            private volatile bool ended;

            public StreamingAudioSession(MediaStreamSource source, System.IO.Stream networkStream, HttpResponseMessage response)
            {
                this.networkStream = networkStream;
                this.response = response;
                source.Starting += OnStarting;
                source.SampleRequested += OnSampleRequested;
                source.Closed += OnClosed;

                // 兜底超时：网络半开连接时避免永久挂起
                cts.CancelAfter(TimeSpan.FromSeconds(120));

                _ = Task.Run(ReadLoopAsync);
            }

            private void OnStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
            {
                // Starting 必须给出实际起始位置，否则播放器会抛异常
                args.Request.SetActualStartPosition(TimeSpan.Zero);
            }

            private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
            {
                while (true)
                {
                    if (chunks.TryDequeue(out byte[] pcm))
                    {
                        TimeSpan duration = TimeSpan.FromTicks(
                            (long)((double)pcm.Length / BytesPerSecond * TimeSpan.TicksPerSecond));
                        args.Request.Sample = MediaStreamSample.CreateFromBuffer(
                            pcm.AsBuffer(), TimeSpan.FromTicks(timestampTicks));
                        args.Request.Sample.Duration = duration;
                        timestampTicks += duration.Ticks;
                        return;
                    }

                    if (ended)
                    {
                        // 无更多数据：null 表示流结束
                        args.Request.Sample = null;
                        return;
                    }

                    // 队列为空且未结束：阻塞等待新数据（播放器线程短暂等待，不阻塞 UI）
                    signal.Wait();
                }
            }

            private void OnClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
            {
                Dispose();
            }

            private async Task ReadLoopAsync()
            {
                try
                {
                    using (var reader = new System.IO.StreamReader(networkStream, Encoding.UTF8))
                    {
                        while (!cts.IsCancellationRequested)
                        {
                            // StreamReader.ReadLineAsync 不响应取消，用 WhenAny 竞争取消等待
                            Task<string> readTask = reader.ReadLineAsync();
                            Task cancelTask = Task.Delay(-1, cts.Token);
                            Task completed = await Task.WhenAny(readTask, cancelTask).ConfigureAwait(false);
                            if (completed == cancelTask) { break; }

                            string line = readTask.Result;
                            if (line == null) { break; } // 流结束
                            if (!line.StartsWith("data:", StringComparison.Ordinal)) { continue; }

                            string payload = line.Substring(5).Trim();
                            if (payload.Length == 0) { continue; }
                            if (payload == "[DONE]") { break; }

                            try
                            {
                                JObject evt = JObject.Parse(payload);
                                string audioBase64 = evt.SelectToken("choices[0].delta.audio.data")?.ToString();
                                if (string.IsNullOrEmpty(audioBase64)) { continue; }

                                byte[] pcm = Convert.FromBase64String(audioBase64);
                                chunks.Enqueue(pcm);
                                signal.Release();
                            }
                            catch (Exception)
                            {
                                // 跳过无法解析的 SSE 事件
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 网络/读取异常：按流结束处理，播放器播放已收到的内容后停止
                }
                finally
                {
                    ended = true;
                    TryRelease();
                    Dispose();
                }
            }

            private void TryRelease()
            {
                try { signal.Release(); } catch (SemaphoreFullException) { }
            }

            public void Dispose()
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                ended = true;
                TryRelease();
                networkStream?.Dispose();
                response?.Dispose();
            }
        }
    }
}
