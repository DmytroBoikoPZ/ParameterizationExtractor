using MahApps.Metro.Controls;

namespace Quipu.ParameterizationExtractor.Desktop.Dialogs.AddStandaloneTable;

internal partial class AddStandaloneTableDialog : MetroWindow
{
    public AddStandaloneTableDialog(AddStandaloneTableDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseDialogWithResult = ok =>
        {
            DialogResult = ok;
            Close();
        };
    }
}
