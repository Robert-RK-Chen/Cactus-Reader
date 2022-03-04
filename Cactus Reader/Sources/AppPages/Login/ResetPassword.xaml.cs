using Cactus_Reader.Entities;
using Cactus_Reader.Sources.AppPages.Register;
using Cactus_Reader.Sources.ToolKits;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Cactus_Reader.Sources.AppPages.Login
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class ResetPassword : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.Email;
        }

        public ResetPassword()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterUserInfoPage), currentUser, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private async void ResetFinish(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
            string password = passwordInput.Password;
            string checkPwd = passwordCheck.Password;
            try
            {
                if (password.Length == 0 && checkPwd.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入一个长度至少为 8 位，并且含有大小写字母、数字或符号组成的密码。";
                    alertMsg.Visibility = Visibility.Visible;
                }
                else if (InformationVerify.IsPassword(password) && string.Equals(password, checkPwd))
                {
                    currentUser.Password = HashDirectory.GetEncryptedPassword(password);
                    freeSql.Update<User>(currentUser).ExecuteAffrows();

                    ContentDialog signInDialog = new ContentDialog
                    {
                        Title = "重置密码成功",
                        Content = "你的 Cactus 帐户密码重置完成。请牢记你的帐号与密码。点击确定按钮后，我们将自动为你登录。",
                        PrimaryButtonText = "确定",
                        DefaultButton = ContentDialogButton.Primary
                    };

                    ContentDialogResult result = await signInDialog.ShowAsync();
                    if (ContentDialogResult.Primary == result)
                    {
                        localSettings.Values["currentUser"] = currentUser.UID;
                        StartPage.startPage.mainContent.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    alertMsg.Text = "无效的密码，或两次输入的密码不匹配。";
                    alertMsg.Visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                alertMsg.Text = "未连接，请检查网络开关是否已打开。";
                alertMsg.Visibility = Visibility.Visible;
            }
        }

        private void ClearAlertMsg(object sender, RoutedEventArgs e)
        {
            alertMsg.Visibility = Visibility.Collapsed;
        }
    }
}