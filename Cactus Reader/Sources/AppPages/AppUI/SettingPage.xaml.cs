using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;
        private MediaPlayer mediaPlayer;

        public SettingPage()
        {
            InitializeComponent();

            if (localSettings.Values["appThemeIndex"] == null) { 
                localSettings.Values["appThemeIndex"] = 2; }
            if (localSettings.Values["fontIndex"] == null) { 
                localSettings.Values["fontIndex"] = 0; }
            if (localSettings.Values["fontSize"] == null) { 
                localSettings.Values["fontSize"] = 15; }
            if (localSettings.Values["voiceIndex"] == null) { 
                localSettings.Values["voiceIndex"] = 0; }
            if (localSettings.Values["voiceName"] == null) {
                localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural"; }
            if (localSettings.Values["voiceLang"] == null) {
                localSettings.Values["voiceLang"] = "Chinese"; }
            if (localSettings.Values["speed"] == null) { 
                localSettings.Values["speed"] = 1.0; }
            if (localSettings.Values["tune"] == null) { 
                localSettings.Values["tune"] = 1.0; }
            if (localSettings.Values["useWindowsHello"] == null) { 
                localSettings.Values["useWindowsHello"] = false; }

            mediaPlayer = new MediaPlayer();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            string UID = localSettings.Values["UID"].ToString();

            // TODO: Load User Information
            name.Text = localSettings.Values["name"].ToString();
            email.Text = localSettings.Values["email"].ToString();

            // TODO: Load App Settings
            previewText.FontSize = (int)localSettings.Values["fontSize"];
            speedSlider.Value = (double)localSettings.Values["speed"];
            tuneSlider.Value = (double)localSettings.Values["tune"];

            // TODO: Load User Profile Image
            try
            {
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                BitmapImage image = new BitmapImage(new Uri(storageFolder.Path + "\\ProfilePicture.PNG"));
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    userProfileImage.ProfilePicture = image;
                });
            }
            catch (Exception)
            {
                userProfileImage.DisplayName = localSettings.Values["name"].ToString();
            }

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

        private async void ChangeProfileImg(object sender, RoutedEventArgs e)
        {
            string UID = localSettings.Values["UID"].ToString();

            FileOpenPicker picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpge");
            StorageFile imageFile = await picker.PickSingleFileAsync();

            if (imageFile != null)
            {
                BitmapImage image = new BitmapImage(new Uri(imageFile.Path));

                // 本地留存
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                await imageFile.CopyAsync(storageFolder, "ProfilePicture.PNG", NameCollisionOption.ReplaceExisting);

                // 直接将选择的图片设置为头像
                await userProfileImage.ProfilePicture.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    userProfileImage.ProfilePicture = image;
                });

                System.Diagnostics.Debug.WriteLine(userProfileImage.ProfilePicture.ToString());

                // 向服务器上传用户头像
                uploadTool.UploadProfileImg(imageFile, UID, "/upload-profile-image");
            }
            Frame.Navigate(typeof(SettingPage));
        }
        
        private void LoadAppTheme(object sender, RoutedEventArgs e)
        {
            appThemeCombo.SelectedIndex = (int)localSettings.Values["appThemeIndex"];
        }
        
        private void ChangeAppTheme(object sender, SelectionChangedEventArgs e)
        {
            int appThemeIndex = appThemeCombo.SelectedIndex;
            localSettings.Values["appThemeIndex"] = appThemeIndex;
            switch (appThemeIndex)
            {
                case 0:
                    (Window.Current.Content as Frame).RequestedTheme = ElementTheme.Light;
                    break;
                case 1:
                    (Window.Current.Content as Frame).RequestedTheme = ElementTheme.Dark;
                    break;
                case 2:
                    (Window.Current.Content as Frame).RequestedTheme = ElementTheme.Default;
                    break;
                default:
                    (Window.Current.Content as Frame).RequestedTheme = ElementTheme.Default;
                    break;
            }
        }

        private void LoadAppFont(object sender, RoutedEventArgs e)
        {
            fontsCombo.SelectedIndex = (int)localSettings.Values["fontIndex"];
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            int currentFontIndex = fontsCombo.SelectedIndex;
            localSettings.Values["fontIndex"] = currentFontIndex;
            previewText.FontFamily = new FontFamily(fontsCombo.SelectedValue.ToString());
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

        private void LoadSpeechVoice(object sender, RoutedEventArgs e)
        {
            voiceCombo.SelectedIndex = (int)localSettings.Values["voiceIndex"];
        }
        
        private void ChangeSpeechVoice(object sender, SelectionChangedEventArgs e)
        {
            localSettings.Values["voiceIndex"] = voiceCombo.SelectedIndex;
            switch (voiceCombo.SelectedIndex)
            {
                case 0:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural";
                    localSettings.Values["voiceLang"] = "Chinese";
                    break;
                case 1:
                    localSettings.Values["voiceName"] = "zh-CN-YunxiNeural";
                    localSettings.Values["voiceLang"] = "Chinese";
                    break;
                case 2:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxuanNeural";
                    localSettings.Values["voiceLang"] = "Chinese";
                    break;
                case 3:
                    localSettings.Values["voiceName"] = "zh-CN-YunyangNeural";
                    localSettings.Values["voiceLang"] = "Chinese";
                    break;
                case 4:
                    localSettings.Values["voiceName"] = "en-US-AshleyNeural";
                    localSettings.Values["voiceLang"] = "English";
                    break;
                case 5:
                    localSettings.Values["voiceName"] = "en-US-JennyNeural";
                    localSettings.Values["voiceLang"] = "English";
                    break;
                case 6:
                    localSettings.Values["voiceName"] = "en-US-BrandonNeural";
                    localSettings.Values["voiceLang"] = "English";
                    break;
                case 7:
                    localSettings.Values["voiceName"] = "en-US-ChristopherNeural";
                    localSettings.Values["voiceLang"] = "English";
                    break;
                default:
                    localSettings.Values["voiceName"] = "zh-CN-XiaoxiaoNeural";
                    localSettings.Values["voiceLang"] = "Chinese";
                    break;
            }
        }

        private void ChangeSpeechSpeed(object sender, RangeBaseValueChangedEventArgs e)
        {
            double speechSpeed = speedSlider.Value;
            localSettings.Values["speed"] = speechSpeed;
        }

        private void ChangeSpeechTune(object sender, RangeBaseValueChangedEventArgs e)
        {
            double voiceName = tuneSlider.Value;
            localSettings.Values["tune"] = voiceName;
        }

        private async void PlaySpeechTextExample(object sender, RoutedEventArgs e)
        {
            // 语速与语调暂不可用
            var config = SpeechConfig.FromSubscription("4c28aeca36ba4709a5c52a2ec64193e6", "eastasia");
            config.SpeechSynthesisVoiceName = localSettings.Values["voiceName"].ToString();
            string exampleText;
            if (localSettings.Values["voiceLang"].ToString().Equals("Chinese"))
            {
                exampleText = "你好，我是讲述人：" + voiceCombo.SelectedItem + ", 欢迎使用 Cactus Reader。";
            }
            else
            {
                exampleText = "Nice to meet you, this is " + voiceCombo.SelectedItem + ". Welcome to Cactus Reader.";
            }
            
            try
            {
                using (var synthesizer = new SpeechSynthesizer(config, null))
                {
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

        private async void SetPrivateKey(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = new PasswordBox
            {
                Width = 360,
                PlaceholderText = "密码长度至少为 6 位",
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = "需要输入个人密码才能查看便签本中的内容。",
            };

            ContentDialog setPrivateKeyDialog = new ContentDialog
            {
                Title = "设置个人密码",
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await setPrivateKeyDialog.ShowAsync();
            
            while (result == ContentDialogResult.Primary)
            {
                string password = passwordBox.Password;
                if(password.Length >= 6)
                {
                    localSettings.Values.Add("privateKey", passwordBox.Password);
                    Privatekey privatekey = new Privatekey
                    {
                        UID = localSettings.Values["UID"].ToString(),
                        Key = passwordBox.Password
                    };
                    await Task.Factory.StartNew(() => freeSql.Insert(privatekey).ExecuteAffrows());
                    break;
                }
                else
                {
                    result = await setPrivateKeyDialog.ShowAsync();
                }
            }
        }

        private void LoadedWindowsHello(object sender, object args)
        {
            windowsHelloSwitch.IsOn = (bool)localSettings.Values["useWindowsHello"];
        }
        
        private async void OpenWindowsHello(object sender, RoutedEventArgs e)
        {            
            string UID = localSettings.Values["UID"].ToString();
            string name = localSettings.Values["name"].ToString();

            localSettings.Values["useWindowsHello"] = windowsHelloSwitch.IsOn;
            if(windowsHelloSwitch.IsOn)
            {
                windowsHelloSwitch.IsEnabled = false;
                bool isSuccessful = await MicrosoftPassportHelper.CreatePassportKeyAsync(UID, name);
                if (isSuccessful)
                {
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "Windows Hello 验证成功",
                        Content = "你现在可以使用 Windows Hello 来查看和管理锁定的便签。",
                        PrimaryButtonText = "确定",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    await contentDialog.ShowAsync();
                    windowsHelloSwitch.IsEnabled = true;
                }
            }
        }
    }
}
