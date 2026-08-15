using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cactus_Reader.Sources.ToolKits.ViewModels
{
    /// <summary>
    /// 讲述人（TTS）设置视图模型：集中管理音色 / 风格选项与当前选中项。
    /// 设置页与阅读页通过 x:Bind 复用同一实例，选中变化时统一写回 SettingsService，
    /// 页面不再各自维护 Loaded / SelectionChanged 事件与内联下拉项。
    /// </summary>
    public class SpeechSettingsViewModel : INotifyPropertyChanged
    {
        /// <summary>全局共享实例（设置页与阅读页共用，保证两处选中态一致）。</summary>
        public static SpeechSettingsViewModel Instance { get; } = new SpeechSettingsViewModel();

        private int selectedVoiceIndex;
        private int selectedStyleIndex;

        public SpeechSettingsViewModel()
        {
            // 确保 voiceIndex / styleIndex / voiceName 等存在默认值，避免读取抛异常
            SettingsService.EnsureDefaultSettings();

            Voices = new ObservableCollection<VoiceOption>
            {
                new VoiceOption("MiMo TTS - 冰糖", "冰糖", "Chinese"),
                new VoiceOption("MiMo TTS - 茉莉", "茉莉", "Chinese"),
                new VoiceOption("MiMo TTS - 苏打", "苏打", "Chinese"),
                new VoiceOption("MiMo TTS - 白桦", "白桦", "Chinese"),
                new VoiceOption("MiMo TTS - Mia", "Mia", "English"),
                new VoiceOption("MiMo TTS - Chloe", "Chloe", "English"),
                new VoiceOption("MiMo TTS - Milo", "Milo", "English"),
                new VoiceOption("MiMo TTS - Dean", "Dean", "English"),
            };

            Styles = new ObservableCollection<string>
            {
                "默认（不指定风格）",
                "温柔", "高冷", "活泼", "严肃", "慵懒", "俏皮", "深沉", "干练", "凌厉",
                "开心", "悲伤", "平静", "磁性", "清亮", "甜美", "沙哑",
                "御姐音", "正太音", "大叔音",
            };

            selectedVoiceIndex = SettingsService.GetVoiceIndex();
            selectedStyleIndex = SettingsService.GetStyleIndex();
        }

        /// <summary>可选音色列表。</summary>
        public ObservableCollection<VoiceOption> Voices { get; }

        /// <summary>可选风格列表（索引 0 为"默认（不指定风格）"）。</summary>
        public ObservableCollection<string> Styles { get; }

        /// <summary>当前选中音色索引；变化时同步 voiceName / voiceLang 到设置。</summary>
        public int SelectedVoiceIndex
        {
            get => selectedVoiceIndex;
            set
            {
                if (selectedVoiceIndex == value) { return; }
                selectedVoiceIndex = value;
                SettingsService.SetSpeechVoice(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedVoice));
            }
        }

        /// <summary>当前选中风格索引；变化时同步 styleName 到设置。</summary>
        public int SelectedStyleIndex
        {
            get => selectedStyleIndex;
            set
            {
                if (selectedStyleIndex == value) { return; }
                selectedStyleIndex = value;
                SettingsService.SetSpeechStyle(value);
                OnPropertyChanged();
            }
        }

        /// <summary>当前选中音色项（用于试听文案等展示；索引越界时回退到首项）。</summary>
        public VoiceOption SelectedVoice
        {
            get
            {
                int index = selectedVoiceIndex;
                if (index < 0 || index >= Voices.Count) { index = 0; }
                return Voices[index];
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>讲述人音色选项：显示名 + 合成接口使用的音色名 + 语言。</summary>
    public class VoiceOption
    {
        public VoiceOption(string displayName, string voiceName, string lang)
        {
            DisplayName = displayName;
            VoiceName = voiceName;
            Lang = lang;
        }

        public string DisplayName { get; }
        public string VoiceName { get; }
        public string Lang { get; }
    }
}
