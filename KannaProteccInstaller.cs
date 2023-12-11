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
        Debug.Log("Installing");

        var Commits = GitHubAPI.GetCommits("PlagueVRC", "AntiRip");

        var LastCommit = Commits.First();

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

        client.DownloadFile("https://github.com/PlagueVRC/AntiRip/archive/refs/heads/main.zip", "KannaProtecc.zip");
        
        ZipFile.ExtractToDirectory("KannaProtecc.zip", "Assets/", true);
    }
}

public class GitHubAPI
{
    #region JSON Classes

    public class CommitResponse
    {
        public string sha { get; set; }
        public string node_id { get; set; }
        public Commit commit { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string comments_url { get; set; }
        public Author author { get; set; }
        public Committer committer { get; set; }
        public Parents[] parents { get; set; }
    }

    public class Commit
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
        public string signature { get; set; }
        public string payload { get; set; }
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

    #endregion

    public static CommitResponse[] GetCommits(string Owner, string Repo)
    {
        using var client = new WebClient();
        client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0";

        var commits = JsonConvert.DeserializeObject<CommitResponse[]>(client.DownloadString($"https://api.github.com/repos/{Owner}/{Repo}/commits"));

        return commits;
    }
}