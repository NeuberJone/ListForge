using System.Windows.Controls;
using System.Windows.Data;
using ListForge.ViewModels;

namespace ListForge.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    public void SetViewModel(MainViewModel vm)
    {
        DataContext = vm;

        // ---- Display ----
        ChkShowJsonTab.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.ShowJsonTab)) { Source = vm, Mode = BindingMode.TwoWay });
        ChkShowGenerateJsonButton.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.ShowGenerateJsonButton)) { Source = vm, Mode = BindingMode.TwoWay });
        ChkShowCopyJsonButton.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.ShowCopyJsonButton)) { Source = vm, Mode = BindingMode.TwoWay });

        // ---- Output ----
        ChkUseDefaultOutputDir.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.UseDefaultOutputDir)) { Source = vm, Mode = BindingMode.TwoWay });
        TxtOutputDir.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.OutputDir)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        ChkUseDefaultListName.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(vm.UseDefaultListName)) { Source = vm, Mode = BindingMode.TwoWay });
        TxtDefaultListName.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.DefaultListName)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        CmbDefaultCase.ItemsSource = vm.CaseLabels;
        CmbDefaultCase.SetBinding(ComboBox.SelectedItemProperty,
            new Binding(nameof(vm.DefaultCaseLabel)) { Source = vm, Mode = BindingMode.TwoWay });

        TxtDefaultSeparator.SetBinding(TextBox.TextProperty,
            new Binding(nameof(vm.DefaultSeparator)) { Source = vm, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        // ---- Theme ----
        CmbTheme.ItemsSource = vm.ThemeNames;
        CmbTheme.SetBinding(ComboBox.SelectedItemProperty,
            new Binding(nameof(vm.ThemeName)) { Source = vm, Mode = BindingMode.TwoWay });

        // ---- Size groups ----
        var male = vm.SizeGroupBindings["male"];
        TxtMaleBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.BaseSizes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtMalePre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.Prefixes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtMaleSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(male.Suffixes)) { Source = male, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var female = vm.SizeGroupBindings["female"];
        TxtFemaleBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.BaseSizes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtFemalePre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.Prefixes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtFemaleSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(female.Suffixes)) { Source = female, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var child = vm.SizeGroupBindings["child"];
        TxtChildBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.BaseSizes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtChildPre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.Prefixes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtChildSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(child.Suffixes)) { Source = child, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        var sock = vm.SizeGroupBindings["sock"];
        TxtSockBase.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.BaseSizes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtSockPre.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.Prefixes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        TxtSockSuf.SetBinding(TextBox.TextProperty,
            new Binding(nameof(sock.Suffixes)) { Source = sock, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        // ---- Size summary ----
        TxtSizeSummary.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(vm.SizeSummary)) { Source = vm });
    }
}
