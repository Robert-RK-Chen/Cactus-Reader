using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.IO;
using System.Linq;
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
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly IFreeSql freeSql = IFreeSqlService.Instance;
        private readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;
        private readonly MD5EncryptTool md5EncryptTool = MD5EncryptTool.Instance;
        private readonly InformationVerify informationVerify = InformationVerify.Instance;
        private readonly EncryptStickyTool encryptStickyTool = EncryptStickyTool.Instance;
        private MediaPlayer mediaPlayer;

        public SettingPage()
        {
            InitializeComponent();

            // Initialize App Settings
            if (localSettings.Values["appThemeIndex"] == null)
            {
                localSettings.Values.Add("appThemeIndex", 2);
            }
            if (localSettings.Values["font"] == null)
            {
                localSettings.Values.Add("font", "宋体");
            }
            if (localSettings.Values["fontSize"] == null)
            {
                localSettings.Values.Add("fontSize", 15.0);
            }
            if (localSettings.Values["voiceIndex"] == null)
            {
                localSettings.Values.Add("voiceIndex", 0);
            }
            if (localSettings.Values["voiceName"] == null)
            {
                localSettings.Values.Add("voiceName", "zh-CN-XiaoxiaoNeural");
            }
            if (localSettings.Values["voiceLang"] == null)
            {
                localSettings.Values.Add("voiceLang", "Chinese");
            }
            if (localSettings.Values["speed"] == null)
            {
                localSettings.Values.Add("speed", 1.0);
            }
            if (localSettings.Values["tune"] == null)
            {
                localSettings.Values.Add("tune", 1.0);
            }
            if (localSettings.Values["alreadySetWindowsHello"] == null)
            {
                localSettings.Values.Add("alreadySetWindowsHello", false);
            }

            // Add a global Media Player Element
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
            previewText.FontSize = (double)localSettings.Values["fontSize"];
            speedSlider.Value = (double)localSettings.Values["speed"];
            tuneSlider.Value = (double)localSettings.Values["tune"];

            if (localSettings.Values.Keys.Contains("privateKey"))
            {
                setKeyButton.Visibility = Visibility.Collapsed;
                closeKeyButton.Visibility = Visibility.Visible;
            }
            else
            {
                setKeyButton.Visibility = Visibility.Visible;
                closeKeyButton.Visibility = Visibility.Collapsed;
            }

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
            fontsCombo.SelectedValue = localSettings.Values["font"];
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            localSettings.Values["font"] = fontsCombo.SelectedValue;
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
            // Show a password input UI
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
                // Get password and check it's correct
                string password = passwordBox.Password;
                if (password.Length >= 6)
                {
                    // Encrypt private key then allow user use Windows Hello
                    localSettings.Values.Add("privateKey", md5EncryptTool.GetUserEncryptedPassword(password));
                    windowsHelloSwitch.IsEnabled = true;

                    // hide the button and show another button
                    setKeyButton.Visibility = Visibility.Collapsed;
                    closeKeyButton.Visibility = Visibility.Visible;

                    ContentDialog keyAlertDialog = new ContentDialog
                    {
                        Title = "请勿忘记便签本密码",
                        Content = "忘记便签本的密码将导致即使你可以通过 Windows Hello 等方式访问你的便签本，你可能会永久性地失去对你便签本的管理权限。",
                        CloseButtonText = "取消",
                        PrimaryButtonText = "确定",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    await keyAlertDialog.ShowAsync();
                    break;
                }
                else
                {
                    result = await setPrivateKeyDialog.ShowAsync();
                }
            }
        }

        private async void ClosePrivateKey(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = new PasswordBox
            {
                Width = 360,
                PlaceholderText = "请输入你用于锁定便签本的密码",
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = "我们需要验证你的密码，然后为你关闭密码，这将解锁你的所有便签。",
            };
            ContentDialog setPrivateKeyDialog = new ContentDialog
            {
                Title = "关闭便签本密码",
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await setPrivateKeyDialog.ShowAsync();

            while (result == ContentDialogResult.Primary)
            {
                string password = passwordBox.Password;
                if (informationVerify.CheckPassword(password))
                {
                    localSettings.Values.Remove("privateKey");
                    localSettings.Values["alreadySetWindowsHello"] = false;

                    windowsHelloSwitch.IsOn = false;
                    windowsHelloSwitch.IsEnabled = false;
                    setKeyButton.Visibility = Visibility.Visible;
                    closeKeyButton.Visibility = Visibility.Collapsed;

                    await Task.Factory.StartNew(() => encryptStickyTool.UnlockAllSticky());
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
            if (localSettings.Values.Keys.Contains("privateKey"))
            {
                windowsHelloSwitch.IsEnabled = true;
                windowsHelloSwitch.IsOn = (bool)localSettings.Values["alreadySetWindowsHello"];
            }
        }

        private async void OpenWindowsHello(object sender, RoutedEventArgs e)
        {
            // 当 Windows Hello 打开时判断用户是否设置过 Windows Hello 加密
            // 没有设置过则开始设置 Windows Hello
            if ((bool)localSettings.Values["alreadySetWindowsHello"] == false)
            {
                string UID = localSettings.Values["UID"].ToString();
                string name = localSettings.Values["name"].ToString();

                windowsHelloSwitch.IsEnabled = false;
                bool isSuccessful = await MicrosoftPassportHelper.CreatePassportKeyAsync(UID, name);
                if (isSuccessful)
                {
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "Windows Hello 验证成功",
                        Content = "你现在可以使用 Windows Hello 来查看和管理锁定的便签本。",
                        PrimaryButtonText = "确定",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    await contentDialog.ShowAsync();
                    localSettings.Values["alreadySetWindowsHello"] = true;
                }
                else
                {
                    localSettings.Values["alreadySetWindowsHello"] = false;
                    windowsHelloSwitch.IsOn = false;
                }
                windowsHelloSwitch.IsEnabled = true;
            }

            // 当用户关闭 Windows Hello 时，同时关闭密码
            if (windowsHelloSwitch.IsOn == false)
            {
                localSettings.Values["alreadySetWindowsHello"] = false;
            }
        }
    }
}
