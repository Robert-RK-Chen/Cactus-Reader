﻿using Cactus_Reader.Entities;
using Cactus_Reader.Sources.ToolKits;
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
    public sealed partial class RegisterUserInfoPage : Page
    {
        readonly IFreeSql freeSql = IFreeSqlService.Instance;
        User currentUser = null;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            currentUser = (User)e.Parameter;
            userMailBlock.Text = currentUser.Email;
        }

        public RegisterUserInfoPage()
        {
            InitializeComponent();
        }

        private void BackPrevPage(object sender, RoutedEventArgs e)
        {
            contentFrame.Navigate(typeof(RegisterCodePage), currentUser, new SlideNavigationTransitionInfo()
            { 
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void ContinueLogon(object sender, RoutedEventArgs e)
        {
            string userName = userNameInput.Text;
            try
            {
                if (userName.Length == 0)
                {
                    alertMsg.Text = "若要继续，请输入一个用户名";
                }
                else if (!InformationVerify.IsUserName(userName))
                {
                    alertMsg.Text = "无效的用户名，有效的用户名仅由非空格起始或结尾的字母、数字与空格组成";
                }
                else if (!InformationVerify.UserNameEnabled(userName))
                {
                    alertMsg.Text = "用户名称已被注册，请换一个尝试。";
                }
                else
                {
                    currentUser.Name = userName;
                    contentFrame.Navigate(typeof(RegisterPwdPage), currentUser, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                alertMsg.Visibility = Visibility.Visible;
            }
            catch(Exception)
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