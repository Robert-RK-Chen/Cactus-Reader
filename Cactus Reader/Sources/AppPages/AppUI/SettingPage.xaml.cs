using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
        private readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;
        private MediaPlayer mediaPlayer;
        private bool suppressSyncToggle;

        public SettingPage()
        {
            InitializeComponent();

            // 补全缺失的默认设置项
            SettingsService.EnsureDefaultSettings();

            // Add a global Media Player Element
            mediaPlayer = new MediaPlayer();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 恢复跨设备同步开关状态（OnNavigatedTo 每次导航进入必触发，比 Loading 事件可靠）
            suppressSyncToggle = true;
            syncSwitch.IsOn = SettingsService.GetSyncEnabled();
            suppressSyncToggle = false;

            string UID = localSettings.Values["UID"].ToString();

            // TODO: Load User Information
            name.Text = localSettings.Values["name"].ToString();
            email.Text = localSettings.Values["email"].ToString();

            // TODO: Load App Settings
            previewText.FontSize = SettingsService.GetFontSize();
            speedSlider.Value = SettingsService.GetVoiceSpeed();
            tuneSlider.Value = SettingsService.GetVoiceTune();

            if (SettingsService.IsPrivateKeySet())
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

        /// <summary>
        /// 切换跨设备同步：关闭后仅维持本地内容（不执行任何上传/下载）；
        /// 再次开启时全量上传本地内容覆盖云端（replace_cloud）。
        /// </summary>
        private async void ToggleSync(object sender, RoutedEventArgs e)
        {
            if (suppressSyncToggle)
            {
                return;
            }
            await SettingsService.SetSyncEnabledAsync(syncSwitch.IsOn);
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
            AccountService.SignOut();
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
            picker.FileTypeFilter.Add(".jpeg");
            StorageFile imageFile = await picker.PickSingleFileAsync();

            if (imageFile != null)
            {
                // 本地留存
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
                await imageFile.CopyAsync(storageFolder, "ProfilePicture.PNG", NameCollisionOption.ReplaceExisting);

                // 从本地副本读取并显示头像（不依赖选择器的临时缓存文件，确保立即显示）
                StorageFile localFile = await storageFolder.GetFileAsync("ProfilePicture.PNG");
                BitmapImage image = new BitmapImage();
                using (IRandomAccessStream stream = await localFile.OpenReadAsync())
                {
                    await image.SetSourceAsync(stream);
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    userProfileImage.ProfilePicture = image;
                });

                // 向服务器上传用户头像（上传本地副本，避免临时文件失效）
                uploadTool.UploadProfileImg(localFile, UID, "/upload-profile-image");
            }
            Frame.Navigate(typeof(SettingPage));
        }

        private void LoadAppTheme(object sender, RoutedEventArgs e)
        {
            appThemeCombo.SelectedIndex = SettingsService.GetAppThemeIndex();
        }

        private void ChangeAppTheme(object sender, SelectionChangedEventArgs e)
        {
            SettingsService.ApplyAppTheme(appThemeCombo.SelectedIndex);
        }

        private void LoadAppFont(object sender, RoutedEventArgs e)
        {
            fontsCombo.SelectedValue = SettingsService.GetAppFont();
        }

        private void ChangeAppFont(object sender, SelectionChangedEventArgs e)
        {
            string font = fontsCombo.SelectedValue.ToString();
            SettingsService.SetAppFont(font);
            previewText.FontFamily = new FontFamily(font);
        }

        private void DeceaseFontSize(object sender, RoutedEventArgs e)
        {
            previewText.FontSize = SettingsService.ChangeFontSize(-1);
        }

        private void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            previewText.FontSize = SettingsService.ChangeFontSize(1);
        }

        private void LoadSpeechVoice(object sender, RoutedEventArgs e)
        {
            voiceCombo.SelectedIndex = SettingsService.GetVoiceIndex();
        }

        private void ChangeSpeechVoice(object sender, SelectionChangedEventArgs e)
        {
            SettingsService.SetSpeechVoice(voiceCombo.SelectedIndex);
        }

        private void ChangeSpeechSpeed(object sender, RangeBaseValueChangedEventArgs e)
        {
            SettingsService.SetSpeechSpeed(speedSlider.Value);
        }

        private void ChangeSpeechTune(object sender, RangeBaseValueChangedEventArgs e)
        {
            SettingsService.SetSpeechTune(tuneSlider.Value);
        }

        private async void PlaySpeechTextExample(object sender, RoutedEventArgs e)
        {
            // 语速与语调暂不可用
            string exampleText;
            if (SettingsService.GetVoiceLang().Equals("Chinese"))
            {
                exampleText = "你好，我是讲述人：" + voiceCombo.SelectedItem + ", 欢迎使用 Cactus Reader。";
            }
            else
            {
                exampleText = "Nice to meet you, this is " + voiceCombo.SelectedItem + ". Welcome to Cactus Reader.";
            }

            try
            {
                // 原子操作：合成语音到本地 wav 文件
                StorageFile audioFile = await SpeechService.SynthesizeToFileAsync(
                    exampleText, SettingsService.GetVoiceName());

                if (audioFile != null)
                {
                    mediaPlayer.Source = MediaSource.CreateFromStorageFile(audioFile);
                    mediaPlayer.Play();
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
            ContentDialog setPrivateKeyDialog = new()
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
                    // 原子操作：PBKDF2 加盐哈希存储 + 用密码包裹便签密钥上传服务端（换设备时可凭密码找回）
                    bool vaultSynced = await SettingsService.SetPrivateKeyAsync(password);
                    windowsHelloSwitch.IsEnabled = true;

                    // hide the button and show another button
                    setKeyButton.Visibility = Visibility.Collapsed;
                    closeKeyButton.Visibility = Visibility.Visible;

                    ContentDialog keyAlertDialog = new ContentDialog
                    {
                        Title = "请勿忘记便签本密码",
                        Content = vaultSynced
                            ? "忘记便签本的密码将导致即使你可以通过 Windows Hello 等方式访问你的便签本，你可能会永久性地失去对你便签本的管理权限。"
                            : "密码已设置（本机可用）。但密钥云同步失败，更换设备时将无法凭密码找回便签，请检查网络后重新设置。",
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
            PasswordBox passwordBox = new()
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
                // 原子操作：验证密码 → 删除服务端包裹密钥 → 清除本机设置 → 解锁全部便签
                bool vaultRemoved = await SettingsService.ClosePrivateKeyAsync(password);
                if (!vaultRemoved)
                {
                    // 密码错误或云端密钥删除失败
                    ContentDialog removeFailDialog = new ContentDialog
                    {
                        Title = "操作失败",
                        Content = "密码不正确，或云端密钥删除失败。本机仍可继续使用，但请检查密码后再试。",
                        CloseButtonText = "确定"
                    };
                    await removeFailDialog.ShowAsync();
                    break;
                }

                windowsHelloSwitch.IsOn = false;
                windowsHelloSwitch.IsEnabled = false;
                setKeyButton.Visibility = Visibility.Visible;
                closeKeyButton.Visibility = Visibility.Collapsed;
                break;
            }
        }

        private void LoadedWindowsHello(object sender, object args)
        {
            if (SettingsService.IsPrivateKeySet())
            {
                windowsHelloSwitch.IsEnabled = true;
                windowsHelloSwitch.IsOn = SettingsService.IsWindowsHelloSet();
            }
        }

        private async void OpenWindowsHello(object sender, RoutedEventArgs e)
        {
            // 当 Windows Hello 打开时判断用户是否设置过 Windows Hello 加密
            // 没有设置过则开始设置 Windows Hello
            if (!SettingsService.IsWindowsHelloSet())
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
                    SettingsService.SetWindowsHello(true);
                }
                else
                {
                    SettingsService.SetWindowsHello(false);
                    windowsHelloSwitch.IsOn = false;
                }
                windowsHelloSwitch.IsEnabled = true;
            }

            // 当用户关闭 Windows Hello 时，同时关闭密码
            if (windowsHelloSwitch.IsOn == false)
            {
                SettingsService.SetWindowsHello(false);
            }
        }
    }
}
