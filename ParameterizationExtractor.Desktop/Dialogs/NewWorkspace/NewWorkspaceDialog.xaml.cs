using MahApps.Metro.Controls;

namespace Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;

internal partial class NewWorkspaceDialog : MetroWindow
{
    public NewWorkspaceDialog(NewWorkspaceDialogViewModel viewModel)
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
