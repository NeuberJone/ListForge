using System.Windows.Controls;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class HistoryView : UserControl
{
    public HistoryView() => InitializeComponent();

    public void SetViewModel(MainViewModel vm) => DataContext = vm;
}
