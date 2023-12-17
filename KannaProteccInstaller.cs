#if UNITY_EDITOR && UNITY_2022
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class KannaProteccInstaller
{
    [InitializeOnLoadMethod]
    private static void CheckForInstall()
    {
        if (Environment.UserName == "krewe") // Me
        {
            return;
        }
        
        Debug.Log("Installing");

        var Commits = GitHubAPI.GetCommits("PlagueVRC", "AntiRip");

        var LastCommit = Commits.First();

        Debug.Log($"Last Commit: {LastCommit.sha}");

        const string CommitFile = "Assets/LastKannaProteccCommit.txt";

        if (File.Exists(CommitFile))
        {
            Debug.Log("Previous Install Found, Comparing..");

            if (File.ReadAllText(CommitFile) != LastCommit.sha)
            {
                Install();
                
                File.WriteAllText(CommitFile, LastCommit.sha);
            }
            else
            {
                Debug.Log("Already Up To Date!");
            }
        }
        else
        {
            Install();
            
            File.WriteAllText(CommitFile, LastCommit.sha);
        }
    }

    private static void Install()
    {
        using (var client = new WebClient())
        {
            client.DownloadFile("https://github.com/PlagueVRC/AntiRip/archive/refs/heads/main.zip", "KannaProtecc.zip");
        }

        ZipFile.ExtractToDirectory("KannaProtecc.zip", "Assets/", true);
        
        File.Delete("KannaProtecc.zip");
    }
}

public class GitHubAPI
{
    #region JSON Classes

    public class CommitResponse
    {
        public string sha { get; set; }
    }

    #endregion

    public static CommitResponse[] GetCommits(string Owner, string Repo)
    {
        using (var client = new WebClient())
        {
            client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0";

            var commits = JsonConvert.DeserializeObject<CommitResponse[]>(client.DownloadString($"https://api.github.com/repos/{Owner}/{Repo}/commits"));

            return commits;
        }
    }
}
#endif