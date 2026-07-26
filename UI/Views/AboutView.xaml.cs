using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class AboutView : UserControl
{
    public AboutView() => InitializeComponent();

    public void SetViewModel(MainViewModel vm)
    {
        DataContext = vm;
        vm.PropertyChanged += ViewModel_PropertyChanged;
        RefreshUpdateActions(vm);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainViewModel vm
            && (e.PropertyName == nameof(MainViewModel.HasAvailableUpdate)
                || e.PropertyName == nameof(MainViewModel.IsUpdateBusy)))
        {
            RefreshUpdateActions(vm);
        }
    }

    private void RefreshUpdateActions(MainViewModel vm)
    {
        BtnDownloadAvailableUpdate.Visibility = vm.HasAvailableUpdate && !vm.IsUpdateBusy
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
