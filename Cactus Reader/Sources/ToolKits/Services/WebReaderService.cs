using Sgml;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace Cactus_Reader.Sources.ToolKits
{
    /// <summary>
    /// 网页阅读原子操作：下载网页 → Sgml 转 XML → 提取沉浸式正文（title/h1-h3/p）。
    /// 由 LibraryPage 调用，页面不再持有 WebClient / SgmlReader 细节。
    /// </summary>
    public static class WebReaderService
    {
        /// <summary>抓取网页并提取沉浸式阅读文本；失败返回空字符串。</summary>
        public static string FetchWebPage(string url)
        {
            string webContent = Download(url);
            if (webContent.Length == 0)
            {
                return "";
            }

            try
            {
                XmlDocument document = new XmlDocument();
                using (StringReader strReader = new StringReader(SgmlTranslate(webContent)))
                {
                    document.Load(strReader);
                }
                return ExtractImmersiveText(document);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Download(string url)
        {
            try
            {
                using (WebClient webClient = new WebClient { Encoding = Encoding.UTF8 })
                {
                    return webClient.DownloadString(url);
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>提取五类节点：title、h1、h2、h3、p，拼接为段落文本。</summary>
        private static string ExtractImmersiveText(XmlDocument document)
        {
            string contentText = string.Empty;
            contentText += AppendNodes(document.GetElementsByTagName("title"));
            contentText += AppendNodes(document.GetElementsByTagName("h1"));
            contentText += AppendNodes(document.GetElementsByTagName("h2"));
            contentText += AppendNodes(document.GetElementsByTagName("h3"));
            contentText += AppendNodes(document.GetElementsByTagName("p"));
            return contentText;
        }

        private static string AppendNodes(XmlNodeList nodes)
        {
            string builder = string.Empty;
            foreach (XmlElement element in nodes)
            {
                string text = element.InnerText.TrimStart().TrimEnd();
                if (text.Length > 0)
                {
                    builder += text + "\n\n";
                }
            }
            return builder;
        }

        private static string SgmlTranslate(string input)
        {
            using (var reader = new SgmlReader
            {
                DocType = "HTML",
                WhitespaceHandling = WhitespaceHandling.None,
                CaseFolding = CaseFolding.ToLower,
                InputStream = new StringReader(input)
            })
            {
                using (var output = new StringWriter())
                {
                    using (var writer = new XmlTextWriter(output) { Formatting = Formatting.Indented })
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Whitespace
                              && reader.NodeType != XmlNodeType.Comment)
                            {
                                writer.WriteNode(reader, true);
                            }
                        }
                        writer.Close();
                        return output.ToString();
                    }
                }
            }
        }
    }
}
