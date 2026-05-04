#nullable enable
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Quipu.ParameterizationExtractor.Desktop.Controls.ConnectionEditor;
using Quipu.ParameterizationExtractor.Desktop.Dialogs.NewWorkspace;
using Quipu.ParameterizationExtractor.Desktop.Services.Workspace;
using Quipu.ParameterizationExtractor.Logic.Connectivity;
using Tests.Desktop.Fakes;

namespace Tests.Desktop;

[TestFixture]
public class NewWorkspaceDialogViewModelTests
{
    private FakeDialogService _dialog = null!;
    private FakeConnectionTester _tester = null!;
    private InMemoryPasswordProtector _protector = null!;
    private FakeWorkspaceStore _store = null!;
    private string _tempDir = null!;

    [SetUp]
    public void Init()
    {
        _dialog = new FakeDialogService();
        _tester = new FakeConnectionTester();
        _protector = new InMemoryPasswordProtector();
        _store = new FakeWorkspaceStore();
        _tempDir = Path.Combine(Path.GetTempPath(), "buldozer-newws-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private NewWorkspaceDialogViewModel NewDialogVm()
    {
        var editor = new ConnectionEditorViewModel(_tester, _protector, NullLogger<ConnectionEditorViewModel>.Instance);
        return new NewWorkspaceDialogViewModel(editor, _store, _dialog, NullLogger<NewWorkspaceDialogViewModel>.Instance);
    }

    [Test]
    public async Task Confirm_NewMode_TestNotRun_ShowsMessage_DoesNotClose()
    {
        var vm = NewDialogVm();
        vm.InitForNew();
        vm.WorkspaceName = "x";
        vm.SavePath = Path.Combine(_tempDir, "x.bws");
        bool? closeArg = null;
        vm.CloseDialogWithResult = ok => closeArg = ok;

        await vm.ConfirmCommand.ExecuteAsync(null);

        _dialog.Calls.Should().ContainSingle(c => c.Method == "ShowMessage");
        closeArg.Should().BeNull("dialog must stay open until a successful Test");
        vm.CreatedResult.Should().BeNull();
    }

    [Test]
    public async Task Confirm_NewMode_AfterSuccessfulTest_WritesSkeletonAndReturnsPath()
    {
        var vm = NewDialogVm();
        vm.InitForNew();
        vm.WorkspaceName = "patient-clearing";
        var savePath = Path.Combine(_tempDir, "patient-clearing.bws");
        vm.SavePath = savePath;
        vm.ConnectionEditor.Server = "h";
        vm.ConnectionEditor.Database = "d";
        _tester.EnqueueResult(new ConnectionTestResult(true, 5, 7, null));
        await vm.ConnectionEditor.TestCommand.ExecuteAsync(null);
        bool? closeArg = null;
        vm.CloseDialogWithResult = ok => closeArg = ok;

        await vm.ConfirmCommand.ExecuteAsync(null);

        closeArg.Should().BeTrue();
        vm.CreatedResult.Should().NotBeNull();
        vm.CreatedResult!.Path.Should().Be(savePath);
        _store.SaveCalls.Should().ContainSingle();
        _store.SaveCalls[0].Path.Should().Be(savePath);
        _store.SaveCalls[0].Workspace.Name.Should().Be("patient-clearing");
    }

    [Test]
    public async Task Confirm_EditMode_AfterSuccessfulTest_ReturnsUpdatedSource()
    {
        var vm = NewDialogVm();
        var current = new WorkspaceSource { Server = "old", Database = "db", Auth = "windows" };
        vm.InitForEdit(current, plaintextPassword: null);
        vm.ConnectionEditor.Server = "new";
        _tester.EnqueueResult(new ConnectionTestResult(true, 1, 1, null));
        await vm.ConnectionEditor.TestCommand.ExecuteAsync(null);
        bool? closeArg = null;
        vm.CloseDialogWithResult = ok => closeArg = ok;

        await vm.ConfirmCommand.ExecuteAsync(null);

        closeArg.Should().BeTrue();
        vm.EditedSource.Should().NotBeNull();
        vm.EditedSource!.Server.Should().Be("new");
        _store.SaveCalls.Should().BeEmpty("Edit mode does not write through the dialog VM — MainWindowViewModel saves");
    }

    [Test]
    public void Cancel_ClearsResultAndClosesWithFalse()
    {
        var vm = NewDialogVm();
        vm.InitForNew();
        bool? closeArg = null;
        vm.CloseDialogWithResult = ok => closeArg = ok;

        vm.CancelCommand.Execute(null);

        closeArg.Should().BeFalse();
        vm.CreatedResult.Should().BeNull();
        vm.EditedSource.Should().BeNull();
    }

    [Test]
    public void InitForEdit_PrepopulatesConnectionEditor()
    {
        var vm = NewDialogVm();
        var source = new WorkspaceSource
        {
            Server = "h", Database = "d", Auth = "sql", User = "sa",
            PasswordEncrypted = _protector.Protect("p"),
        };

        vm.InitForEdit(source, plaintextPassword: "p");

        vm.IsEditMode.Should().BeTrue();
        vm.IsNewMode.Should().BeFalse();
        vm.Title.Should().Be("Edit connection");
        vm.ConfirmButtonText.Should().Be("Save");
        vm.ConnectionEditor.Server.Should().Be("h");
        vm.ConnectionEditor.IsSqlAuth.Should().BeTrue();
        vm.ConnectionEditor.User.Should().Be("sa");
        vm.ConnectionEditor.Password.Should().Be("p");
    }
}
