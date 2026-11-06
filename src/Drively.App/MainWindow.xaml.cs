using System.Windows;
using System.Windows.Controls;
using Drively.App.Services;
using Drively.App.ViewModels;

namespace Drively.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Navigation follows the radio button's checked state, so mouse, keyboard (arrow keys)
    /// and assistive technology (UI Automation) all navigate the same way.
    /// </summary>
    private void OnNavigationChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { CommandParameter: PageKey page } && DataContext is MainViewModel vm && vm.CurrentPageKey != page)
            vm.NavigateCommand.Execute(page);
    }
}
