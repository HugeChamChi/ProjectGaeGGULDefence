using System;
using System.Threading.Tasks;
using BackEnd;
using LitJson;

// Compiled in memory by Pipeline run_script. This file is deliberately outside Assets.
// No Initialize, login, user-data write, chart upload, or scene/asset mutation.
public static class BackndChartReader
{
    public static async Task<string> Run(string operation, string chartName)
    {
        if (operation != "status" && operation != "list" && operation != "read")
            throw new ArgumentException("Unsupported chart operation.");
        if (operation == "status")
            return "{\"loggedIn\":" + (Backend.IsLogin ? "true" : "false") + "}";
        if (!Backend.IsLogin) throw new InvalidOperationException("BACKND login is required in the running Editor. No automatic login was attempted.");

        var listing = new TaskCompletionSource<BackendReturnObject>();
        Backend.Chart.GetChartListV2(response => listing.TrySetResult(response));
        var responseList = await Wait(listing.Task);
        Check(responseList);
        var rows = responseList.FlattenRows();
        if (operation == "list") return rows.ToJson();

        string fileId = null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i]["chartName"].ToString() != chartName) continue;
            if (fileId != null) throw new InvalidOperationException("Duplicate chart name; specify a unique chart in the console.");
            fileId = rows[i]["selectedChartFileId"].ToString();
        }
        if (string.IsNullOrEmpty(fileId) || fileId == "0")
            throw new InvalidOperationException("Chart is missing or has no applied file.");
        var contents = new TaskCompletionSource<BackendReturnObject>();
        Backend.Chart.GetChartContents(fileId, response => contents.TrySetResult(response));
        var data = await Wait(contents.Task);
        Check(data);
        var result = new JsonData();
        result["chartName"] = chartName;
        result["selectedChartFileId"] = fileId;
        result["rows"] = data.FlattenRows();
        return result.ToJson();
    }

    private static async Task<BackendReturnObject> Wait(Task<BackendReturnObject> task)
    {
        if (await Task.WhenAny(task, Task.Delay(10000)) != task)
            throw new TimeoutException("BACKND chart read timed out.");
        return await task;
    }

    private static void Check(BackendReturnObject response)
    {
        if (!response.IsSuccess()) throw new InvalidOperationException("BACKND chart read failed: " + response.GetStatusCode());
    }
}
