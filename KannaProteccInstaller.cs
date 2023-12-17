#if UNITY_EDITOR && UNITY_2022
using System;
using System.IO;
using System.IO.Compression;
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

        var Branch = GitHubAPI.GetBranch("PlagueVRC", "AntiRip", "universal-shader-support");

        var LastCommit = Branch.commit;

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
            client.DownloadFile("https://github.com/PlagueVRC/AntiRip/archive/refs/heads/universal-shader-support.zip", "KannaProtecc.zip");
        }

        ZipFile.ExtractToDirectory("KannaProtecc.zip", "Assets/", true);
        
        File.Delete("KannaProtecc.zip");
    }
}

public class GitHubAPI
{
    #region JSON Classes
    
    public class BranchResponse
    {
        public Commit commit { get; set; }
        [JsonProperty("protected")]
        public bool protecc { get; set; }
    }

    public class Commit
    {
        public string sha { get; set; }
    }

    #endregion

    public static BranchResponse GetBranch(string Owner, string Repo, string Branch)
    {
        using (var client = new WebClient())
        {
            client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0";

            var branch = JsonConvert.DeserializeObject<BranchResponse>(client.DownloadString($"https://api.github.com/repos/{Owner}/{Repo}/branches/{Branch}"));
            
            return branch;
        }
    }
}
#endif