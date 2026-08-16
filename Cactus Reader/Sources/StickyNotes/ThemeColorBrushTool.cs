using Cactus_Reader.Entities;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Cactus_Reader.Sources.StickyNotes
{
    /// <summary>
    /// 便签主题色查询工具。
    /// 颜色收敛为 C# 静态表（不再依赖 Application.Resources），
    /// SolidColorBrush 是 DependencyObject 必须在创建它的 UI 线程上使用，
    /// 因此每次调用都在调用线程新建 brush 实例，天然跨线程安全
    /// （主窗口线程 / 便签编辑辅助视图线程均可调用）。
    /// </summary>
    public class ThemeColorBrushTool
    {
        /// <summary>主题标识 → (标题色, 背景色, 悬停标题色, 悬停背景色)。</summary>
        private static readonly Dictionary<string, (Color Title, Color Background, Color TitleFocus, Color BackgroundFocus)> ColorTable =
            new Dictionary<string, (Color, Color, Color, Color)>
            {
                ["GingkoYellow"] = (Color.FromArgb(0xFF, 0xFF, 0xF2, 0xAB), Color.FromArgb(0xFF, 0xFF, 0xF7, 0xD1),
                                    Color.FromArgb(0xFF, 0xF0, 0xE7, 0xB1), Color.FromArgb(0xFF, 0xF0, 0xEB, 0xCE)),
                ["MintGreen"] = (Color.FromArgb(0xFF, 0xCB, 0xF1, 0xC4), Color.FromArgb(0xFF, 0xE4, 0xF9, 0xE0),
                                 Color.FromArgb(0xFF, 0xC9, 0xE6, 0xC4), Color.FromArgb(0xFF, 0xDC, 0xEC, 0xD9)),
                ["BubblePink"] = (Color.FromArgb(0xFF, 0xFF, 0xCC, 0xE5), Color.FromArgb(0xFF, 0xFF, 0xE4, 0xF1),
                                  Color.FromArgb(0xFF, 0xF0, 0xCA, 0xDD), Color.FromArgb(0xFF, 0xF0, 0xDC, 0xE6)),
                ["TaroPurple"] = (Color.FromArgb(0xFF, 0xE7, 0xCF, 0xFF), Color.FromArgb(0xFF, 0xF2, 0xE6, 0xFF),
                                  Color.FromArgb(0xFF, 0xDF, 0xCC, 0xF0), Color.FromArgb(0xFF, 0xE7, 0xDE, 0xF0)),
                ["SkyBlue"] = (Color.FromArgb(0xFF, 0xCD, 0xE9, 0xFF), Color.FromArgb(0xFF, 0xE2, 0xF1, 0xFF),
                               Color.FromArgb(0xFF, 0xCB, 0xE0, 0xF0), Color.FromArgb(0xFF, 0xDB, 0xE6, 0xF0)),
                ["StoneGray"] = (Color.FromArgb(0xFF, 0xE1, 0xDF, 0xDD), Color.FromArgb(0xFF, 0xF3, 0xF2, 0xF1),
                                 Color.FromArgb(0xFF, 0xDA, 0xD8, 0xD7), Color.FromArgb(0xFF, 0xE7, 0xE7, 0xE6)),
            };

        private static ThemeColorBrushTool instance;

        public static ThemeColorBrushTool Instance
        {
            get { return instance ?? (instance = new ThemeColorBrushTool()); }
        }

        /// <summary>按主题标识获取标题/背景画笔（在调用线程新建实例），isFocused 为 true 时返回悬停状态色。</summary>
        public ThemeColorBrush GetThemeColorBrush(string theme, bool isFocused)
        {
            (Color Title, Color Background, Color TitleFocus, Color BackgroundFocus) palette;
            if (!ColorTable.TryGetValue(theme, out palette))
            {
                // 未知主题回退银杏黄
                palette = ColorTable["GingkoYellow"];
            }

            Color titleColor = isFocused ? palette.TitleFocus : palette.Title;
            Color backgroundColor = isFocused ? palette.BackgroundFocus : palette.Background;

            return new ThemeColorBrush
            {
                TitleBrush = new SolidColorBrush(titleColor),
                BackgroundBrush = new SolidColorBrush(backgroundColor),
            };
        }
    }
}
