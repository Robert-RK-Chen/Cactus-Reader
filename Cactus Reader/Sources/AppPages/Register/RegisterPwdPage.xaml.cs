﻿using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
using Cactus_Reader.Sources.WindowsHello;
using System;
using System.Threading.Tasks;
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
    public sealed partial class RegisterPwdPage : Page
    {
        private readonly IFreeSql freeSql = IFreeSqlService.Instance;
        private readonly ProfileSyncTool syncTool = ProfileSyncTool.Instance;
        private readonly HashDirectory hashDirectory = HashDirectory.Instance;
        private readonly InformationVerify informationVerify = InformationVerify.Instance;

        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.Email;
        }

        public RegisterPwdPage()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private async void LogonFinish(object sender, RoutedEventArgs e)
        {
            string password = passwordInput.Password;
            string checkPwd = passwordCheck.Password;

            try
            {
                if (password.Length == 0 && checkPwd.Length == 0)
                {
                    alertMsg.Text = "若要继续，请为你的帐户创建一个密码。";
                }
                else if (informationVerify.IsPassword(password) && string.Equals(password, checkPwd))
                {
                    currentUser.Password = hashDirectory.GetEncryptedPassword(password);
                    currentUser.UID = Guid.NewGuid().ToString("D").ToUpper();
                    currentUser.RegistDate = DateTime.Now;
                    currentUser.Mobile = string.Empty;

                    ControllerVisibility.ShowProgressBar(statusBar);
                    await Task.Factory.StartNew(() => freeSql.Insert(currentUser).ExecuteAffrows());
                    bool isTPMEnabled = await MicrosoftPassportHelper.MicrosoftPassportAvailableCheckAsync();

                    if (isTPMEnabled)
                    {
                        contentFrame.Navigate(typeof(RegisterWindowsHello), currentUser, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                    }
                    else
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
                else
                {
                    alertMsg.Text = "无效的密码，或两次输入的密码不相同。";
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
            }

            ControllerVisibility.HideProgressBar(statusBar);
            alertMsg.Visibility = Visibility.Visible;
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}
