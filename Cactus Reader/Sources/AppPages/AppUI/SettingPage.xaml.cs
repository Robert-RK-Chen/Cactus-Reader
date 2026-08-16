using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.ToolKits.ViewModels;
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

namespace Cactus_Reader.Sources.AppPages.AppUI
{
    public sealed partial class SettingPage : Page
    {
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ProfileUploadTool uploadTool = ProfileUploadTool.Instance;
        private MediaPlayer mediaPlayer;
        private bool suppressSyncToggle;

        /// <summary>讲述人设置视图模型（音色 / 风格，供 x:Bind 使用）。</summary>
        public SpeechSettingsViewModel SpeechSettings { get; } = SpeechSettingsViewModel.Instance;

        public SettingPage()
        {
            InitializeComponent();

            // 补全缺失的默认设置项
            SettingsService.EnsureDefaultSettings();

            // 全局播放器：供试听朗读使用
            mediaPlayer = new MediaPlayer();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 恢复跨设备同步开关状态（OnNavigatedTo 每次导航进入必触发，比 Loading 事件可靠）
            suppressSyncToggle = true;
            syncSwitch.IsOn = SettingsService.GetSyncEnabled();
            suppressSyncToggle = false;

            LoadUserInfo();
            LoadAppSettings();
            UpdatePrivateKeyButtons();

            // 加载本地头像并显示（缺失时回退为用户名首字）
            await LoadAvatarAsync();

            // 恢复上次会话未完成的后台上传任务（受跨设备同步开关控制）
            uploadTool.RecoveryBackgroundTransfer();
        }

        /// <summary>回填用户信息（姓名/邮箱）。</summary>
        private void LoadUserInfo()
        {
            name.Text = localSettings.Values["name"].ToString();
            email.Text = localSettings.Values["email"].ToString();
        }

        /// <summary>回填应用设置：预览字号/语速/音调/MiMo Key。</summary>
        private void LoadAppSettings()
        {
            previewText.FontSize = SettingsService.GetFontSize();
            speedSlider.Value = SettingsService.GetVoiceSpeed();
            tuneSlider.Value = SettingsService.GetVoiceTune();

            // 回填已保存的 MiMo API Key（密码框不回显明文，仅显示占位点数）
            mimoApiKeyBox.Password = SettingsService.GetMimoApiKey() ?? string.Empty;
        }

        /// <summary>按私钥是否已设置切换按钮显隐。</summary>
        private void UpdatePrivateKeyButtons()
        {
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
        }

        /// <summary>加载本地头像并显示（缺失时回退为显示用户名）。</summary>
        private async Task LoadAvatarAsync()
        {
            string UID = localSettings.Values["UID"].ToString();
            try
            {
                // TryGetItemAsync 不抛 FileNotFoundException：卸载重装后用户目录尚不存在
                StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.TryGetItemAsync(UID) as StorageFolder;
                if (storageFolder == null)
                {
                    userProfileImage.DisplayName = localSettings.Values["name"].ToString();
                    return;
                }
                StorageFile avatarFile = await storageFolder.TryGetItemAsync("ProfilePicture.PNG") as StorageFile;
                if (avatarFile == null)
                {
                    userProfileImage.DisplayName = localSettings.Values["name"].ToString();
                    return;
                }
                BitmapImage image = new BitmapImage(new Uri(avatarFile.Path));
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    userProfileImage.ProfilePicture = image;
                });
            }
            catch (Exception)
            {
                userProfileImage.DisplayName = localSettings.Values["name"].ToString();
            }
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
            StorageFile imageFile = await PickImageAsync();
            if (imageFile == null)
            {
                return;
            }

            // 本地留存
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(UID);
            await imageFile.CopyAsync(storageFolder, "ProfilePicture.PNG", NameCollisionOption.ReplaceExisting);

            // 从本地副本读取并显示头像（不依赖选择器的临时缓存文件，确保立即显示）
            StorageFile localFile = await storageFolder.GetFileAsync("ProfilePicture.PNG");
            await DisplayProfileImageAsync(localFile);

            // 向服务器上传用户头像（上传本地副本，避免临时文件失效）
            uploadTool.UploadProfileImg(localFile, UID, "/upload-profile-image");

            Frame.Navigate(typeof(SettingPage));
        }

        /// <summary>弹出文件选择器挑选头像图片（bmp/png/jpg/jpeg），取消时返回 null。</summary>
        private async Task<StorageFile> PickImageAsync()
        {
            FileOpenPicker picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            return await picker.PickSingleFileAsync();
        }

        /// <summary>从本地文件读取图片并显示到头像控件。</summary>
        private async Task DisplayProfileImageAsync(StorageFile localFile)
        {
            BitmapImage image = new BitmapImage();
            using (IRandomAccessStream stream = await localFile.OpenReadAsync())
            {
                await image.SetSourceAsync(stream);
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                userProfileImage.ProfilePicture = image;
            });
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

        private void ChangeSpeechSpeed(object sender, RangeBaseValueChangedEventArgs e)
        {
            SettingsService.SetSpeechSpeed(speedSlider.Value);
        }

        private void ChangeSpeechTune(object sender, RangeBaseValueChangedEventArgs e)
        {
            SettingsService.SetSpeechTune(tuneSlider.Value);
        }

        /// <summary>保存 MiMo API Key 到 Windows 凭据保险箱。</summary>
        private void SaveMimoApiKey(object sender, RoutedEventArgs e)
        {
            SettingsService.SetMimoApiKey(mimoApiKeyBox.Password);
            new ToastContentBuilder().AddArgument("action", "viewConversation")
                .AddArgument("conversationId", 9528)
                .AddText("Cactus Reader 设置")
                .AddText("MiMo API Key 已保存。")
                .Show();
        }

        private async void PlaySpeechTextExample(object sender, RoutedEventArgs e)
        {
            string exampleText;
            string voiceDisplay = SpeechSettings.SelectedVoice?.DisplayName ?? SettingsService.GetVoiceName();
            if (SettingsService.GetVoiceLang().Equals("Chinese"))
            {
                exampleText = "你好，我是讲述人：" + voiceDisplay + ", 欢迎使用 Cactus Reader。";
            }
            else
            {
                exampleText = "Nice to meet you, this is " + voiceDisplay + ". Welcome to Cactus Reader.";
            }

            try
            {
                // 流式合成：返回后立即开始播放，边合成边出声，无需等待整段生成
                MediaStreamSource source = await SpeechService.CreateStreamingSourceAsync(
                    exampleText, SettingsService.GetVoiceName(), SettingsService.GetStyleName(),
                    SettingsService.GetVoiceSpeed(), SettingsService.GetVoiceTune());

                if (source != null)
                {
                    mediaPlayer.Source = MediaSource.CreateFromMediaStreamSource(source);
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

        /// <summary>构建密码输入对话框（标题/提示/占位符可定制）。</summary>
        private static ContentDialog BuildPasswordDialog(string title, string header, string placeholder)
        {
            PasswordBox passwordBox = new PasswordBox
            {
                Width = 360,
                PlaceholderText = placeholder,
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalContentAlignment = VerticalAlignment.Center,
                Header = header,
            };
            return new ContentDialog
            {
                Title = title,
                Content = passwordBox,
                CloseButtonText = "取消",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
        }

        /// <summary>私钥设置成功后的 UI 反馈：启用 Windows Hello、切换按钮显隐、提示云同步结果。</summary>
        private async Task OnPrivateKeySetAsync(bool vaultSynced)
        {
            windowsHelloSwitch.IsEnabled = true;

            // 切换按钮显隐
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
        }

        private async void SetPrivateKey(object sender, RoutedEventArgs e)
        {
            ContentDialog setPrivateKeyDialog = BuildPasswordDialog(
                "设置个人密码", "需要输入个人密码才能查看便签本中的内容。", "密码长度至少为 6 位");
            ContentDialogResult result = await setPrivateKeyDialog.ShowAsync();

            while (result == ContentDialogResult.Primary)
            {
                // 校验密码长度并设置
                string password = (setPrivateKeyDialog.Content as PasswordBox).Password;
                if (password.Length >= 6)
                {
                    // 原子操作：PBKDF2 加盐哈希存储 + 用密码包裹便签密钥上传服务端（换设备时可凭密码找回）
                    bool vaultSynced = await SettingsService.SetPrivateKeyAsync(password);
                    await OnPrivateKeySetAsync(vaultSynced);
                    break;
                }
                else
                {
                    result = await setPrivateKeyDialog.ShowAsync();
                }
            }
        }

        /// <summary>关闭私钥成功后的 UI 反馈：禁用 Windows Hello、切换按钮显隐。</summary>
        private void OnPrivateKeyClosed()
        {
            windowsHelloSwitch.IsOn = false;
            windowsHelloSwitch.IsEnabled = false;
            setKeyButton.Visibility = Visibility.Visible;
            closeKeyButton.Visibility = Visibility.Collapsed;
        }

        private async void ClosePrivateKey(object sender, RoutedEventArgs e)
        {
            ContentDialog setPrivateKeyDialog = BuildPasswordDialog(
                "关闭便签本密码", "我们需要验证你的密码，然后为你关闭密码，这将解锁你的所有便签。", "请输入你用于锁定便签本的密码");
            ContentDialogResult result = await setPrivateKeyDialog.ShowAsync();

            while (result == ContentDialogResult.Primary)
            {
                string password = (setPrivateKeyDialog.Content as PasswordBox).Password;
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

                OnPrivateKeyClosed();
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
            // 未设置过 Windows Hello 时引导用户设置
            if (!SettingsService.IsWindowsHelloSet())
            {
                await SetupWindowsHelloAsync();
            }

            // 当用户关闭 Windows Hello 时，同时关闭密码
            if (windowsHelloSwitch.IsOn == false)
            {
                SettingsService.SetWindowsHello(false);
            }
        }

        /// <summary>创建 Windows Hello 密钥并提示结果，成功则写入设置。</summary>
        private async Task SetupWindowsHelloAsync()
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
    }
}
