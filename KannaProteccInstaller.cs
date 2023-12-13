#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class KannaProteccInstaller
{
    [InitializeOnLoadMethod]
    static void CheckForInstall()
    {
        if (Environment.UserName == "krewe") // Me
        {
            return;
        }
        
        Debug.Log("Installing");

        var Branch = GitHubAPI.GetBranch("PlagueVRC", "AntiRip", "universal-shader-support");

        var LastCommit = Branch.commit;

        Debug.Log($"Last Commit: {LastCommit.sha}");

        var CommitFile = "Assets/LastKannaProteccCommit.txt";

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

    static void Install()
    {
        using var client = new WebClient();

        client.DownloadFile("https://github.com/PlagueVRC/AntiRip/archive/refs/heads/universal-shader-support.zip", "KannaProtecc.zip");

        ZipFile.ExtractToDirectory("KannaProtecc.zip", "Assets/", true);
        
        File.Delete("KannaProtecc.zip");
    }
}

public class GitHubAPI
{
    #region JSON Classes
    
    public class BranchResponse
    {
        public string name { get; set; }
        public Commit commit { get; set; }
        public _links _links { get; set; }
        [JsonProperty("protected")]
        public bool protecc {
            get;
            set;
        }

        public Protection protection { get; set; }
        public string protection_url { get; set; }
    }

    public class Commit
    {
        public string sha { get; set; }
        public string node_id { get; set; }
        public Commit1 commit { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string comments_url { get; set; }
        public Author author { get; set; }
        public Committer committer { get; set; }
        public Parents[] parents { get; set; }
    }

    public class Commit1
    {
        public Author1 author { get; set; }
        public Committer1 committer { get; set; }
        public string message { get; set; }
        public Tree tree { get; set; }
        public string url { get; set; }
        public int comment_count { get; set; }
        public Verification verification { get; set; }
    }

    public class Author1
    {
        public string name { get; set; }
        public string email { get; set; }
        public string date { get; set; }
    }

    public class Committer1
    {
        public string name { get; set; }
        public string email { get; set; }
        public string date { get; set; }
    }

    public class Tree
    {
        public string sha { get; set; }
        public string url { get; set; }
    }

    public class Verification
    {
        public bool verified { get; set; }
        public string reason { get; set; }
        public object signature { get; set; }
        public object payload { get; set; }
    }

    public class Author
    {
        public string login { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string avatar_url { get; set; }
        public string gravatar_id { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string followers_url { get; set; }
        public string following_url { get; set; }
        public string gists_url { get; set; }
        public string starred_url { get; set; }
        public string subscriptions_url { get; set; }
        public string organizations_url { get; set; }
        public string repos_url { get; set; }
        public string events_url { get; set; }
        public string received_events_url { get; set; }
        public string type { get; set; }
        public bool site_admin { get; set; }
    }

    public class Committer
    {
        public string login { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string avatar_url { get; set; }
        public string gravatar_id { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string followers_url { get; set; }
        public string following_url { get; set; }
        public string gists_url { get; set; }
        public string starred_url { get; set; }
        public string subscriptions_url { get; set; }
        public string organizations_url { get; set; }
        public string repos_url { get; set; }
        public string events_url { get; set; }
        public string received_events_url { get; set; }
        public string type { get; set; }
        public bool site_admin { get; set; }
    }

    public class Parents
    {
        public string sha { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
    }

    public class _links
    {
        public string self { get; set; }
        public string html { get; set; }
    }

    public class Protection
    {
        public bool enabled { get; set; }
        public Required_status_checks required_status_checks { get; set; }
    }

    public class Required_status_checks
    {
        public string enforcement_level { get; set; }
        public object[] contexts { get; set; }
        public object[] checks { get; set; }
    }

    #endregion

    public static BranchResponse GetBranch(string Owner, string Repo, string Branch)
    {
        using var client = new WebClient();
        client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0";

        var branch = JsonConvert.DeserializeObject<BranchResponse>(client.DownloadString($"https://api.github.com/repos/{Owner}/{Repo}/branches/{Branch}"));

        return branch;
    }
}
#endif