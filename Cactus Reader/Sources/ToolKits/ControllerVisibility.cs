using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Cactus_Reader.Sources.ToolKits
{
    public class ControllerVisibility
    {
        public static void ShowProgressBar(Microsoft.UI.Xaml.Controls.ProgressBar progressBar)
        {
            progressBar.Opacity = 1;
            progressBar.IsIndeterminate = true;
        }

        public static void HideProgressBar(Microsoft.UI.Xaml.Controls.ProgressBar progressBar)
        {
            progressBar.Opacity = 0;
            progressBar.IsIndeterminate = false;
        }
    }
}
