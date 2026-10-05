using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace DS4Windows.Tests;

/// <summary>
/// Actual installer constructors/registration verifier -> actual runtime
/// definition verifiers. Registration and execution are never performed.
/// </summary>
[TestClass]
[DoNotParallelize]
public class OwnedStartupTaskContractTests
{
    private const string UserSid = "S-1-5-21-1234-5678-9012-1001";
    private const string OtherSid = "S-1-5-21-9999-9999-9999-1001";
    private static Dictionary<string, JsonElement> fixtures;

    [ClassInitialize]
    public static void ReadActualInstallerDefinitions(TestContext context)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PureDS4.sln")))
            root = root.Parent;
        Assert.IsNotNull(root);

        var start = new ProcessStartInfo
        {
            // Existing CI/local verification shell. Do not bypass execution
            // policy or reconfigure Windows PowerShell to run this fixture.
            FileName = "pwsh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
            Path.Combine(root.FullName, "utils", "test-viiper-launch-task.ps1"),
            "-BackendScript", Path.Combine(root.FullName, "extras", "install-viiper-backend.ps1"),
            "-EmitInstallerTasks",
        }) start.ArgumentList.Add(argument);

        using var process = Process.Start(start);
        Assert.IsNotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            // This is the only process owned by this fixture.
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("Memory-only installer fixture timed out.");
        }
        Assert.AreEqual(0, process.ExitCode, errors.GetAwaiter().GetResult());
        using var document = JsonDocument.Parse(output.GetAwaiter().GetResult());
        fixtures = document.RootElement.EnumerateArray().ToDictionary(
            task => task.GetProperty("Name").GetString(), task => task.Clone());
        Assert.AreEqual(2, fixtures.Count);
    }

    [DataTestMethod]
    [DataRow("RunPureDS4")]
    [DataRow("RunPureDS4VIIPER")]
    public void RuntimeAcceptsTheTaskTheInstallerActuallyCreates(string name)
    {
        Assert.AreEqual(7, fixtures[name].GetProperty("Priority").GetInt32());
        using var service = new TaskService();
        var definition = Definition(service, name);
        Assert.AreEqual(ProcessPriorityClass.BelowNormal, definition.Settings.Priority);
        Assert.IsTrue(Accept(name, definition), name + " must not require another repair.");

        // Decoded XML is read back into a fresh, unregistered definition:
        // no reliance on a cached server or task object from the repair session.
        var reopened = service.NewTask();
        reopened.XmlText = definition.XmlText;
        Assert.IsTrue(Accept(name, reopened), name + " must survive definition reload.");
    }

    [DataTestMethod]
    [DataRow("RunPureDS4")]
    [DataRow("RunPureDS4VIIPER")]
    public void ExistingHighPriorityTasksRemainCompatible(string name)
    {
        using var service = new TaskService();
        Assert.IsTrue(Accept(name, Definition(service, name, priority: 1)));
    }

    [DataTestMethod]
    [DataRow("RunPureDS4")]
    [DataRow("RunPureDS4VIIPER")]
    public void CompatiblePriorityNeverAdoptsAnotherTargetOrAccount(string name)
    {
        using var service = new TaskService();
        var changes = new Action<TaskDefinition>[]
        {
            d => ((ExecAction)d.Actions[0]).Path = @"C:\OtherProduct\app.exe",
            d => ((ExecAction)d.Actions[0]).Arguments = "unexpected",
            d => ((ExecAction)d.Actions[0]).WorkingDirectory = @"C:\OtherProduct",
            d => d.Principal.UserId = OtherSid,
            d => d.Principal.RunLevel = TaskRunLevel.LUA,
            d => d.Principal.LogonType = TaskLogonType.ServiceAccount,
            d => d.Actions.Add(new ExecAction(@"C:\OtherProduct\app.exe")),
        };
        foreach (int priority in new[] { 7, 1 })
        foreach (var change in changes)
        {
            var definition = Definition(service, name, priority);
            change(definition);
            Assert.IsFalse(Accept(name, definition), "Foreign task accepted at priority " + priority);
        }
        Assert.IsFalse(Accept(name, Definition(service, name, 1), sid: OtherSid));
        Assert.IsFalse(Accept(name, Definition(service, name, 1), sid: null));
    }

    [DataTestMethod]
    [DataRow("RunPureDS4")]
    [DataRow("RunPureDS4VIIPER")]
    public void DisabledOrWrongShapeTasksRemainRejected(string name)
    {
        using var service = new TaskService();
        Assert.IsFalse(Accept(name, Definition(service, name, 1), enabled: false));
        var disabled = Definition(service, name, 1);
        disabled.Settings.Enabled = false;
        Assert.IsFalse(Accept(name, disabled));
        var noAction = Definition(service, name, 1);
        noAction.Actions.Clear();
        Assert.IsFalse(Accept(name, noAction));
        foreach (int priority in new[] { 0, 4, 10 })
            Assert.IsFalse(Accept(name, Definition(service, name, priority)));

        var wrongTrigger = Definition(service, name, 1);
        if (name == "RunPureDS4VIIPER") wrongTrigger.Triggers.Add(new LogonTrigger());
        else wrongTrigger.Triggers.Clear();
        Assert.IsFalse(Accept(name, wrongTrigger));

        if (name == "RunPureDS4")
        {
            foreach (var change in new Action<TaskDefinition>[]
            {
                d => ((LogonTrigger)d.Triggers[0]).UserId = OtherSid,
                d => d.Triggers[0].Enabled = false,
                d => d.Triggers.Add(new LogonTrigger()),
                d => d.Settings.ExecutionTimeLimit = TimeSpan.FromHours(2),
                d => d.Settings.MultipleInstances = TaskInstancesPolicy.Parallel,
                d => d.Settings.DisallowStartIfOnBatteries = true,
                d => d.Settings.StopIfGoingOnBatteries = true,
            })
            {
                var definition = Definition(service, name, 1);
                change(definition);
                Assert.IsFalse(Accept(name, definition));
            }
        }
    }

    [TestMethod]
    public void MissingDefinitionsFailClosedWithLauncherDiagnostics()
    {
        Assert.IsFalse(StartupMethods.TaskTargetsCurrentExecutable(null, true,
            @"C:\Program Files\PureDS4\PureDS4.exe", @"C:\Program Files\PureDS4", UserSid));
        Assert.IsFalse(ViiperSetupManager.IsViiperStartupTaskDefinitionValid(null, true,
            @"C:\Program Files\PureDS4\VIIPER\viiper.exe", UserSid, out string reason));
        StringAssert.Contains(reason, "on-demand task shape");
    }

    private static bool Accept(string name, TaskDefinition definition,
        bool enabled = true, string sid = UserSid)
    {
        JsonElement fixture = fixtures[name];
        string path = fixture.GetProperty("Execute").GetString();
        if (name == "RunPureDS4")
            return StartupMethods.TaskTargetsCurrentExecutable(definition, enabled,
                path, fixture.GetProperty("WorkingDirectory").GetString(), sid);
        bool accepted = ViiperSetupManager.IsViiperStartupTaskDefinitionValid(
            definition, enabled, path, sid, out string reason);
        if (accepted) Assert.IsNull(reason);
        else Assert.IsFalse(string.IsNullOrWhiteSpace(reason));
        return accepted;
    }

    private static TaskDefinition Definition(TaskService service, string name, int? priority = null)
    {
        JsonElement task = fixtures[name];
        Assert.AreEqual("Highest", task.GetProperty("RunLevel").GetString());
        Assert.AreEqual("Interactive", task.GetProperty("LogonType").GetString());
        Assert.AreEqual(UserSid, task.GetProperty("UserId").GetString());
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement Element(string key, object value) => new(ns + key, value);
        var triggers = Element("Triggers", null);
        foreach (JsonElement trigger in task.GetProperty("Triggers").EnumerateArray())
        {
            Assert.AreEqual("MSFT_TaskLogonTrigger", trigger.GetProperty("Type").GetString());
            var logon = Element("LogonTrigger", Element("Enabled", trigger.GetProperty("Enabled").GetBoolean()));
            string userId = trigger.GetProperty("UserId").GetString();
            if (!string.IsNullOrEmpty(userId)) logon.Add(Element("UserId", userId));
            triggers.Add(logon);
        }
        var xml = new XElement(ns + "Task", new XAttribute("version", "1.3"),
            triggers,
            Element("Principals", new XElement(ns + "Principal", new XAttribute("id", "Owner"),
                Element("UserId", task.GetProperty("UserId").GetString()),
                Element("LogonType", "InteractiveToken"), Element("RunLevel", "HighestAvailable"))),
            Element("Settings", new object[]
            {
                Element("Enabled", task.GetProperty("Enabled").GetBoolean()),
                Element("Priority", priority ?? task.GetProperty("Priority").GetInt32()),
                Element("ExecutionTimeLimit", task.GetProperty("ExecutionTimeLimit").GetString()),
                Element("MultipleInstancesPolicy", ((TaskInstancesPolicy)task.GetProperty("MultipleInstances").GetInt32()).ToString()),
                Element("DisallowStartIfOnBatteries", task.GetProperty("DisallowStartIfOnBatteries").GetBoolean()),
                Element("StopIfGoingOnBatteries", task.GetProperty("StopIfGoingOnBatteries").GetBoolean()),
            }),
            new XElement(ns + "Actions", new XAttribute("Context", "Owner"),
                Element("Exec", new object[]
                {
                    Element("Command", task.GetProperty("Execute").GetString()),
                    Element("Arguments", task.GetProperty("Arguments").GetString()),
                    Element("WorkingDirectory", task.GetProperty("WorkingDirectory").GetString()),
                })));
        var definition = service.NewTask(); // COM object only; never registered.
        definition.XmlText = xml.ToString(SaveOptions.DisableFormatting);
        return definition;
    }
}
