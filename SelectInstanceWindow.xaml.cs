using System.Windows;
using ProfanityFilterEditor.Models;

namespace ProfanityFilterEditor;

public partial class SelectInstanceWindow : Window
{
    public MinecraftInstance? SelectedInstance { get; private set; }

    public SelectInstanceWindow(List<MinecraftInstance> instances)
    {
        InitializeComponent();
        InstanceList.ItemsSource = instances;
        if (instances.Count > 0)
        {
            InstanceList.SelectedIndex = 0;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (InstanceList.SelectedItem is MinecraftInstance instance)
        {
            SelectedInstance = instance;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(this, "Pick an install from the list first.", "Nothing selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
