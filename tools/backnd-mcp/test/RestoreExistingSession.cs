using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BackEnd;

// Verification helper only. Not exposed as an MCP tool. Never creates an account.
public static class RestoreExistingSession
{
    public static async Task<string> Run()
    {
        if (Backend.IsLogin) return "already_logged_in";
        var initialized = Backend.Initialize();
        if (!initialized.IsSuccess()) return "initialize_failed:" + initialized.GetStatusCode();
        var completion = new TaskCompletionSource<BackendReturnObject>();
        Backend.BMember.LoginWithTheBackendToken(response => completion.TrySetResult(response));
        if (await Task.WhenAny(completion.Task, Task.Delay(15000)) != completion.Task)
            return "restore_timeout";
        var restored = await completion.Task;
        if (restored.IsSuccess()) return "restored";
        return "restore_failed:" + restored.GetStatusCode();
    }

    public static async Task<string> LoginExistingEditorTestAccount()
    {
        if (Backend.IsLogin) return "already_logged_in";
        var initialized = Backend.Initialize();
        if (!initialized.IsSuccess()) return "initialize_failed:" + initialized.GetStatusCode();
        // Reuse the repository's existing Editor test credentials without copying them
        // into tool arguments, logs, or another configuration file. No signup fallback.
        var source = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "WorkSpace/USW/Scripts/OutGame/BackendManager.cs"));
        var match = Regex.Match(source, @"CustomLogin\(""([^""]+)"",\s*""([^""]+)""\)");
        if (!match.Success) return "editor_test_login_not_found";
        var completion = new TaskCompletionSource<BackendReturnObject>();
        Backend.BMember.CustomLogin(match.Groups[1].Value, match.Groups[2].Value,
            response => completion.TrySetResult(response));
        if (await Task.WhenAny(completion.Task, Task.Delay(15000)) != completion.Task)
            return "editor_test_login_timeout";
        var response = await completion.Task;
        return response.IsSuccess() ? "editor_test_login_succeeded" : "editor_test_login_failed:" + response.GetStatusCode();
    }
}
