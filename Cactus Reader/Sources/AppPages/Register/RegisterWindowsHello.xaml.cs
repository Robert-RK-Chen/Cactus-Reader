using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Register
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class RegisterWindowsHello : Page
    {
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        private readonly MicrosoftPassportHelper microsoftPassportHelper = MicrosoftPassportHelper.Instance;

        User currentUser = null;

        public RegisterWindowsHello()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
        }

        private async void SkipSetting(object sender, RoutedEventArgs e)
        {
            ContentDialog signInDialog = new ContentDialog
            {
                Title = "欢迎来到 Cactus Reader",
                Content = "你的 Cactus 帐户已准备就绪！请牢记你的帐号与密码。下次登录时，你可以使用 Cactus 帐户与你的密码组合进行登录。点击确定按钮后，我们将自动为你登录。",
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary
            };
            ContentDialogResult result = await signInDialog.ShowAsync();

            if (ContentDialogResult.Primary == result)
            {
                syncTool.LoadCurrentUser(currentUser);
                StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        private async void ContinueRegister(object sender, RoutedEventArgs e)
        {
            ControllerVisibility.ShowProgressBar(statusBar);
            bool isSuccessful = await microsoftPassportHelper.CreatePassportKeyAsync(currentUser.UID, currentUser.Name);
            ControllerVisibility.HideProgressBar(statusBar);

            if (isSuccessful)
            {
                ContentDialog signInDialog = new ContentDialog
                {
                    Title = "欢迎来到 Cactus Reader",
                    Content = "你的 Cactus 帐户已准备就绪！请牢记你的帐号与密码。下次登录时，你可以使用 Cactus 帐户与你的密码组合进行登录。点击确定按钮后，我们将自动为你登录。",
                    PrimaryButtonText = "确定",
                    DefaultButton = ContentDialogButton.Primary
                };
                ContentDialogResult result = await signInDialog.ShowAsync();

                if (ContentDialogResult.Primary == result)
                {
                    syncTool.LoadCurrentUser(currentUser);
                    StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                }
            }
        }
    }
}
