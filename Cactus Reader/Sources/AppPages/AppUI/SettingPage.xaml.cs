using Microsoft.CognitiveServices.Speech;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using System.IO;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Cactus_Reader.Sources.ToolKits;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private MediaPlayer mediaPlayer;
        readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;

        public SettingPage()
        {
            InitializeComponent();
            if (localSettings.Values["appTheme"] == null) { localSettings.Values["appTheme"] = "跟随系统主题"; }
            if (localSettings.Values["font"] == null) { localSettings.Values["font"] = "宋体"; }
            if (localSettings.Values["fontSize"] == null) { localSettings.Values["fontSize"] = 15; }
            if (localSettings.Values["lang"] == null) { localSettings.Values["lang"] = "中文（简体中文）"; }
            if (localSettings.Values["voice"] == null) { localSettings.Values["voice"] = "Azure TTS - 晓晓"; }
            if (localSettings.Values["speed"] == null) { localSettings.Values["speed"] = 1.0; }
            if (localSettings.Values["tune"] == null) { localSettings.Values["tune"] = 1.0; }
            mediaPlayer = new MediaPlayer();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string UID = localSettings.Values["UID"].ToString();

            // TODO: Load User Profile Image
            try
            {
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                BitmapImage image = new BitmapImage(new Uri(storageFolder.Path + "\\ProfilePicture.PNG"));
                userProfileImage.ProfilePicture = image;
            }
            catch (Exception) { }

            // TODO: Load User Information
            name.Text = localSettings.Values["Name"].ToString();
            email.Text = localSettings.Values["Email"].ToString();

            // TODO: Load App Settings
            appThemeCombo.SelectedValue = localSettings.Values["appTheme"].ToString();
            fontsCombo.SelectedValue = localSettings.Values["font"].ToString();
            previewText.FontSize = (int)localSettings.Values["fontSize"];
            langCombo.SelectedValue = localSettings.Values["lang"].ToString();
            voiceCombo.SelectedValue = localSettings.Values["voice"].ToString();
            speedSlider.Value = (double)localSettings.Values["speed"];
            tuneSlider.Value = (double)localSettings.Values["tune"];

            // TODO: 恢复后台传输列表
            uploadTool.RecoveryBackgroundTransfer();
        }

        private void HideUserImage(object sender, SizeChangedEventArgs e)
        {
            if (Window.Current.Bounds.Width <= 640)
            {
                userProfileImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                userProfileImage.Visibility = Visibility.Visible;
            }
        }

        private void SignOut(object sender, RoutedEventArgs e)
        {
            localSettings.Values["isLogin"] = false;
            MainPage.mainPage.mainContent.Navigate(typeof(StartPage), null, new DrillInNavigationTransitionInfo());
        }

        private void ChangeAppTheme(object sender, SelectionChangedEventArgs e)
        {
            string appTheme = appThemeCombo.SelectedValue.ToString();
            localSettings.Values["appTheme"] = appTheme;
            switch (appTheme)
            {
                case "使用浅色主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Light;
                        break;
                    }
                case "使用深色主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Dark;
                        break;
                    }
                case "跟随系统主题":
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Default;
                        break;
                    }
                default:
                    {
                        ((Frame)Window.Current.Content).RequestedTheme = ElementTheme.Default;
                        break;
                    }
            }
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            string currentFont = fontsCombo.SelectedValue.ToString();
            localSettings.Values["font"] = currentFont;
            previewText.FontFamily = new FontFamily(currentFont);
        }

        private void DeceaseFontSize(object sender, RoutedEventArgs e)
        {
            int currentFontSize = int.Parse(localSettings.Values["fontSize"].ToString());
            if (currentFontSize > 12)
            {
                currentFontSize--;
                localSettings.Values["fontSize"] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }

        private void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            int currentFontSize = int.Parse(localSettings.Values["fontSize"].ToString());
            if (currentFontSize < 30)
            {
                currentFontSize++;
                localSettings.Values["fontSize"] = currentFontSize;
                previewText.FontSize = currentFontSize;
            }
        }

        private void ChangeSpeechLang(object sender, SelectionChangedEventArgs e)
        {
            string speecLang = langCombo.SelectedValue.ToString();
            localSettings.Values["lang"] = langCombo.SelectedValue.ToString();
        }

        private void ChangeSpeechVoice(object sender, SelectionChangedEventArgs e)
        {
            string speecVoice = voiceCombo.SelectedValue.ToString();
            localSettings.Values["voice"] = speecVoice;
        }

        private void ChangeSpeechSpeed(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            double speechSpeed = speedSlider.Value;
            localSettings.Values["speed"] = speechSpeed;
        }

        private void ChangeSpeechTune(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            double speechTune = tuneSlider.Value;
            localSettings.Values["tune"] = speechTune;
        }

        private async void PlaySpeechTextExample(object sender, RoutedEventArgs e)
        {
            // 语速与语调暂不可用
            var config = SpeechConfig.FromSubscription("4c28aeca36ba4709a5c52a2ec64193e6", "eastasia");
            switch (localSettings.Values["voice"].ToString())
            {
                case "Azure TTS - 晓晓":
                    config.SpeechSynthesisVoiceName = "zh-CN-XiaoxiaoNeural"; break;
                case "Azure TTS - 云希":
                    config.SpeechSynthesisVoiceName = "zh-CN-YunxiNeural"; break;
                case "Azure TTS - 晓萱":
                    config.SpeechSynthesisVoiceName = "zh-CN-XiaoxuanNeural"; break;
                case "Azure TTS - 云杨":
                    config.SpeechSynthesisVoiceName = "zh-CN-YunyangNeural"; break;
                default:
                    config.SpeechSynthesisVoiceName = "zh-CN-XiaoxiaoNeural"; break;
            }

            try
            {
                using (var synthesizer = new SpeechSynthesizer(config, null))
                {
                    string exampleText = "欢迎使用 Cactus Reader。我是讲述人：" + voiceCombo.SelectedItem;
                    using (var result = await synthesizer.SpeakTextAsync(exampleText).ConfigureAwait(false))
                    {
                        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                        {
                            using (var audioStream = AudioDataStream.FromResult(result))
                            {
                                var filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "outputaudio.wav");
                                await audioStream.SaveToWaveFileAsync(filePath);
                                mediaPlayer.Source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(filePath));
                                mediaPlayer.Play();
                            }
                        }
                        else
                        {
                            new ToastContentBuilder().AddArgument("action", "viewConversation")
                                .AddArgument("conversationId", 9527)
                                .AddText("Cactus Reader 讲述人")
                                .AddText("未能生成语音。若要继续，请将设备连接到网络。")
                                .Show();
                        }
                    }
                }
            }
            catch (Exception)
            {
                new ToastContentBuilder().AddArgument("action", "viewConversation")
                    .AddArgument("conversationId", 9528)
                    .AddText("Cactus Reader 讲述人")
                    .AddText("我们出了点问题。若要使用语音服务，请稍后再试。")
                    .Show();
            }
        }

        private async void ChangeProfileImg(object sender, RoutedEventArgs e)
        {
            string UID = localSettings.Values["UID"].ToString();

            FileOpenPicker picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".PNG");
            StorageFile imageFile = await picker.PickSingleFileAsync();
            if (imageFile != null)
            {
                // 本地留存以及立即替换用户头像
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                await imageFile.CopyAsync(storageFolder, "ProfilePicture.PNG", NameCollisionOption.ReplaceExisting);

                // 优化：直接将选择的图片设置为头像
                BitmapImage image = new BitmapImage(new Uri(storageFolder.Path + "\\ProfilePicture.PNG"));
                userProfileImage.ProfilePicture = image;

                // 向服务器上传用户头像
                uploadTool.UploadProfileImg(imageFile, UID, "/upload-profile-image");
            }
        }
    }
}
