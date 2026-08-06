using System.Windows.Controls;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class AboutView : UserControl
{
    public AboutView() => InitializeComponent();

    public void SetViewModel(MainViewModel vm)
    {
        DataContext = vm;
    }
}
